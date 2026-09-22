namespace Sgol.Cv05Demo;

internal sealed class Cv05Image(string repositoryRoot, string commit)
{
    private readonly string tag = $"sgol-cv05:{Guid.CreateVersion7():N}";
    private bool built;
    public string ImageId { get; private set; } = null!;

    public async Task BuildAsync(CancellationToken token)
    {
        if (commit.Length != 40 || commit.Any(character => !Uri.IsHexDigit(character)))
            throw new DemoFailureException("PREFLIGHT", "NONE", "CV05_PRECONDITION_FAILED");
        var trustedImage = Environment.GetEnvironmentVariable("CV05_CI_IMAGE_ID");
        if (trustedImage is not null)
        {
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" ||
                Environment.GetEnvironmentVariable("IMPLEMENTATION_SHA") != commit ||
                !trustedImage.StartsWith("sha256:", StringComparison.Ordinal) || trustedImage.Length != 71)
                throw new DemoFailureException("OCI", "NONE", "CV05_PRECONDITION_FAILED");
            var inspected = await NativeProcess.RunAsync("docker", ["image", "inspect", "--format",
                "{{.Id}}|{{index .Config.Labels \"org.opencontainers.image.revision\"}}|{{index .Config.Labels \"com.sgol.source.dirty\"}}",
                $"sgol:{commit}"], repositoryRoot, TimeSpan.FromSeconds(30), token);
            if (inspected.Exit != 0 || inspected.Stdout.Trim() != $"{trustedImage}|{commit}|false")
                throw new DemoFailureException("OCI", "NONE", "CV05_PRECONDITION_FAILED");
            ImageId = trustedImage;
            return;
        }
        var status = await NativeProcess.RunAsync("git", ["status", "--porcelain"], repositoryRoot,
            TimeSpan.FromSeconds(30), token);
        if (status.Exit != 0)
            throw new DemoFailureException("OCI", "NONE", "CV05_PRECONDITION_FAILED");
        var dirty = status.Stdout.Length > 0 ? "true" : "false";
        var date = await NativeProcess.RunAsync("git", ["show", "-s", "--format=%cI", commit],
            repositoryRoot, TimeSpan.FromSeconds(30), token);
        if (date.Exit != 0 || !DateTimeOffset.TryParse(date.Stdout.Trim(), out _))
            throw new DemoFailureException("OCI", "NONE", "CV05_PRECONDITION_FAILED");
        var build = await NativeProcess.RunAsync("docker",
            ["build", "--platform", "linux/amd64", "--build-arg", $"VCS_REF={commit}",
                "--build-arg", $"IMAGE_VERSION=cv05-{commit[..12]}",
                "--build-arg", $"BUILD_CREATED={date.Stdout.Trim()}",
                "--build-arg", $"SOURCE_DIRTY={dirty}", "--tag", tag, "."],
            repositoryRoot, TimeSpan.FromMinutes(15), token);
        if (build.Exit != 0)
            throw new DemoFailureException("OCI", "NONE", "CV05_PRECONDITION_FAILED", build.Exit);
        built = true;
        var inspect = await NativeProcess.RunAsync("docker", ["image", "inspect", "--format", "{{.Id}}", tag],
            repositoryRoot, TimeSpan.FromSeconds(30), token);
        if (inspect.Exit != 0 || !inspect.Stdout.Trim().StartsWith("sha256:", StringComparison.Ordinal) ||
            inspect.Stdout.Trim().Length != 71)
            throw new DemoFailureException("OCI", "NONE", "CV05_PRECONDITION_FAILED");
        ImageId = inspect.Stdout.Trim();
    }

    public async Task<bool> CleanupAsync()
    {
        if (!built) return true;
        var removed = await NativeProcess.RunAsync("docker", ["image", "rm", tag], repositoryRoot,
            TimeSpan.FromSeconds(30), CancellationToken.None);
        if (removed.Exit != 0) return false;
        var remains = await NativeProcess.RunAsync("docker", ["image", "inspect", tag], repositoryRoot,
            TimeSpan.FromSeconds(30), CancellationToken.None);
        return remains.Exit != 0;
    }
}
