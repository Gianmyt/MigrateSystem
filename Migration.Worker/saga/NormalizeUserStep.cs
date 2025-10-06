using Microsoft.Extensions.DependencyInjection;
using Migration.Infrastructure.models;
using Migration.Infrastructure.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Migration.Worker.saga
{
    public class NormalizeUserStep : IMigrationSagaStep
    {
        public string Name => "NormalizeUser";

        public async Task ExecuteAsync(MigrationContext context)
        {
            var normalizer = context.Scope.ServiceProvider.GetRequiredService<INormalizer<OldUser, NewUser>>();

            context.NewUser = normalizer.Normalize(context.OldUser);
            await context.Audit.LogAsync(context.OldUser.Id.ToString(), "UserNormalized", $"Email={context.NewUser.Email}", "Success");
        }

        public Task CompensateAsync(MigrationContext context)
        {
            // niente da fare, non persistiamo nulla
            return Task.CompletedTask;
        }
    }
}
