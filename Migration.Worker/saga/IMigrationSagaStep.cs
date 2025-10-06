using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Migration.Worker.saga
{
    public interface IMigrationSagaStep
    {
        string Name { get; }

        Task ExecuteAsync(MigrationContext context);
        Task CompensateAsync(MigrationContext context);
    }

}
