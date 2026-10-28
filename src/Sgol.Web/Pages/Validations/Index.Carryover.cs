using Microsoft.AspNetCore.Mvc;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;

namespace Sgol.Web.Pages.Validations;
public sealed partial class IndexModel
{
    public OperationalCarryoverRequest[] Carryovers { get; private set; } = [];
    public Dictionary<Guid, OperationalAssignment> CarryoverCurrent { get; private set; } = [];
    public int PendingAuthorizedCarryoverCount { get; private set; }
    public Sgol.Web.Presentation.Navigation.ListPage? CarryoverPage { get; private set; }
    public Sgol.Web.Presentation.Navigation.ListPage? CarryoverHistoryPage { get; private set; }
    private async Task LoadCarryoversAsync(Guid actor, CancellationToken token)
    {
        var pending = await carryovers.InboxPageAsync(actor, false, Sgol.Web.Presentation.Navigation.ListPage.Read(Request, "CarryoverPage"), token);
        var history = await carryovers.InboxPageAsync(actor, true, Sgol.Web.Presentation.Navigation.ListPage.Read(Request, "CarryoverHistoryPage"), token);
        PendingAuthorizedCarryoverCount = pending.Total;
        Carryovers = pending.Items.Concat(history.Items).ToArray();
        CarryoverPage = new("CarryoverPage", pending.Page, pending.Total, 10, Request, "carryovers");
        CarryoverHistoryPage = new("CarryoverHistoryPage", history.Page, history.Total, 10, Request, "carryovers");
        var ids = pending.Items.SelectMany(r => r.Items()).Select(i => i.AssignmentId).Distinct().ToArray();
        CarryoverCurrent = (await supervision.FindManyAsync(actor, ids, token)).ToDictionary(t => t.Assignment.Id, t => t.Assignment);
    }
    public string CarryoverDecisionHref => Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("/validaciones",
        Request.Query.Where(p => p.Key != "handler").ToDictionary(p => p.Key, p => (string?)p.Value.ToString())
            .Append(new KeyValuePair<string,string?>("handler", "Carryover")).ToDictionary());
    public async Task<IActionResult> OnPostCarryoverAsync(Guid requestId, long requestRevision, bool approve, string decisionComment, CancellationToken token)
    {
        var actor = await ActorAsync(token); if (actor == null) return Redirect("/acceso");
        try { await carryovers.DecideAsync(actor.Value, requestId, requestRevision, approve, decisionComment, token); }
        catch (DirectionPlanException e) { Error = e.Code == "ARRASTRE_CONFLICTO" ? "El alcance cambió. Revisa los datos actuales; rechaza y solicita una nueva petición si hubo cambios de fechas o responsable." : e.Code; }
        await LoadAsync(actor.Value, null, token); return Page();
    }
}
