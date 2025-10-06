using Infrastructure;
using Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting;
using Migration.Infrastructure.models;
using Migration.Infrastructure.services;
using Migration.Infrastructure.Utilities;
using Migration.Worker.saga;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Migration.Worker.services
{
    public class MigrationWorkerService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly string _queueName = "migration_queue";
        private IConnection? _connection;
        private IChannel? _channel;

        public MigrationWorkerService(IServiceScopeFactory scopeFactory, IConnection? connection)
        {
            _scopeFactory = scopeFactory;
            _connection = connection;
            InitRabbitMQ();

        }

        private void InitRabbitMQ()
        {
            _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
            _channel.QueueDeclareAsync(queue: _queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var mqReq = JsonSerializer.Deserialize<MqModel>(Encoding.UTF8.GetString(body));
                var oldUser = mqReq?.OldUser;
                var forced = mqReq?.Forced ?? false;
                var slotId = mqReq?.SlotId;

                if (oldUser == null)
                {
                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MigrationDbContext>();
                var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

                try
                {
                    // Se forced → salta la saga e migra direttamente
                    if (forced)
                    {
                        var already = await db.UserMigrations.AnyAsync(u => u.UserId == oldUser.Id.ToString() && u.IsMigrated);
                        if (already)
                        {
                            await auditLog.LogAsync(oldUser.Id.ToString(), "ForcedMigrationSkipped", "WorkerService", $"User {oldUser.Id} già migrato", "Skipped");
                            await _channel.BasicAckAsync(ea.DeliveryTag, false);
                            return;
                        }

                        db.UserMigrations.Add(new UserMigration
                        {
                            UserId = oldUser.Id.ToString(),
                            IsMigrated = true,
                            MigrationDate = DateTime.UtcNow,
                            Status = "Success"
                        });
                        await db.SaveChangesAsync();
                        await auditLog.LogAsync(oldUser.Id.ToString(), "ForcedMigration", "WorkerService", $"Migrazione forzata completata per {oldUser.Id}", "Success");
                        await _channel.BasicAckAsync(ea.DeliveryTag, false);
                        return;
                    }

                    var context = new MigrationContext(oldUser,scope);

                    var saga = new MigrationSagaCoordinator();

                    saga.AddStep(new ReserveSlotStep(slotId));
                    saga.AddStep(new NormalizeUserStep());
                    saga.AddStep(new PersistUserStep());

                    await saga.ExecuteAsync(context);

                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    await auditLog.LogAsync(oldUser?.Id.ToString() ?? "Unknown", "WorkerService", $"Error={ex.Message}", "Failed", success: false);
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }

            };
        }
    }
}