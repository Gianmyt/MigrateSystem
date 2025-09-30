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

        protected override async Task<Task> ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var slot = (MigrationSlot?)null;
                var body = ea.Body.ToArray();
                var mqRequ = JsonSerializer.Deserialize<MqModel>(Encoding.UTF8.GetString(body));
                var oldUser = mqRequ?.OldUser;
                var forced = mqRequ?.Forced ?? false;
                var slotId = mqRequ?.SlotId;

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
                    // ---- Transazione completa: slot + migrazione ----
                    await using var tx = await db.Database.BeginTransactionAsync();

                    // 1) Selezione slot
                    if (forced)
                    {
                        slot = await db.MigrationSlots
                            .FromSqlRaw("SELECT TOP(1) * FROM MigrationSlots WITH (UPDLOCK, ROWLOCK, READPAST) WHERE IsOccupied = 0")
                            .FirstOrDefaultAsync();
                    }
                    else
                    {
                        slot = await db.MigrationSlots
                            .FromSqlRaw("SELECT TOP(1) * FROM MigrationSlots WITH (UPDLOCK, ROWLOCK, READPAST) WHERE Id = {0}", slotId)
                            .FirstOrDefaultAsync();
                    }

                    if (slot == null)
                    {
                        await auditLog.LogAsync("SlotUnavailable", "WorkerService", $"User={oldUser.Id}", "Failed");
                        await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                        await auditLog.LogAsync("Retry beacuse slot unavailable", "WorkerService", $"User={oldUser.Id}", "Info");

                        return;
                    }

                    slot.IsOccupied = true;
                    slot.UserId = oldUser.Id.ToString();
                    slot.StartedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();

                    await auditLog.LogAsync("MigrationStarted", "WorkerService", $"UserId={oldUser.Id}, Slot={slot.Id}", "InProgress");

                    // 2) Verifica duplicati
                    var alreadyMigrated = await db.UserMigrations
                        .AnyAsync(u => u.UserId == oldUser.Id.ToString() && u.IsMigrated);

                    if (alreadyMigrated)
                    {
                        await auditLog.LogAsync("MigrationSkipped", "WorkerService", $"UserId={oldUser.Id}", "AlreadyMigrated");
                        await tx.RollbackAsync();
                        await _channel.BasicAckAsync(ea.DeliveryTag, false);
                        return;
                    }

                    // 3) Mapping e normalizzazione (può lanciare ArgumentException)
                    var newUser = UserMapper.Map(oldUser);

                    // 4) Salvataggio
                    //db.NewUsers.Add(newUser);
                    db.UserMigrations.Add(new UserMigration
                    {
                        UserId = oldUser.Id.ToString(),
                        IsMigrated = true,
                        MigrationDate = DateTime.UtcNow,
                        Status = "Success"
                    });

                    await db.SaveChangesAsync();
                    await tx.CommitAsync();

                    await auditLog.LogAsync("MigrationSuccess", "WorkerService", $"UserId={oldUser.Id}", "Success");

                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (ArgumentException argEx)
                {
                    await auditLog.LogAsync("ValidationFailed", "WorkerService",
                        $"UserId={oldUser?.Id}, Error={argEx.Message}", "Failed");

                    await _channel.BasicAckAsync(ea.DeliveryTag, false); // non rimettiamo in coda
                }
                catch (Exception ex)
                {
                    await auditLog.LogAsync("MigrationFailed", "WorkerService", $"UserId={oldUser?.Id}, Error={ex.Message}", "Failed");

                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true); // errore transiente → retry
                }
                finally
                {
                    if (slot != null)
                    {
                        slot.IsOccupied = false;
                        slot.UserId = null;
                        slot.StartedAt = null;
                        slot.IsReserved = false;
                        slot.ReservedUntil = null;
                        await db.SaveChangesAsync();

                        await auditLog.LogAsync("SlotReleased", "WorkerService", $"UserId={oldUser?.Id}, Slot={slot.Id}", "Done");
                    }
                }
            };

            await _channel.BasicConsumeAsync(queue: _queueName, autoAck: false, consumer: consumer);
            return Task.CompletedTask;
        }
    }
}