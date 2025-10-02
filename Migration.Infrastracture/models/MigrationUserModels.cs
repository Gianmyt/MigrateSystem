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
        public string FullName { get; set; } = string.Empty; 
        public string Mail { get; set; } = string.Empty;     
        public string? Phone { get; set; }                   
    }

    public class NewUser
    {
        public Guid UserId { get; set; }                     
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public DateTime MigratedAt { get; set; }
    }

    
}
