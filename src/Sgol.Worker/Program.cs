using Sgol.JobInfrastructure;
using Sgol.Operations;
using Microsoft.Extensions.Configuration;

namespace Sgol.Worker;

public static class WorkerProgram
{
    public static Task<int> Main(string[] args) => WorkerApplication.RunAsync(
        args,
        services =>
        {
            services.AddSgolPortableOperations();
        },
        configureHostServices: (services, configuration, environment) =>
        {
            if (configuration.GetValue<bool>("LegacyWorkflow:Enabled"))
            {
                services.AddSgolRecurringGeneration();
                services.AddSgolManualAssignment();
                services.AddSgolEvidenceWorkerInfrastructure(configuration, environment);
            }
        });
}
