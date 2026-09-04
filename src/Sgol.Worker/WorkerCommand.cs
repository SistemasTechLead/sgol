using System.Globalization;
using System.Text.RegularExpressions;

namespace Sgol.Worker;

public enum WorkerCommandKind
{
    Help,
    Outbox,
    RunJob
}

public sealed record WorkerCommand(
    WorkerCommandKind Kind,
    string? JobName = null,
    DateTimeOffset? ScheduledFor = null);

public static partial class WorkerCommandParser
{
    public static bool TryParse(string[] args, out WorkerCommand? command)
    {
        ArgumentNullException.ThrowIfNull(args);
        command = null;

        if (args is ["--help"])
        {
            command = new WorkerCommand(WorkerCommandKind.Help);
            return true;
        }

        if (args is ["outbox"])
        {
            command = new WorkerCommand(WorkerCommandKind.Outbox);
            return true;
        }

        if (args is ["run-job", "--job", var jobName, "--scheduled-for", var scheduledFor] &&
            JobNameRegex().IsMatch(jobName) &&
            scheduledFor.EndsWith('Z') &&
            DateTimeOffset.TryParse(
                scheduledFor,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var instant))
        {
            command = new WorkerCommand(WorkerCommandKind.RunJob, jobName, instant);
            return true;
        }

        return false;
    }

    public static string HelpText =>
        "Usage:" + Environment.NewLine +
        "  Sgol.Worker outbox" + Environment.NewLine +
        "  Sgol.Worker run-job --job <jobName> --scheduled-for <UTC-RFC3339-Z>" + Environment.NewLine +
        "  Sgol.Worker --help";

    [GeneratedRegex("^[A-Z][A-Z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex JobNameRegex();
}
