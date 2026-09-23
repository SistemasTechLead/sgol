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

    [Fact]
    public void InterfaceStyles_UseOnlyVariablesDefinedByTheDesignContract()
    {
        var repositoryRoot = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var documentedVariables = ReadDocumentedVariables(Path.Combine(repositoryRoot, "docs", "design", "tokens.md"));
        var stylesPath = Path.Combine(repositoryRoot, "src", "Sgol.Web", "wwwroot", "css");
        var usedVariables = Directory
            .EnumerateFiles(stylesPath, "*.css", SearchOption.TopDirectoryOnly)
            .Where(path => !IsCssVariablesFile(repositoryRoot, path))
            .SelectMany(ReadUsedVariables)
            .ToHashSet(StringComparer.Ordinal);

        var undocumented = usedVariables.Except(documentedVariables, StringComparer.Ordinal).ToArray();

        Assert.True(
            undocumented.Length == 0,
            "Every CSS variable must be defined in docs/design/tokens.md: " +
            string.Join(", ", undocumented));
    }

    [Fact]
    public void VariablesStylesheet_DeclaresExactlyTheDocumentedVariables()
    {
        var repositoryRoot = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var documentedVariables = ReadDocumentedVariables(Path.Combine(repositoryRoot, "docs", "design", "tokens.md"));
        var implementedVariables = ReadDocumentedVariables(Path.Combine(repositoryRoot, CssVariablesFile));

        Assert.Equal(
            documentedVariables.Order(StringComparer.Ordinal),
            implementedVariables.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void SharedLayout_ProvidesCriticalKeyboardAndLanguageLandmarks()
    {
        var repositoryRoot = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var layout = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Sgol.Web",
            "Pages",
            "Shared",
            "_Layout.cshtml"));

        Assert.Contains("lang=\"es-MX\"", layout, StringComparison.Ordinal);
        Assert.Contains("href=\"#contenido-principal\"", layout, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Navegación principal\"", layout, StringComparison.Ordinal);
        Assert.Contains("<main id=\"contenido-principal\"", layout, StringComparison.Ordinal);
        Assert.Contains("~/js/components.js", layout, StringComparison.Ordinal);
        Assert.Contains("data-dialog-open", layout, StringComparison.Ordinal);
        Assert.Contains("<dialog id=\"navegacion-movil\"", layout, StringComparison.Ordinal);
        Assert.Contains("method=\"dialog\"", layout, StringComparison.Ordinal);
        Assert.Contains("Aún no hay secciones disponibles", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("IsInRole", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontendSessionAndNavigationDoNotUseRoleClaimsOrPersistIdentity()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var navigation = File.ReadAllText(Path.Combine(root, "src", "Sgol.Web", "Interface", "Navigation", "NavigationItem.cs"));
        var session = File.ReadAllText(Path.Combine(root, "src", "Sgol.Web", "Interface", "Navigation", "RazorSessionState.cs"));
        var layout = File.ReadAllText(Path.Combine(root, "src", "Sgol.Web", "Pages", "Shared", "_Layout.cshtml"));
        Assert.DoesNotContain("IsInRole", navigation, StringComparison.Ordinal);
        Assert.Contains("session.RoleCode", navigation, StringComparison.Ordinal);
        Assert.Contains("session.Permissions", navigation, StringComparison.Ordinal);
        Assert.Contains("/api/v1/auth/session", session, StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage", layout + session, StringComparison.Ordinal);
        Assert.DoesNotContain("/mi-trabajo\"", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedComponents_ImplementTheApprovedTechFront001Contracts()
    {
        var repositoryRoot = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var sharedPath = Path.Combine(repositoryRoot, "src", "Sgol.Web", "Pages", "Shared");
        var requiredContracts = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["_CredentialField.cshtml"] = ["autocomplete=", "inputmode=", "data-credential-toggle"],
            ["_TextArea.cshtml"] = ["<textarea", "aria-describedby="],
            ["_RadioGroup.cshtml"] = ["<fieldset", "<legend", "type=\"radio\""],
            ["_LocalDateField.cshtml"] = ["datetime-local", "Zona operativa:"],
            ["_MotivatedConfirmation.cshtml"] = ["<dialog", "data-dialog-cancel", "autofocus"],
            ["_UploadPresentation.cshtml"] = ["<progress", "aria-live=\"polite\"", "data-upload-cancel"],
            ["_ValidationSummary.cshtml"] = ["role=\"alert\"", "data-validation-summary"],
            ["_SuccessAlert.cshtml"] = ["role=\"status\"", "aria-live=\"polite\""],
        };

        foreach (var (file, fragments) in requiredContracts)
        {
            var content = File.ReadAllText(Path.Combine(sharedPath, file));
            foreach (var fragment in fragments)
            {
                Assert.Contains(fragment, content, StringComparison.Ordinal);
            }
        }

        var models = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Sgol.Web",
            "Presentation",
            "Components",
            "ComponentModels.cs"));
        Assert.Contains("America/Mexico_City", models, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedComponentScript_DoesNotPersistCredentialsOrInspectCookies()
    {
        var repositoryRoot = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var script = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Sgol.Web",
            "wwwroot",
            "js",
            "components.js"));

        Assert.DoesNotContain("localStorage", script, StringComparison.Ordinal);
        Assert.DoesNotContain("sessionStorage", script, StringComparison.Ordinal);
        Assert.DoesNotContain("document.cookie", script, StringComparison.Ordinal);
        Assert.Contains("showModal()", script, StringComparison.Ordinal);
        Assert.Contains(".focus()", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AccessibilityContract_UsesWcag22AaAndResponsiveComponents()
    {
        var repositoryRoot = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var accessibility = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "design", "accesibilidad.md"));
        var components = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Sgol.Web", "wwwroot", "css", "components.css"));

        Assert.Contains("WCAG 2.2", accessibility, StringComparison.Ordinal);
        Assert.Contains("nivel AA", accessibility, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 48rem)", components, StringComparison.Ordinal);
        Assert.Contains("overflow-x: auto", components, StringComparison.Ordinal);
    }

    [Fact]
    public void OperationalColorPairs_MeetDocumentedWcag22ContrastThresholds()
    {
        var repositoryRoot = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var variables = HexVariableRegex()
            .Matches(File.ReadAllText(Path.Combine(repositoryRoot, CssVariablesFile)))
            .ToDictionary(
                match => match.Groups[1].Value,
                match => match.Groups[2].Value,
                StringComparer.Ordinal);
        var pairs = new (string Foreground, string Background, double Minimum)[]
        {
            ("--color-texto-primario", "--color-superficie", 4.5),
            ("--color-texto-primario", "--color-superficie-elevada", 4.5),
            ("--color-texto-secundario", "--color-superficie", 4.5),
            ("--color-texto-secundario", "--color-superficie-elevada", 4.5),
            ("--color-acento", "--color-superficie", 4.5),
            ("--color-acento-hover", "--color-superficie", 4.5),
            ("--color-acento-texto", "--color-acento", 4.5),
            ("--color-acento-texto", "--color-acento-hover", 4.5),
            ("--color-borde-fuerte", "--color-superficie", 3.0),
            ("--color-exito", "--color-exito-fondo", 4.5),
            ("--color-advertencia", "--color-advertencia-fondo", 4.5),
            ("--color-peligro", "--color-peligro-fondo", 4.5),
            ("--color-info", "--color-info-fondo", 4.5),
        };

        foreach (var pair in pairs)
        {
            var ratio = ContrastRatio(variables[pair.Foreground], variables[pair.Background]);
            Assert.True(
                ratio >= pair.Minimum,
                $"{pair.Foreground}/{pair.Background} has contrast {ratio:F2}:1; expected at least {pair.Minimum:F1}:1.");
        }
    }

    private static double ContrastRatio(string foreground, string background)
    {
        var first = RelativeLuminance(foreground);
        var second = RelativeLuminance(background);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        var channels = Enumerable.Range(0, 3)
            .Select(index => Convert.ToInt32(hex.Substring(index * 2, 2), 16) / 255d)
            .Select(channel => channel <= 0.04045
                ? channel / 12.92
                : Math.Pow((channel + 0.055) / 1.055, 2.4))
            .ToArray();
        return (0.2126 * channels[0]) + (0.7152 * channels[1]) + (0.0722 * channels[2]);
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

    private static HashSet<string> ReadUsedVariables(string path) =>
        CssVariableRegex()
            .Matches(File.ReadAllText(path))
            .Select(match => match.Value)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> ReadDocumentedVariables(string path) =>
        CssVariableDeclarationRegex()
            .Matches(File.ReadAllText(path))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static string Normalize(string path) => path.Replace('\\', '/');

    [GeneratedRegex(
        @"(?<![A-Za-z0-9_-])#(?:[0-9a-f]{3,4}|[0-9a-f]{6}|[0-9a-f]{8})(?![0-9a-f])|(?<![A-Za-z0-9_-])(?:rgb|rgba|hsl)\s*\(",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LiteralColorRegex();

    [GeneratedRegex(@"(?<=var\()--[a-z0-9-]+", RegexOptions.CultureInvariant)]
    private static partial Regex CssVariableRegex();

    [GeneratedRegex(@"^\s*(--[a-z0-9-]+)\s*:\s*#([0-9a-f]{6})\s*;", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HexVariableRegex();

    [GeneratedRegex(@"^\s*(--[a-z0-9-]+)\s*:", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex CssVariableDeclarationRegex();
}
