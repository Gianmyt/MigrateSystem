using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Migration.Infrastructure.models
{
    public class MqModel
    {
        public OldUser? OldUser { get; set; }
        public int? SlotId { get; set; } 
        public bool Forced { get; set; } = false;

    }

    
}
