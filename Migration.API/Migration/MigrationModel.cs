using Migration.Infrastructure.models;

namespace Migration.API.Migration
{
    public class MigrationModel
    {
        public class MigrationRequest
        {
            public OldUser OldUser { get; set; } = new();
            public int slotId { get; set; }

        }

        public class MigrationSlotReservation
        {
            public OldUser OldUser { get; set; } = new();

        }

        public class AdministrativeMigrationRequest
        {
            public OldUser OldUser { get; set; } = new();

        }
    }
}
