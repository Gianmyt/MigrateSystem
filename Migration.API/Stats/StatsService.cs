using Infrastructure;
using Microsoft.EntityFrameworkCore;
using static Migration.API.Stats.StasModel;

namespace Migration.API.Stats
{
    public interface IStatsService
    {
        Task<InProgressResultDto> GetInProgressasync();
    }
    public class StatsService : IStatsService
    {
        private readonly MigrationDbContext _db;
        public StatsService(MigrationDbContext db)
        {
            _db = db;
        }

        public async Task<InProgressResultDto> GetInProgressasync()
        {
            var migrations = await _db.UserMigrations
        .Where(u => u.Status == "InProgress")
        .ToListAsync();

            var slots = await _db.MigrationSlots
                .Where(s => s.IsOccupied)
                .ToListAsync();

            return new InProgressResultDto
            {
                ActiveMigrations = migrations,
                OccupiedSlots = slots
            };
        }
    }
}
