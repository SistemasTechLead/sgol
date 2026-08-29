using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Sgol.ArchitectureTests;

internal static partial class ArchitectureRules
{
    private const string DomainIsolationRule = "ARCH-001";
    private const string HostCompositionRule = "ARCH-002";
    private const string ModuleImplementationRule = "ARCH-003";
    private const string BuildingBlocksRule = "ARCH-004";

    private static readonly (string Token, string Dependency)[] ForbiddenDomainDependencies =
    [
        ("Microsoft.AspNetCore", "ASP.NET Core"),
        ("Microsoft.EntityFrameworkCore", "EF Core"),
        ("Amazon.S3", "S3"),
        ("AWSSDK.S3", "S3"),
        ("Minio", "S3"),
        ("Azure.Storage.Blobs", "S3-compatible storage provider"),
        ("Azure.ResourceManager", "deployment provider"),
        ("Amazon.CloudFormation", "deployment provider"),
        ("Google.Cloud.Deploy", "deployment provider"),
        ("KubernetesClient", "deployment provider"),
        ("Pulumi", "deployment provider")
    ];

    internal static ArchitectureSnapshot LoadRepository(string repositoryRoot)
    {
        var sourceRoot = Path.Combine(repositoryRoot, "src");
        var projects = Directory
            .EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path))
            .Select(path => LoadProject(repositoryRoot, path))
            .ToArray();

        return new ArchitectureSnapshot(projects);
    }

    internal static IReadOnlyList<ArchitectureViolation> Analyze(ArchitectureSnapshot snapshot)
    {
        var violations = new List<ArchitectureViolation>();

        foreach (var project in snapshot.Projects)
        {
            AnalyzeDomainSources(project, violations);
            AnalyzeCompositionHost(project, violations);
            AnalyzeModuleInternals(project, violations);
            AnalyzeBuildingBlocks(project, violations);
        }

        return violations;
    }

    private static ArchitectureProject LoadProject(string repositoryRoot, string projectPath)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidOperationException($"Project path has no directory: {projectPath}");
        var projectDocument = XDocument.Load(projectPath);
        var projectReferences = projectDocument
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!)
            .Select(include => Path.GetFullPath(Path.Combine(projectDirectory, include)))
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .ToArray();
        var sources = Directory
            .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path))
            .Select(path => new ArchitectureSource(
                Normalize(Path.GetRelativePath(repositoryRoot, path)),
                File.ReadAllText(path)))
            .ToArray();

        return new ArchitectureProject(
            Path.GetFileNameWithoutExtension(projectPath),
            Normalize(Path.GetRelativePath(repositoryRoot, projectPath)),
            projectReferences,
            sources);
    }

    private static void AnalyzeDomainSources(
        ArchitectureProject project,
        List<ArchitectureViolation> violations)
    {
        foreach (var source in project.Sources.Where(IsDomainSource))
        {
            foreach (var (token, dependency) in ForbiddenDomainDependencies)
            {
                if (!source.Content.Contains(token, StringComparison.Ordinal))
                {
                    continue;
                }

                violations.Add(new ArchitectureViolation(
                    DomainIsolationRule,
                    $"Domain isolation: '{source.RelativePath}' references forbidden {dependency} token '{token}'. " +
                    "Domain must remain independent of ASP.NET Core, EF Core, S3 and deployment providers."));
            }
        }
    }

    private static void AnalyzeCompositionHost(
        ArchitectureProject project,
        List<ArchitectureViolation> violations)
    {
        if (project.Name is not ("Sgol.Web" or "Sgol.Worker"))
        {
            return;
        }

        var hostName = project.Name["Sgol.".Length..];
        foreach (var source in project.Sources)
        {
            var containsBusinessCode =
                HasPathSegment(source.RelativePath, "Domain") ||
                HasPathSegment(source.RelativePath, "Features") ||
                HostBusinessNamespaceRegex().IsMatch(source.Content);
            var internalModuleDependency = InternalModuleNamespaceRegex()
                .Matches(source.Content)
                .Cast<Match>()
                .FirstOrDefault(match =>
                    !string.Equals(match.Groups["module"].Value, hostName, StringComparison.Ordinal));

            if (containsBusinessCode)
            {
                violations.Add(new ArchitectureViolation(
                    HostCompositionRule,
                    $"Composition host: '{source.RelativePath}' places Domain/Features code in {project.Name}. " +
                    "Web and Worker may compose modules but must not contain business rules."));
            }

            if (internalModuleDependency is not null)
            {
                violations.Add(new ArchitectureViolation(
                    HostCompositionRule,
                    $"Composition host: '{source.RelativePath}' references internal module namespace " +
                    $"'{internalModuleDependency.Value}'. Web and Worker must compose through explicit contracts."));
            }
        }
    }

    private static void AnalyzeModuleInternals(
        ArchitectureProject project,
        List<ArchitectureViolation> violations)
    {
        var moduleMatch = ModuleProjectPathRegex().Match(project.RelativePath);
        if (!moduleMatch.Success)
        {
            return;
        }

        var owner = moduleMatch.Groups["module"].Value;
        foreach (var source in project.Sources)
        {
            foreach (Match dependencyMatch in InternalModuleNamespaceRegex().Matches(source.Content))
            {
                var dependency = dependencyMatch.Groups["module"].Value;
                if (string.Equals(owner, dependency, StringComparison.Ordinal))
                {
                    continue;
                }

                violations.Add(new ArchitectureViolation(
                    ModuleImplementationRule,
                    $"Module boundary: '{source.RelativePath}' in module '{owner}' references internal " +
                    $"implementation namespace '{dependencyMatch.Value}' of module '{dependency}'. " +
                    "Cross-module collaboration must use an explicit contract."));
            }
        }
    }

    private static void AnalyzeBuildingBlocks(
        ArchitectureProject project,
        List<ArchitectureViolation> violations)
    {
        if (!string.Equals(project.Name, "Sgol.BuildingBlocks", StringComparison.Ordinal))
        {
            return;
        }

        foreach (var reference in project.ProjectReferences.Where(IsForbiddenBuildingBlocksReference))
        {
            violations.Add(new ArchitectureViolation(
                BuildingBlocksRule,
                $"BuildingBlocks direction: '{project.RelativePath}' references forbidden project '{reference}'. " +
                "Sgol.BuildingBlocks must not depend on Web, Worker or functional modules."));
        }

        foreach (var source in project.Sources.Where(source =>
                     ForbiddenBuildingBlocksNamespaceRegex().IsMatch(source.Content)))
        {
            violations.Add(new ArchitectureViolation(
                BuildingBlocksRule,
                $"BuildingBlocks direction: '{source.RelativePath}' references Web, Worker or a functional module. " +
                "Dependencies must point from composition/modules toward Sgol.BuildingBlocks."));
        }
    }

    private static bool IsDomainSource(ArchitectureSource source) =>
        HasPathSegment(source.RelativePath, "Domain") || DomainNamespaceRegex().IsMatch(source.Content);

    private static bool IsForbiddenBuildingBlocksReference(string reference) =>
        reference.StartsWith("Sgol.", StringComparison.Ordinal);

    private static bool HasPathSegment(string path, string segment) =>
        path.Split('/').Contains(segment, StringComparer.Ordinal);

    private static bool IsGeneratedPath(string path)
    {
        var normalized = Normalize(path);
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    [GeneratedRegex(@"\bnamespace\s+[A-Za-z0-9_.]+\.Domain(?:[.;\s])", RegexOptions.CultureInvariant)]
    private static partial Regex DomainNamespaceRegex();

    [GeneratedRegex(@"\bnamespace\s+Sgol\.(?:Web|Worker)\.(?:Domain|Features)(?:[.;\s])", RegexOptions.CultureInvariant)]
    private static partial Regex HostBusinessNamespaceRegex();

    [GeneratedRegex(@"^src/Modules/(?<module>[^/]+)/", RegexOptions.CultureInvariant)]
    private static partial Regex ModuleProjectPathRegex();

    [GeneratedRegex(@"\bSgol\.(?<module>[A-Za-z0-9_]+)\.(?:Domain|Features|Infrastructure|Endpoints)\b", RegexOptions.CultureInvariant)]
    private static partial Regex InternalModuleNamespaceRegex();

    [GeneratedRegex(@"\bSgol\.(?:Web|Worker|Modules\.[A-Za-z0-9_]+)\b", RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenBuildingBlocksNamespaceRegex();
}

internal sealed record ArchitectureSnapshot(IReadOnlyCollection<ArchitectureProject> Projects);

internal sealed record ArchitectureProject(
    string Name,
    string RelativePath,
    IReadOnlyCollection<string> ProjectReferences,
    IReadOnlyCollection<ArchitectureSource> Sources);

internal sealed record ArchitectureSource(string RelativePath, string Content);

internal sealed record ArchitectureViolation(string RuleId, string Message)
{
    public override string ToString() => $"[{RuleId}] {Message}";
}
