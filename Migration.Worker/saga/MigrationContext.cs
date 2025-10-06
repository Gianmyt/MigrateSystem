using Infrastructure;
using Infrastructure.Models;
using Microsoft.Extensions.DependencyInjection;
using Migration.Infrastructure.models;
using Migration.Infrastructure.services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Migration.Worker.saga
{
    public class MigrationContext
    {
        public OldUser OldUser { get; set; } = default!;
        public NewUser? NewUser { get; set; }
        public MigrationSlot? Slot { get; set; }
        public IServiceScope Scope { get; set; } = default!;
        public MigrationDbContext Db => Scope.ServiceProvider.GetRequiredService<MigrationDbContext>();
        public IAuditLogService Audit => Scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        public MigrationContext(OldUser oldUser, IServiceScope scope)
        {
            OldUser = oldUser ?? throw new ArgumentNullException(nameof(oldUser));
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }

    }
}
