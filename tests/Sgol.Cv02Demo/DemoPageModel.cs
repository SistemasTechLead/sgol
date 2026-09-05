using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Sgol.Cv02Demo;

internal abstract class DemoPageModel(
    string pageKey,
    string pageTitle,
    ScenarioCoordinator coordinator,
    DemoState state) : PageModel
{
    public string PageKey { get; } = pageKey;
    public string PageTitle { get; } = pageTitle;
    public IReadOnlyList<DemoScenario> Scenarios => ScenarioCatalog.ForPage(PageKey);
    public DemoState State { get; } = state;

    public void OnGet()
    {
        _ = PageKey;
    }

    public async Task<IActionResult> OnPostRunAsync(string scenarioId, CancellationToken cancellationToken)
    {
        if (!Scenarios.Any(item => string.Equals(item.Id, scenarioId, StringComparison.Ordinal)))
        {
            return NotFound();
        }

        await coordinator.RunAsync(scenarioId, cancellationToken);
        return Page();
    }
}
