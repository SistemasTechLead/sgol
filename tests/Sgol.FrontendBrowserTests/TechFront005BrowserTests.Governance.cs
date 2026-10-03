using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Sgol.Configuration.Contracts;
using Xunit;

namespace Sgol.FrontendBrowserTests;

public sealed partial class TechFront005BrowserTests
{
    private async Task IdentityAsync()
    {
        var page = Direction;
        await page.GotoAsync("/personas-y-accesos");
        var stable = "TF005-" + Guid.CreateVersion7().ToString("N");
        await page.GetByLabel("Código de persona").FillAsync(stable);
        await page.GetByLabel("Nombre", new() { Exact = true }).FillAsync("Persona auxiliar sintética");
        await Submit(page, page.GetByRole(AriaRole.Button, new() { Name = "Registrar persona" }), "", 302);
        await using var db = fixture.TechFront005Context();
        var person = await db.People.AsNoTracking().SingleAsync(p => p.StableCode == stable);
        auxiliaryPerson = person.Id;
        await Assertions.Expect(page).ToHaveURLAsync(fixture.BaseAddress.AbsoluteUri.TrimEnd('/') + "/personas-y-accesos/personas/" + person.Id.ToString("D"));
        await page.GotoAsync("/personas-y-accesos");
        var userName = "tf005." + Guid.CreateVersion7().ToString("N");
        await page.Locator("#account-person").SelectOptionAsync(person.Id.ToString("D"));
        await page.Locator("#account-user").FillAsync(userName);
        await Submit(page, page.Locator("#alta-cuenta button[type=submit]"), "CreateAccount");
        var password = await page.Locator("[data-sensitive-activation] code").InnerTextAsync();
        await page.ReloadAsync();
        var user = await db.IdentityCredentials.AsNoTracking().SingleAsync(u => u.UserName == userName);
        await ChangeAuxiliaryRole(page, user.UserId, "PISO_VENTAS");
        var auxiliary = new BrowserAccount(user.UserId, person.Id, userName, "PISO_VENTAS", password, "S!" + Guid.CreateVersion7().ToString("N") + "a");
        await using var auxiliaryContext = await ContextAsync();
        var auxiliaryPage = await auxiliaryContext.NewPageAsync();
        await FirstAccess(auxiliaryPage, auxiliary);
        await page.GotoAsync("/personas-y-accesos/personas/" + person.Id);
        await page.GetByLabel("Puesto", new() { Exact = true }).FillAsync("Director");
        await page.GetByLabel("Turno", new() { Exact = true }).FillAsync("Matutino");
        await page.GetByLabel("Motivo").First.FillAsync("Corrección laboral sintética");
        await Submit(page, page.GetByRole(AriaRole.Button, new() { Name = "Guardar empleo" }), "Employment");
        foreach (var (button, handler) in new[] { ("Dar de baja", "Deactivate"), ("Reactivar", "Reactivate") })
        {
            await page.GetByRole(AriaRole.Button, new() { Name = button, Exact = true }).ClickAsync();
            await page.Locator("dialog[open] textarea[name=reason]").FillAsync("Vigencia sintética");
            await Submit(page, page.Locator("dialog[open] button[type=submit]"), handler);
        }
        await Capture(page, "R2-historia-laboral");
        foreach (var personId in fixture.Accounts.Select(a => a.PersonId).Append(auxiliaryPerson))
        {
            await page.GotoAsync("/personas-y-accesos/personas/" + personId + "?fromDate=" + IsoDay + "&toDate=" + IsoDay + "&day=" + IsoDay);
            var form = page.Locator("form[action*='Availability']").First;
            await form.Locator("input[type=radio][value=true]").CheckAsync();
            await Submit(page, form.Locator("button[type=submit]"), "Availability");
        }
        await page.GotoAsync("/personas-y-accesos");
        await ChangeAuxiliaryRole(page, user.UserId, "SUBCOORDINACION");
        Assert.Equal(401, (await auxiliaryContext.APIRequest.GetAsync("/api/v1/auth/session")).Status);
        await ChangeAuxiliaryRole(page, user.UserId, "PISO_VENTAS");
        var revoke = page.Locator("dialog[id$='-revoke']").Filter(new()
        { Has = page.Locator("input[name=userId][value='" + user.UserId + "']") });
        var revokeId = await revoke.GetAttributeAsync("id");
        await page.Locator("button[aria-controls='" + revokeId + "']").ClickAsync();
        await revoke.Locator("textarea[name=reason]").FillAsync("Revocación sintética motivada");
        await Submit(page, revoke.Locator("button[type=submit]"), "ChangeRole");
        await Assertions.Expect(page.Locator("main")).ToContainTextAsync("Rol revocado");
        await ChangeAuxiliaryRole(page, user.UserId, "PISO_VENTAS");
        foreach (var handler in new[] { "DeactivateAccount", "ReactivateAccount" })
        {
            var accountDialog = page.Locator("#account-status-" + user.UserId.ToString("D"));
            await page.Locator("button[aria-controls='account-status-" + user.UserId.ToString("D") + "']").ClickAsync();
            await accountDialog.Locator("textarea[name=reason]").FillAsync("Vigencia de cuenta sintética motivada");
            await Submit(page, accountDialog.Locator("button[type=submit]"), handler);
            var status = await db.AppUsers.AsNoTracking().Where(u => u.Id == user.UserId).Select(u => u.Status).SingleAsync();
            Assert.Equal(handler == "DeactivateAccount" ? "INACTIVA" : "ACTIVA", status);
        }
        await Capture(page, "R2-cuentas-y-roles");
        var reset = page.Locator("dialog").Filter(new() { Has = page.Locator("form[action*='ResetMfa'] input[name=userId][value='" + user.UserId + "']") });
        var resetId = await reset.GetAttributeAsync("id");
        await page.Locator("button[aria-controls='" + resetId + "']").ClickAsync();
        await reset.Locator("textarea[name=reason]").FillAsync("Reset sintético autorizado");
        await Submit(page, reset.Locator("button[type=submit]"), "ResetMfa");
    }

    private async Task ChangeAuxiliaryRole(IPage page, Guid user, string role)
    {
        var dialog = page.Locator("dialog").Filter(new() { Has = page.Locator("form[action*='ChangeRole'] input[name=userId][value='" + user + "']") })
            .Filter(new() { Has = page.Locator("select[name=roleCode]") });
        var id = await dialog.GetAttributeAsync("id");
        await page.Locator("button[aria-controls='" + id + "']").ClickAsync();
        await dialog.Locator("select[name=roleCode]").SelectOptionAsync(role);
        await dialog.Locator("textarea[name=reason]").FillAsync("Rol sintético autorizado");
        await Submit(page, dialog.Locator("button[type=submit]"), "ChangeRole");
    }

    private async Task CalendarAsync()
    {
        var page = Direction;
        await page.GotoAsync("/configuracion");
        await Capture(page, "R3-release-vacia");
        await NewDraft();
        // Publish enough real calendar facts for the seven-working-day warranty contract.
        for (var offset = 0; offset < 12; offset++)
        {
            var date = day.AddDays(offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            await page.GotoAsync("/planificacion?" + Period + "&from=" + date + "&to=" + date + "&day=" + date + "&releaseId=" + draft);
            await page.Locator("#draft-type").SelectOptionAsync(offset < 10 ? "LABORABLE" : offset == 10 ? "FESTIVO" : "CIERRE_EXTRAORDINARIO");
            await page.GetByRole(AriaRole.Button, new() { Name = "Guardar día en borrador", Exact = true }).ClickAsync();
            await Assertions.Expect(page.Locator("#calendar-confirm")).ToBeVisibleAsync();
            await page.Locator("#calendar-reason").FillAsync("Calendario sintético integral");
            await Submit(page, page.Locator("#calendar-save button[type=submit]"), "Save");
        }
        await PublishDraft(DateTimeOffset.UtcNow.AddDays(-3));
        foreach (var role in pages)
        {
            await role.GotoAsync("/planificacion?" + Period + "&from=" + IsoDay + "&to=" + IsoDay);
            await Assertions.Expect(role.Locator("#published-calendar")).ToContainTextAsync("Laborable");
        }
        await Capture(page, "R3-calendario-semana");
    }

    private async Task NewDraft()
    {
        await Direction.GotoAsync("/configuracion");
        await Submit(Direction, Direction.GetByRole(AriaRole.Button, new() { Name = "Crear borrador", Exact = true }), "Create");
        var created = Direction.Locator("section[aria-labelledby='releases-title'] > a")
            .Filter(new() { HasText = "Editar días del borrador" });
        await Assertions.Expect(created).ToHaveCountAsync(1);
        var href = await created.GetAttributeAsync("href");
        draft = Guid.Parse(href!.Split("releaseId=", StringSplitOptions.None)[1].Split('&')[0]);
    }

    private async Task PublishDraft(DateTimeOffset effective)
    {
        await Direction.GotoAsync("/configuracion");
        await Direction.Locator("button[aria-controls='publish-" + draft.ToString("D") + "']").ClickAsync();
        await Direction.Locator("dialog[open] input[name=effectiveFrom]").FillAsync(Local(effective));
        await Direction.Locator("dialog[open] textarea[name=reason]").FillAsync("Publicación sintética integral");
        await Submit(Direction, Direction.Locator("dialog[open] button[type=submit]"), "Publish");
    }

    private async Task ConfigurationAsync()
    {
        await NewDraft();
        foreach (var task in TaskDefinitionCatalog.All)
        {
            await Direction.GotoAsync("/configuracion?taskCode=" + task.TaskCode);
            await Direction.Locator("#task-code-select").SelectOptionAsync(task.TaskCode);
            await Direction.Locator("#task-release-select").SelectOptionAsync(draft.ToString("D"));
            await Submit(Direction, Direction.GetByRole(AriaRole.Button, new() { Name = "Crear versión TAR" }), "CreateTaskVersion");
        }
        await PublishDraft(DateTimeOffset.UtcNow.AddDays(-2));
        await NewDraft();
        foreach (var task in TaskDefinitionCatalog.All)
        {
            await Direction.GotoAsync("/configuracion?taskCode=" + task.TaskCode);
            foreach (var (section, handler, label) in new[] { ("activation", "ActivationPolicy", "Guardar política de activación"),
                ("eligibility", "EligibilityPolicy", "Guardar política de elegibilidad"),
                ("evidence", "EvidencePolicy", "Guardar política de evidencia"), ("validation", "ValidationPolicy", "Guardar política de validación") })
            {
                await Direction.Locator("#" + section + "-policy button[data-dialog-open]").First.ClickAsync();
                var dialog = Direction.Locator("dialog[open]");
                if (section is "evidence" or "validation")
                    await dialog.Locator("select[name=releaseId]").SelectOptionAsync(draft.ToString("D"));
                else
                    Assert.Equal(draft.ToString("D"), await dialog.Locator("input[name=releaseId]").InputValueAsync());
                if (section == "activation" && task.TaskCode == "TAR-0026")
                    await dialog.Locator("input[name=localTime]").FillAsync("08:30");
                await Submit(Direction, dialog.GetByRole(AriaRole.Button, new() { Name = label }), handler);
            }
        }
        await PublishDraft(DateTimeOffset.UtcNow.AddDays(-1));
        await Capture(Direction, "R4-ocho-TAR-politicas");
    }
}
