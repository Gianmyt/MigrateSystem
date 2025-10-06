using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Migration.Worker.saga
{
    public class MigrationSagaCoordinator
    {
        private readonly List<IMigrationSagaStep> _steps = new();

        public MigrationSagaCoordinator AddStep(IMigrationSagaStep step)
        {
            _steps.Add(step);
            return this;
        }

        public async Task ExecuteAsync(MigrationContext context)
        {
            var executedSteps = new Stack<IMigrationSagaStep>();

            foreach (var step in _steps)
            {
                try
                {
                    await step.ExecuteAsync(context);
                    executedSteps.Push(step);
                }
                catch (Exception ex)
                {
                    await context.Audit.LogAsync(context.OldUser.Id.ToString(),
                        $"SagaFailed:{step.Name}", ex.Message, "Failed");

                    // compensazioni in ordine inverso
                    while (executedSteps.Count > 0)
                    {
                        var s = executedSteps.Pop();
                        await s.CompensateAsync(context);
                    }

                    throw;
                }
            }
        }
    }
}
