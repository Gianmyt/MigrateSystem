using Infrastructure;
using Infrastructure.Models;

namespace Migration.API.Stats
{
    public class StasModel
    {
        public class InProgressResultDto
        {
            public List<UserMigration> ActiveMigrations { get; set; } = new();
            public List<MigrationSlot> OccupiedSlots { get; set; } = new();
        }
    }
}
