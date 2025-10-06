using Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Migration.Worker.saga
{
    public class ReserveSlotStep : IMigrationSagaStep
    {
        private readonly int? _slotId;
        private MigrationSlot? _slot;

        public string Name => "Reservationslot";

        public ReserveSlotStep(int? slotId)
        {
            _slotId = slotId;
        }

        public async Task ExecuteAsync(MigrationContext context)
        {
            var db = context.Db;
            var userId = context.OldUser.Id.ToString();

            await using var tx = await db.Database.BeginTransactionAsync();

            _slot = await db.MigrationSlots
                .FromSqlRaw("SELECT TOP(1) * FROM MigrationSlots WITH (UPDLOCK, ROWLOCK, READPAST) WHERE Id = {0}", _slotId)
                .FirstOrDefaultAsync();

            if (_slot == null)
            {
                await context.Audit.LogAsync(userId, "SlotUnavailable", "ReserveSlotStep",
                    $"SlotId={_slotId ?? -1} non trovato o occupato", "Failed", false);
                throw new InvalidOperationException("Nessuno slot disponibile o slot non valido");
            }

            _slot.IsOccupied = true;
            _slot.UserId = userId;
            _slot.StartedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            await context.Audit.LogAsync(userId, "SlotReserved", "ReserveSlotStep",
                $"Slot={_slot.Id} riservato per User={userId}", "Success");
        }

        public async Task CompensateAsync(MigrationContext context)
        {
            if (_slot == null) return;
            var db = context.Db;

            _slot.IsOccupied = false;
            _slot.UserId = null;
            _slot.StartedAt = null;
            _slot.IsReserved = false;
            _slot.ReservedUntil = null;

            await db.SaveChangesAsync();
            await context.Audit.LogAsync(context.OldUser.Id.ToString(),
                "SlotReleased", "ReserveSlotStep",
                $"Slot={_slot.Id} rilasciato in compensazione", success:true);
        }
    }
}
