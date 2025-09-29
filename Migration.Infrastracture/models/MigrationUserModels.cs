using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Migration.Infrastructure.models
{
    public class OldUser
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty; // "Mario Rossi"
        public string Mail { get; set; } = string.Empty;     // " mario.rossi@email.it "
        public string? Phone { get; set; }                   // "0039-3331234567"
    }

    // Nuovo modello (NEW)
    public class NewUser
    {
        public Guid UserId { get; set; }                     // nuovo formato chiave
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public DateTime MigratedAt { get; set; }
    }

    public class MigrationRequest
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int Priority { get; set; } = 0;
        public string Status { get; set; } = "Pending"; // Pending/InProgress/Completed/Failed
        public int Attempts { get; set; } = 0;
        public string? LastError { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
