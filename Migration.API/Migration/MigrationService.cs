using Infrastructure;
using Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Migration.Infrastructure.models;
using System.Transactions;

namespace Migration.API.Migration
{
    public interface IMigrationService
    {
        Task<UserMigration?> GetUserMigrationAsync(OldUser oldUser);
        Task<MigrationSlot?> GetSlotAsync(OldUser oldUser);
        Task<int> ReserveSlot(OldUser oldUser, MigrationSlot slot);
        Task<MigrationSlot?> TryReserveSlotAsync(OldUser oldUser);
    }
    public class MigrationService : IMigrationService
    {
        private readonly MigrationDbContext _db;
        public MigrationService(MigrationDbContext db)
        {
            _db = db;
        }

        public async Task<MigrationSlot?> TryReserveSlotAsync(OldUser oldUser)
        {
            // Transazione: basta ReadCommitted perché usiamo i lock espliciti
            await using var tx = await _db.Database.BeginTransactionAsync();

            // 1) Lock applicativo per-utente (impedisce due prenotazioni concorrenti dello stesso utente)
            var lockRes =  await _db.Database.ExecuteSqlRawAsync(
                "SELECT 1 FROM MigrationSlots WITH(TABLOCKX, HOLDLOCK)" );

            

            // 2) Se esiste già una prenotazione ATTIVA per questo utente → non farne un’altra
            var alreadyReserved = await _db.MigrationSlots.AnyAsync(
                s => s.UserId == oldUser.Id.ToString() && s.IsReserved && s.ReservedUntil > DateTime.UtcNow);

            if (alreadyReserved)
            {
                await tx.RollbackAsync();
                return null;
            }

            // 3) Claim di uno slot libero: UPDLOCK/ROWLOCK/READPAST evita il thundering herd
            var slot = await _db.MigrationSlots
                .FromSqlRaw(
                    "SELECT TOP(1) * " +
                    "FROM MigrationSlots WITH (UPDLOCK, ROWLOCK, READPAST) " +
                    "WHERE IsOccupied = 0 AND (IsReserved = 0 OR ReservedUntil < GETUTCDATE())")
                .FirstOrDefaultAsync();

            if (slot == null)
            {
                await tx.RollbackAsync();
                return null; // nessuno slot disponibile adesso
            }

            // 4) Scrivi la prenotazione
            slot.IsReserved = true;
            slot.ReservedUntil = DateTime.UtcNow.AddMinutes(2);
            slot.UserId = oldUser.Id.ToString(); // sovrascrive eventuale UserId “stale” di una prenotazione scaduta
            await _db.SaveChangesAsync();

            await tx.CommitAsync();
            return slot;
        }


        public async Task<MigrationSlot?> GetSlotAsync(OldUser oldUser)
        {

            var slot = await _db.MigrationSlots
    .FromSqlRaw("SELECT TOP(1) * FROM MigrationSlots WITH (UPDLOCK, ROWLOCK, READPAST) WHERE IsOccupied = 0 AND (IsReserved = 0 OR ReservedUntil < GETUTCDATE())")
    .FirstOrDefaultAsync();
            // Caso 1: l’utente ha già una prenotazione valida → niente
            var alreadyReserved = await _db.MigrationSlots
                .AnyAsync(s => s.UserId == oldUser.Id.ToString()
                            && s.IsReserved
                            && s.ReservedUntil > DateTime.UtcNow);

            if (alreadyReserved)
                return null;

            // Caso 2: cerca uno slot libero
            var freeSlot = await _db.MigrationSlots
                .FirstOrDefaultAsync(s => !s.IsOccupied && (!s.IsReserved || s.ReservedUntil < DateTime.UtcNow));

            return freeSlot; // se null → nessuno slot disponibile
        }
        public async Task<UserMigration> GetUserMigrationAsync(OldUser oldUser)
        {
            return await _db.UserMigrations.FirstOrDefaultAsync(x => x.Id == oldUser.Id);
        }

        public async Task<int> ReserveSlot(OldUser oldUser, MigrationSlot slot)
        {
            slot.IsReserved = true;
            slot.ReservedUntil = DateTime.UtcNow.AddMinutes(2);
            slot.UserId = oldUser.Id.ToString();
            return await _db.SaveChangesAsync();
        }
    }
}
