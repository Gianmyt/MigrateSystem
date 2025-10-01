using Migration.Infrastructure.models;

namespace Migration.API.Migration
{
    public class MigrationModel
    {
        public class MigrationRequestDto
        {
            public OldUser OldUser { get; set; } = new();
            public int slotId { get; set; }

        }

        public class MigrationSlotReservationDto
        {
            public OldUser OldUser { get; set; } = new();

        }

        public class AdministrativeMigrationRequestDto
        {
            public OldUser OldUser { get; set; } = new();

        }
    }
}
