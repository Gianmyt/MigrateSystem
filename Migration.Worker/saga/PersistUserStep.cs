using Microsoft.EntityFrameworkCore;
using Migration.Infrastructure.models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Migration.Worker.saga
{
    public class PersistUserStep : IMigrationSagaStep
    {
        public string Name => "PersistUser";

        public async Task ExecuteAsync(MigrationContext context)
        {
            var db = context.Db;

            var alreadyMigrated = await db.UserMigrations
                .AnyAsync(u => u.UserId == context.OldUser.Id.ToString() && u.IsMigrated);

            if (alreadyMigrated)
                throw new InvalidOperationException("User already migrated");

            db.UserMigrations.Add(new UserMigration
            {
                UserId = context.OldUser.Id.ToString(),
                IsMigrated = true,
                MigrationDate = DateTime.UtcNow,
                Status = "Success"
            });

            await db.SaveChangesAsync();
            await context.Audit.LogAsync(context.OldUser.Id.ToString(), "WorkerService : UserPersisted", "UserMigration entry created", success:true);
        }

        public async Task CompensateAsync(MigrationContext context)
        {
            var db = context.Db;
            var record = await db.UserMigrations
                .FirstOrDefaultAsync(u => u.UserId == context.OldUser.Id.ToString());

            if (record != null)
            {
                db.UserMigrations.Remove(record);
                await db.SaveChangesAsync();
                await context.Audit.LogAsync(context.OldUser.Id.ToString(), "WorkerService : UserRollback", "UserMigration entry deleted", success:false);
            }
        }
    }
}
