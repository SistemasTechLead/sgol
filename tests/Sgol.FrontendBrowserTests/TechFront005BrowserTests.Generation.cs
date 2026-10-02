using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Sgol.Assignment.Contracts;
using Sgol.Generation.Contracts;
using Sgol.JobInfrastructure;
using Sgol.Web.Infrastructure.Persistence.Generation;
using Xunit;

namespace Sgol.FrontendBrowserTests;

public sealed partial class TechFront005BrowserTests
{
    private async Task GenerationAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSgolJobInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Sgol"] = owner.ConnectionString }).Build());
        services.AddSgolRecurringGeneration();
        await using var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var processor = scope.ServiceProvider.GetRequiredService<IRecurringOccurrenceProcessor>();
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
            var window = new RecurringWindow(day, new(12, 0), RecurringGenerationContract.ToUtc(day, new(12, 0), timeZone));
            var first = await processor.ProcessAsync(window, Guid.CreateVersion7());
            Assert.Equal("GENERADA", first!.Result);
            var second = await processor.ProcessAsync(window, Guid.CreateVersion7());
            Assert.Equal("RECUPERADA", second!.Result);
            Assert.Equal(first.ObligationId, second.ObligationId);
            obligations.Add("TAR-0005", first.ObligationId!.Value);
        }
        foreach (var code in new[] { "TAR-0007", "TAR-0008", "TAR-0011", "TAR-0018", "TAR-0092", "TAR-0093" })
        {
            await Direction.GotoAsync("/planificacion?" + Period + "&taskCode=" + code);
            Assert.DoesNotContain("TAR-0026", await Direction.Locator("#manual-task").InnerTextAsync());
            var fields = Direction.Locator("#manual-form input:not([type=hidden])");
            for (var index = 0; index < await fields.CountAsync(); index++)
            {
                var field = fields.Nth(index);
                var name = await field.GetAttributeAsync("name");
                var type = await field.GetAttributeAsync("type");
                await field.FillAsync(type == "datetime-local" ? day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
                    (name == "expiresAt" ? "T23:00:00" : "T00:01:00") : "SYN-" + code + "-" + index);
            }
            if (code == "TAR-0011") await Direction.Locator("#manual-solutionType").SelectOptionAsync("CAMBIO");
            if (code == "TAR-0093")
            {
                await Direction.Locator("#manual-description").FillAsync("Daño sintético de recepción");
                await Direction.Locator("#manual-incidentType").SelectOptionAsync("DANO");
                await Direction.Locator("#manual-parentObligationId").SelectOptionAsync(obligations["TAR-0092"].ToString("D"));
                await Direction.Locator("#manual-parentObligationId").PressAsync("Tab");
            }
            await Submit(Direction, Direction.GetByRole(AriaRole.Button, new() { Name = "Revisar solicitud" }), "PrepareManual");
            await Assertions.Expect(Direction.Locator("#manual-confirm")).ToBeVisibleAsync();
            await Submit(Direction, Direction.Locator("#manual-confirm button[type=submit]"), "CreateManual");
            await Assertions.Expect(Direction.Locator("#manual-result")).ToContainTextAsync("Solicitud aceptada");
            var request = await Direction.Locator("#manual-result-id").InnerTextAsync();
            var obligation = Guid.Parse(await Direction.Locator("#manual-obligation-id").InnerTextAsync());
            obligations.Add(code, obligation);
            await Submit(Direction, Direction.GetByRole(AriaRole.Button, new() { Name = "Recuperar resultado" }), "CreateManual");
            Assert.Equal(request, await Direction.Locator("#manual-result-id").InnerTextAsync());
            Assert.Equal(obligation.ToString("D"), await Direction.Locator("#manual-obligation-id").InnerTextAsync());
            await using var scope = provider.CreateAsyncScope();
            var evaluation = await scope.ServiceProvider.GetRequiredService<IEligibilityEvaluationService>().EvaluateAsync(
                new(Guid.CreateVersion7(), Guid.CreateVersion7(), obligation, day, EligibilityDateSources.ManualRequest));
            Assert.Equal(EligibilityResults.EligibleCandidates, evaluation.Result);
            var assigned = await scope.ServiceProvider.GetRequiredService<IAutomaticAssignmentService>().AssignAsync(
                new(Guid.CreateVersion7(), obligation, evaluation.EvaluationId, Guid.CreateVersion7()));
            Assert.NotNull(assigned.AssignmentId);
        }
        await using (var db = fixture.TechFront005Context())
        {
            var reserved = Sgol.Configuration.Contracts.TaskDefinitionCatalog.Require("TAR-0026").Id;
            var versions = db.TaskDefinitionVersions.Where(v => v.TaskDefinitionId == reserved).Select(v => v.Id);
            Assert.Equal(0, await db.WorkObligations.CountAsync(o => versions.Contains(o.TaskDefinitionVersionId)));
        }
        // UI correction restores the tested floor account even when the ranking chose the auxiliary.
        foreach (var target in new[] { auxiliaryPerson, fixture.Accounts[3].PersonId })
        {
            await Direction.GotoAsync("/planificacion?obligationId=" + obligations["TAR-0007"]);
            var option = Direction.Locator("#new-responsible option[value='" + target + "']");
            if (await option.CountAsync() == 0) continue; // Current holder is deliberately excluded by the real contract.
            await Direction.Locator("#new-responsible").SelectOptionAsync(target.ToString("D"));
            await Direction.Locator("#correction-reason").FillAsync("Corrección sintética integral motivada");
            await Submit(Direction, Direction.Locator("form[action*='PrepareCorrection'] button[type=submit]"), "PrepareCorrection");
            await Submit(Direction, Direction.Locator("#correction-confirm button[type=submit]"), "CorrectAssignment");
            await Assertions.Expect(Direction.Locator("#correction-result")).ToContainTextAsync("Asignación corregida");
        }
        // TAR-0018 can have the other eligible floor holder; bring it to the profile's authenticated executor through UI.
        await Direction.GotoAsync("/planificacion?obligationId=" + obligations["TAR-0018"]);
        if (await Direction.Locator("#new-responsible option[value='" + fixture.Accounts[3].PersonId + "']").CountAsync() != 0)
        {
            await Direction.Locator("#new-responsible").SelectOptionAsync(fixture.Accounts[3].PersonId.ToString("D"));
            await Direction.Locator("#correction-reason").FillAsync("Asignación sintética al ejecutor del recorrido");
            await Submit(Direction, Direction.Locator("form[action*='PrepareCorrection'] button[type=submit]"), "PrepareCorrection");
            await Submit(Direction, Direction.Locator("#correction-confirm button[type=submit]"), "CorrectAssignment");
        }
        await Capture(Direction, "R5-generacion-asignacion-historia");
    }

    private async Task PlanAsync()
    {
        await Direction.GotoAsync("/planificacion?" + Period);
        await Submit(Direction, Direction.GetByRole(AriaRole.Button, new() { Name = "Preparar publicación" }), "PreparePlanPublication");
        await Assertions.Expect(Direction.Locator("#plan-confirm")).ToBeVisibleAsync();
        await Direction.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Direction.Locator("#plan-confirm")).ToBeHiddenAsync();
        await Assertions.Expect(Direction.GetByRole(AriaRole.Button, new() { Name = "Revisar publicación" })).ToBeFocusedAsync();
        await Direction.GetByRole(AriaRole.Button, new() { Name = "Revisar publicación" }).ClickAsync();
        await Submit(Direction, Direction.Locator("#plan-confirm button[type=submit]"), "PublishPlan");
        foreach (var (page, index) in pages.Select((p, i) => (p, i)))
        {
            await page.GotoAsync("/planificacion?" + Period);
            await Assertions.Expect(page.Locator("#plan-semanal")).ToContainTextAsync("Publicado");
            await Capture(page, "R6-plan-" + fixture.Accounts[index].Role);
        }
    }
}
