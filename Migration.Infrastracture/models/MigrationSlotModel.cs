using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Models
{
  

    public class MigrationSlot
    {
        public int Id { get; set; }
        public bool IsOccupied { get; set; } = false;
        public string? UserId { get; set; }
        public DateTime? StartedAt { get; set; }
    }

}
