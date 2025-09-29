using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting;
using Migration.Infrastructure.services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Threading.Channels;

namespace Migration.Worker.services
{
    public class MigrationWorkerService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly string _queueName = "migration_queue";
        private  IConnection? _connection;
        private  IChannel? _channel;

        public MigrationWorkerService(IServiceScopeFactory scopeFactory, IConnection? connection)
        {
            _scopeFactory = scopeFactory;
            _connection = connection;
            InitRabbitMQ();
            
        }

        private void InitRabbitMQ()
        {
            //var factory = new ConnectionFactory { HostName = "rabbitmq", UserName = "admin", Password = "admin" };
            //_connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            //_connection=_
            _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
            _channel.QueueDeclareAsync(queue: _queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
        }

        protected override async Task<Task> ExecuteAsync(CancellationToken stoppingToken)
        {
            

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var userId = Encoding.UTF8.GetString(body);

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MigrationDbContext>();
                var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

                // Verifica slot
                var slot = await db.MigrationSlots.FirstOrDefaultAsync(s => !s.IsOccupied);
                if (slot == null)
                {
                    await auditLog.LogAsync("SlotUnavailable", "WorkerService", $"UserId={userId}", "Failed");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                    return;
                }

                // Occupa slot
                slot.IsOccupied = true;
                slot.UserId = userId;
                slot.StartedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                await auditLog.LogAsync("MigrationStarted", "WorkerService", $"UserId={userId}, Slot={slot.Id}", "InProgress");

                try
                {
                    var user = await db.UserMigrations.FirstOrDefaultAsync(u => u.UserId == userId);
                    if (user == null) throw new Exception("User not found");

                    // Simula la migrazione
                    await Task.Delay(2000);

                    user.IsMigrated = true;
                    user.MigrationDate = DateTime.UtcNow;
                    user.Status = "Success";

                    await db.SaveChangesAsync();
                    await auditLog.LogAsync("MigrationSuccess", "WorkerService", $"UserId={userId}", "Success");
                }
                catch (Exception ex)
                {
                    var user = await db.UserMigrations.FirstOrDefaultAsync(u => u.UserId == userId);
                    if (user != null)
                    {
                        user.Status = $"Failed: {ex.Message}";
                        await db.SaveChangesAsync();
                    }
                    await auditLog.LogAsync("MigrationFailed", "WorkerService", $"UserId={userId}, Error={ex.Message}", "Failed");
                }
                finally
                {
                    // Libera slot
                    slot.IsOccupied = false;
                    slot.UserId = null;
                    slot.StartedAt = null;
                    await db.SaveChangesAsync();

                    await auditLog.LogAsync("SlotReleased", "WorkerService", $"UserId={userId}, Slot={slot.Id}", "Done");

                    // Conferma al broker
                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
            };

            await _channel.BasicConsumeAsync(queue: _queueName, autoAck: false, consumer: consumer);
            return Task.CompletedTask;


        }
    }
}