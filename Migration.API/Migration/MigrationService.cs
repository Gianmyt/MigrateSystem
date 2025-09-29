using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Migration.API.Services;
using Migration.Infrastructure.models;
using Migration.Infrastructure.services;

namespace Migration.API.Migration
{
    public interface IMigrationService
    {
        Task<UserMigration?> GetUserMigrationAsync(OldUser oldUser);
    }
    public class MigrationService : IMigrationService
    {
        private readonly MigrationDbContext _db;
        public MigrationService(MigrationDbContext db) 
        {
            _db = db;
        }

       

        public async Task<UserMigration> GetUserMigrationAsync(OldUser oldUser)
        {
            return await _db.UserMigrations.FirstOrDefaultAsync(x => x.Id == oldUser.Id);
        }
    }
}
