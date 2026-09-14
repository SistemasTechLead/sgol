using Sgol.JobInfrastructure;
using Sgol.Operations;

namespace Sgol.Worker;

public static class WorkerProgram
{
    public static Task<int> Main(string[] args) => WorkerApplication.RunAsync(
        args,
        services =>
        {
            services.AddSgolRecurringGeneration();
            services.AddSgolPortableOperations();
        },
        configureHostServices: (services, configuration, environment) =>
            services.AddSgolEvidenceWorkerInfrastructure(configuration, environment));
}
