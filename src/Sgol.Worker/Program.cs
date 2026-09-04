namespace Sgol.Worker;

public static class WorkerProgram
{
    public static Task<int> Main(string[] args) => WorkerApplication.RunAsync(args);
}
