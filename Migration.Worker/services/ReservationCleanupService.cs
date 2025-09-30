using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Migration.Worker.services
{
    public class ReservationCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(1); // esegue ogni minuto

        public ReservationCleanupService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<MigrationDbContext>();

                    var expiredReservations = await db.MigrationSlots
                        .Where(s => s.IsReserved && s.ReservedUntil < DateTime.UtcNow)
                        .ToListAsync();

                    if (expiredReservations.Any())
                    {
                        foreach (var slot in expiredReservations)
                        {
                            slot.IsReserved = false;
                            slot.ReservedUntil = null;
                            slot.UserId = null; // opzionale: resetti anche il legame utente
                        }

                        await db.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    // loggare eventuali errori
                    Console.WriteLine($"[ReservationCleanup] Errore: {ex.Message}");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
    }
}
