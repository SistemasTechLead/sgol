using System.Text.RegularExpressions;
using Xunit;

namespace Sgol.ArchitectureTests;

public sealed partial class InterfaceDesignRulesTests
{
    private const string CssVariablesFile = "src/Sgol.Web/wwwroot/css/tokens.css";

    private static readonly string[] ProjectDirectories = ["src", "tests"];
    private static readonly string[] InterfaceExtensions = [".css", ".razor", ".cshtml"];

    [Fact]
    public void InterfaceFiles_DoNotContainLiteralColorsOutsideVariablesFile()
    {
        var repositoryRoot = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var violations = ProjectDirectories
            .Select(directory => Path.Combine(repositoryRoot, directory))
            .Where(Directory.Exists)
            .SelectMany(EnumerateInterfaceFiles)
            .Where(path => !IsCssVariablesFile(repositoryRoot, path))
            .SelectMany(path => FindLiteralColors(repositoryRoot, path))
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Literal color values are forbidden outside the CSS variables file " +
            $"'{CssVariablesFile}':{Environment.NewLine}" +
            string.Join(Environment.NewLine, violations));
    }

    private static IEnumerable<string> EnumerateInterfaceFiles(string root)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(root);

        while (pendingDirectories.TryPop(out var directory))
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if (InterfaceExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                {
                    yield return file;
                }
            }

            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                var name = Path.GetFileName(child);
                if (!name.Equals("bin", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("obj", StringComparison.OrdinalIgnoreCase))
                {
                    pendingDirectories.Push(child);
                }
            }
        }
    }

    private static bool IsCssVariablesFile(string repositoryRoot, string path) =>
        string.Equals(
            Normalize(Path.GetRelativePath(repositoryRoot, path)),
            CssVariablesFile,
            StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> FindLiteralColors(string repositoryRoot, string path)
    {
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            foreach (Match match in LiteralColorRegex().Matches(line))
            {
                yield return $"{Normalize(Path.GetRelativePath(repositoryRoot, path))}:{lineNumber}: {match.Value}";
            }
        }
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    [GeneratedRegex(
        @"(?<![A-Za-z0-9_-])#(?:[0-9a-f]{3,4}|[0-9a-f]{6}|[0-9a-f]{8})(?![0-9a-f])|(?<![A-Za-z0-9_-])(?:rgb|rgba|hsl)\s*\(",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LiteralColorRegex();
}
