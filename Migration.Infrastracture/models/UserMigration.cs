using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Migration.Infrastructure.models
{
    public class UserMigration
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public bool IsMigrated { get; set; }
        public DateTime? MigrationDate { get; set; }
        public string? Status { get; set; }
    }
}
