using Microsoft.AspNetCore.Mvc;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;

namespace Sgol.Web.Pages.Validations;
public sealed partial class IndexModel
{
    public OperationalCarryoverRequest[] Carryovers { get; private set; } = [];
    public Dictionary<Guid, OperationalAssignment> CarryoverCurrent { get; private set; } = [];
    public int PendingAuthorizedCarryoverCount { get; private set; }
    public Dictionary<Guid, CarryoverDecisionPreview> CarryoverDiagnostics { get; private set; } = [];
    public Dictionary<Guid, CapacityWorker[]> CarryoverRecipients { get; private set; } = [];
    public CarryoverDecisionPreview? ItemPreview { get; private set; }
    [BindProperty] public CarryoverItemForm ItemDecision { get; set; } = new();
    [BindProperty] public string? ItemFingerprint { get; set; }
    public string ItemDecisionHref => CarryoverDecisionHref.Replace("handler=Carryover", "handler=CarryoverItem");
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
        foreach (var request in pending.Items.Where(r => r.Kind == "POSPONER"))
            foreach (var item in request.PendingItems())
                if (CarryoverCurrent.TryGetValue(item.AssignmentId, out var current)) {
                    CarryoverDiagnostics[item.AssignmentId] = await carryovers.PreviewDecisionAsync(actor,
                        new(request.Id, item.AssignmentId, current.Revision, "APPROVE", null, null, "", Guid.Empty), token);
                    CarryoverRecipients[item.AssignmentId] = current.CompletedAt == null ? await supervision.TransferRecipientsAsync(actor, item.AssignmentId, token) : [];
                }
    }
    public async Task<IActionResult> OnPostCarryoverItemAsync(bool confirm, CancellationToken token)
    {
        var actor = await ActorAsync(token); if (actor == null) return Redirect("/acceso");
        try {
            // Other independent forms on this page are not part of this POST.
            foreach (var key in ModelState.Keys.Where(k => !k.StartsWith("ItemDecision.") && k != nameof(ItemFingerprint) && k != "confirm").ToArray()) ModelState.Remove(key);
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(ItemDecision.Comment) || ItemDecision.Comment.Length > 4000)
                throw new DirectionPlanException("ARRASTRE_DATOS_INVALIDOS", 422);
            var command = ItemDecision.Command();
            if (confirm) {
                await carryovers.ResolveItemAsync(actor.Value, command, ItemFingerprint ?? "", token);
                ReviewSuccess = "Decisión guardada para esta tarea. Las demás conservan su estado.";
                ModelState.Clear(); ItemDecision = new(); ItemFingerprint = null;
            } else {
                ItemPreview = await carryovers.PreviewDecisionAsync(actor.Value, command, token);
                ItemFingerprint = ItemPreview.Fingerprint; ModelState.Remove(nameof(ItemFingerprint));
            }
        } catch (DirectionPlanException e) {
            if (e.Status == 403) return StatusCode(403);
            Error = e.Code == "ARRASTRE_DATOS_INVALIDOS" ? "Elige una acción y escribe el comentario de decisión (hasta 4000 caracteres)." : "La tarea o su agenda cambió. Revisa el diagnóstico y vuelve a obtener la vista previa antes de confirmar.";
            ItemFingerprint = null; ModelState.Remove(nameof(ItemFingerprint));
        }
        await LoadAsync(actor.Value, null, token);
        return Page();
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
public sealed class CarryoverItemForm
{
    public Guid RequestId { get; set; }
    public Guid AssignmentId { get; set; }
    public long Revision { get; set; }
    public string Action { get; set; } = "APPROVE";
    public Guid? Recipient { get; set; }
    public DateOnly? Day { get; set; }
    public string Comment { get; set; } = "";
    public Guid Intent { get; set; } = Guid.NewGuid();
    public CarryoverDecision Command() => new(RequestId, AssignmentId, Revision, Action, Recipient, Day, Comment, Intent);
}
