using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Sgol.Evidence.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.MyWork;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.MyWork;

public sealed partial class DetailsModel
{
    public bool CanContribute { get; private set; }
    public ObligationEvidenceRequirement? SelectedRequirement { get; private set; }
    public HashSet<string> ExistingRequirements { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> EvidenceValues { get; } = new(StringComparer.Ordinal);
    public HashSet<string> InvalidEvidenceFields { get; } = new(StringComparer.Ordinal);
    public ProblemDetailsPresentation? EvidenceError { get; private set; }
    public string? EvidenceSuccess { get; private set; }
    public string? PreparedEvidence { get; private set; }
    public bool? DifferenceOrDamage { get; private set; }
    public bool IsBinary => SelectedRequirement?.Kind is "FOTOGRAFIA" or "DOCUMENTO_REFERENCIADO";
    public string EvidenceValue(string name) => EvidenceValues.GetValueOrDefault(name, "");
    private EvidenceIntentionProtector Intentions => new(protection);

    private async Task LoadEvidenceAsync(CancellationToken token)
    {
        var session = await sessionState.GetAsync(token);
        CanContribute = session is not null && Detail?.ExecutionStatus == "PENDIENTE" &&
            Detail.CurrentAssignment?.Responsible.PersonId == session.PersonId && session.Permissions.Contains(EvidenceAuthorization.Contribute);
        if (!CanContribute || Detail?.EvidencePolicy is not { } policy) return;
        var canonical = Sgol.Configuration.Contracts.EvidencePolicyCatalog.Require(Detail.Task.TaskCode);
        if (policy.EvidencePolicyVersionId == Guid.Empty || policy.Requirements is null || policy.Requirements.Count != canonical.Count ||
            policy.Requirements.Where((r, i) => r.RequirementVersionId == Guid.Empty || r.RequirementCode != canonical[i].Code ||
                r.Kind != canonical[i].Kind || r.ConditionCode != canonical[i].ConditionCode || r.Ordinal != canonical[i].Ordinal).Any())
            throw new ApiProtocolException();
        string? cursor = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            var path = QueryHelpers.AddQueryString($"/api/v1/obligations/{Detail.ObligationId:D}/evidence",
                new Dictionary<string, string?> { ["status"] = "VIGENTE", ["cursor"] = cursor });
            var response = await apiClient.SendAsync<EvidenceDetails>(new(HttpMethod.Get, path, ApiResponseShape.Collection), token);
            if (!response.IsSuccess)
            {
                if (response.Status == 401) { sessionState.Invalidate(); ApiCookieBridge.Clear(HttpContext); CanContribute = false; }
                EvidenceError = new(EvidenceContributionPresentation.Message(response.Status, response.ErrorCode), "", response.CorrelationId);
                return;
            }
            foreach (var item in response.Items ?? [])
            {
                if (item.Requirement is null || item.Version?.Status != "VIGENTE" || !policy.Requirements.Any(
                    r => r.RequirementVersionId == item.Requirement.RequirementVersionId && r.RequirementCode == item.Requirement.RequirementCode))
                    throw new ApiProtocolException();
                ExistingRequirements.Add(item.Requirement.RequirementCode);
                if (item.Requirement.RequirementCode == "F_ENT_001" && item.StructuredPayload is { } payload &&
                    StructuredEvidencePayloadValidator.TryResolveDifferenceOrDamage(payload.RootElement, out var applies)) DifferenceOrDamage = applies;
                item.StructuredPayload?.Dispose();
            }
            cursor = response.NextCursor;
            if (cursor is not null && !seen.Add(cursor)) throw new ApiProtocolException();
        } while (cursor is not null);
    }

    private async Task<IFormCollection?> EvidenceFormAsync(CancellationToken token)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (await sessionState.GetAsync(token) is null || !Request.HasFormContentType) return null;
        try { await HttpContext.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(HttpContext); }
        catch (AntiforgeryValidationException) { return null; }
        var form = await Request.ReadFormAsync(token);
        return form.Files.Count == 0 && form.All(p => p.Value.Count == 1) ? form : null;
    }

    private void ChooseRequirement(string? requirement)
    {
        SelectedRequirement = Detail?.EvidencePolicy?.Requirements.SingleOrDefault(r => r.RequirementCode == requirement);
        if (SelectedRequirement is null || ExistingRequirements.Contains(SelectedRequirement.RequirementCode)) SelectedRequirement = null;
        if (SelectedRequirement?.ConditionCode == "DIFERENCIA_O_DANO" && DifferenceOrDamage != true)
        {
            EvidenceError = new(EvidenceContributionPresentation.Message(422, DifferenceOrDamage is null ?
                "CONDICION_EVIDENCIA_NO_RESUELTA" : "REQUISITO_EVIDENCIA_NO_APLICABLE"), "", null);
        }
    }

    public async Task<IActionResult> OnPostSelectEvidenceAsync(string obligationId, CancellationToken cancellationToken)
    {
        if (await sessionState.GetAsync(cancellationToken) is null) return Redirect("/acceso");
        var form = await EvidenceFormAsync(cancellationToken);
        if (form is null) return EvidenceFailure(400, "CSRF_INVALIDO");
        var page = await OnGetAsync(obligationId, cancellationToken);
        if (CanContribute && EvidenceError is null) ChooseRequirement(form["requirementCode"]);
        return page;
    }

    public async Task<IActionResult> OnPostPrepareEvidenceAsync(string obligationId, CancellationToken cancellationToken)
    {
        if (await sessionState.GetAsync(cancellationToken) is null) return Redirect("/acceso");
        var form = await EvidenceFormAsync(cancellationToken);
        if (form is null) return EvidenceFailure(400, "CSRF_INVALIDO");
        var page = await OnGetAsync(obligationId, cancellationToken);
        if (!CanContribute || EvidenceError is not null || Detail is null) return page;
        ChooseRequirement(form["requirementCode"]);
        if (SelectedRequirement is null || IsBinary || EvidenceError is not null) return page;
        foreach (var field in EvidenceContributionPresentation.Inputs(SelectedRequirement.RequirementCode))
            EvidenceValues[field.Name] = form[field.Name].ToString();
        try
        {
            var fields = EvidenceContributionPresentation.Inputs(SelectedRequirement.RequirementCode);
            if (form.Keys.Any(k => k is not ("__RequestVerificationToken" or "requirementCode") && !fields.Any(f => f.Name == k)))
                throw new EvidenceRequestInvalidException();
            using var payload = EvidenceContributionPresentation.Payload(Detail.Task.TaskCode, SelectedRequirement.RequirementCode,
                SelectedRequirement.Kind, EvidenceValues, InvalidEvidenceFields);
            var body = JsonSerializer.SerializeToElement(new { requirementCode = SelectedRequirement.RequirementCode, structuredPayload = payload.RootElement });
            var session = (await sessionState.GetAsync(cancellationToken))!;
            PreparedEvidence = Intentions.Protect(NewIntention(session, Detail.ObligationId, SelectedRequirement.RequirementCode, "structured", body));
        }
        catch (EvidenceRequestInvalidException)
        { EvidenceError = new("Revisa los datos del requisito seleccionado.", "", null); Response.StatusCode = 422; }
        return page;
    }

    public async Task<IActionResult> OnPostSendEvidenceAsync(string obligationId, CancellationToken cancellationToken)
    {
        var form = await EvidenceFormAsync(cancellationToken);
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        if (form is null || !MyWorkQuery.CanonicalId(obligationId, out var id)) return EvidenceFailure(400, "CSRF_INVALIDO");
        var intention = Intentions.Read(form["intention"].ToString(), session.UserId, id, "structured", DateTimeOffset.UtcNow);
        if (intention is null) return EvidenceFailure(400, "SOLICITUD_EVIDENCIA_INVALIDA");
        var page = await OnGetAsync(obligationId, cancellationToken);
        if (page is RedirectResult) return page;
        if (Error is not null) return Page();
        // A valid protected intent may recover a committed result even when its item is now visible.
        if (!CanContribute || Detail?.EvidencePolicy?.Requirements.Any(r => r.RequirementCode == intention.Requirement) != true)
            return EvidenceFailure(403, "ACCESO_DENEGADO");
        PreparedEvidence = form["intention"].ToString();
        var alreadyVisible = ExistingRequirements.Contains(intention.Requirement);
        try
        {
            var response = await SendEvidenceMutationAsync<JsonElement>(form, $"/api/v1/obligations/{id:D}/evidence", intention.Body, intention.Key, cancellationToken);
            if (!response.IsSuccess)
            {
                if (response.Status == 401) { sessionState.Invalidate(); ApiCookieBridge.Clear(HttpContext); return Redirect("/acceso"); }
                EvidenceError = new(EvidenceContributionPresentation.Message(response.Status, response.ErrorCode), "", response.CorrelationId);
                Response.StatusCode = response.Status;
                if (response.Status is 403 or 404) PreparedEvidence = null;
                return Page();
            }
            ConfirmContribution(response.Data, intention.Requirement);
            PreparedEvidence = null;
            EvidenceSuccess = response.Replayed || alreadyVisible ? "Se recuperó la aportación registrada." : "Se aportó la evidencia. Se guardó su primera versión.";
            if (intention.Requirement == "CHECKLIST_COMPLETO" && intention.Body.GetProperty("structuredPayload").EnumerateObject()
                .Any(p => p.Value.ValueKind == JsonValueKind.False)) EvidenceSuccess = "La evidencia se guardó. Una respuesta No no acredita la conformidad del checklist.";
            return Page();
        }
        catch (ApiProtocolException) { EvidenceError = new("No se pudo confirmar el resultado.", "", null); return Page(); }
    }

    public async Task<IActionResult> OnPostPrepareUploadAsync(string obligationId, CancellationToken cancellationToken)
    {
        if (await sessionState.GetAsync(cancellationToken) is null) return EvidenceFailure(401, "AUTENTICACION_REQUERIDA");
        var form = await EvidenceFormAsync(cancellationToken);
        if (form is null) return EvidenceFailure(400, "CSRF_INVALIDO");
        await OnGetAsync(obligationId, cancellationToken);
        if (Error is not null || !CanContribute || EvidenceError is not null) return EvidenceFailure(Response.StatusCode is >= 400 ? Response.StatusCode : 403, "ACCESO_DENEGADO");
        ChooseRequirement(form["requirementCode"]);
        if (SelectedRequirement is null || !IsBinary || EvidenceError is not null || Detail is null) return EvidenceFailure(422, "REQUISITO_EVIDENCIA_INVALIDO");
        if (!long.TryParse(form["sizeBytes"], NumberStyles.None, CultureInfo.InvariantCulture, out var size) || size < 1) return EvidenceFailure(400, "SOLICITUD_EVIDENCIA_INVALIDA");
        if (size > 15728640) return EvidenceFailure(413, "ARCHIVO_DEMASIADO_GRANDE");
        var hash = form["sha256"].ToString();
        if (hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigitLower(c))) return EvidenceFailure(400, "SOLICITUD_EVIDENCIA_INVALIDA");
        var subtype = SelectedRequirement.RequirementCode == "DOCUMENTO_RECEPCION" ? form["documentSubtype"].ToString() : null;
        var body = JsonSerializer.SerializeToElement(new { obligationId = Detail.ObligationId, requirementCode = SelectedRequirement.RequirementCode,
            originalFileName = form["originalFileName"].ToString(), declaredMediaType = form["declaredMediaType"].ToString(), sizeBytes = size, sha256 = hash, documentSubtype = subtype });
        var session = (await sessionState.GetAsync(cancellationToken))!;
        return new JsonResult(new { intention = Intentions.Protect(NewIntention(session, Detail.ObligationId, SelectedRequirement.RequirementCode, "upload", body)) });
    }

    public Task<IActionResult> OnPostUploadAsync(string obligationId, CancellationToken cancellationToken) => FileOperationAsync(obligationId, "upload", cancellationToken);
    public Task<IActionResult> OnPostCompleteUploadAsync(string obligationId, CancellationToken cancellationToken) => FileOperationAsync(obligationId, "complete", cancellationToken);
    public Task<IActionResult> OnPostFileStatusAsync(string obligationId, CancellationToken cancellationToken) => FileOperationAsync(obligationId, "status", cancellationToken);
    public Task<IActionResult> OnPostContributeFileAsync(string obligationId, CancellationToken cancellationToken) => FileOperationAsync(obligationId, "contribute", cancellationToken);

    private async Task<IActionResult> FileOperationAsync(string obligationId, string operation, CancellationToken token)
    {
        var form = await EvidenceFormAsync(token);
        var session = await sessionState.GetAsync(token);
        if (session is null) return EvidenceFailure(401, "AUTENTICACION_REQUERIDA");
        if (form is null || !MyWorkQuery.CanonicalId(obligationId, out var id)) return EvidenceFailure(400, "CSRF_INVALIDO");
        var intention = Intentions.Read(form["intention"].ToString(), session.UserId, id, operation == "upload" ? "upload" : "file", DateTimeOffset.UtcNow);
        if (intention is null || operation != "upload" && intention.FileId is null) return EvidenceFailure(400, "SOLICITUD_EVIDENCIA_INVALIDA");
        try
        {
            var linked = false;
            if (operation == "contribute")
            {
                var current = await apiClient.SendAsync<JsonElement>(new(HttpMethod.Get, $"/api/v1/files/{intention.FileId:D}/status", ApiResponseShape.Item), token);
                if (!current.IsSuccess) return EvidenceFailure(current.Status, current.ErrorCode, current.CorrelationId);
                if (current.Data.GetProperty("fileId").GetGuid() != intention.FileId) throw new ApiProtocolException();
                if (current.Data.GetProperty("status").GetString() != "LIMPIO") return EvidenceFailure(409, "ARCHIVO_NO_LIMPIO");
                linked = current.Data.TryGetProperty("linkedEvidenceItemId", out var item) && item.ValueKind != JsonValueKind.Null;
            }
            var path = operation switch
            {
                "upload" => "/api/v1/files/upload-intents",
                "complete" => $"/api/v1/files/{intention.FileId:D}/complete",
                "status" => $"/api/v1/files/{intention.FileId:D}/status",
                _ => $"/api/v1/obligations/{id:D}/evidence"
            };
            var body = operation switch
            {
                "upload" => intention.Body,
                "complete" => JsonSerializer.SerializeToElement(new { }),
                _ => JsonSerializer.SerializeToElement(new { requirementCode = intention.Requirement, fileId = intention.FileId })
            };
            var response = operation == "status" ? await apiClient.SendAsync<JsonElement>(new(HttpMethod.Get, path, ApiResponseShape.Item), token) :
                await SendEvidenceMutationAsync<JsonElement>(form, path, body, operation == "upload" ? intention.Key :
                    operation == "complete" ? intention.CompleteKey : intention.ContributionKey, token);
            if (!response.IsSuccess) return EvidenceFailure(response.Status, response.ErrorCode, response.CorrelationId);
            var data = response.Data;
            if (operation == "upload")
            {
                var fileId = data.GetProperty("fileId").GetGuid();
                if (fileId == Guid.Empty || data.GetProperty("status").GetString() != "PENDIENTE_CARGA") throw new ApiProtocolException();
                var flow = Intentions.Protect(intention with { Operation = "file", FileId = fileId, Body = JsonSerializer.SerializeToElement(new { }) });
                // This transient JSON is the transport contract, never rendered markup or persisted URL.
                return new JsonResult(new { upload = data.GetProperty("upload"), intention = flow });
            }
            if (operation == "contribute")
            {
                ConfirmContribution(data, intention.Requirement);
                if (data.GetProperty("file").GetProperty("fileId").GetGuid() != intention.FileId) throw new ApiProtocolException();
                return new JsonResult(new { message = response.Replayed || linked ? "Se recuperó la aportación registrada." : "Se aportó la evidencia. Se guardó su primera versión." });
            }
            if (data.GetProperty("fileId").GetGuid() != intention.FileId) throw new ApiProtocolException();
            var state = data.GetProperty("status").GetString()!;
            return new JsonResult(new { status = state, message = EvidenceContributionPresentation.Scan(state) });
        }
        catch (Exception ex) when (ex is ApiProtocolException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { return EvidenceFailure(503, null); }
    }

    private static EvidenceIntention NewIntention(SessionSnapshot session, Guid obligation, string requirement, string operation, JsonElement body) =>
        new(session.UserId, obligation, requirement, operation, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            session.IdleExpiresAt < session.AbsoluteExpiresAt ? session.IdleExpiresAt : session.AbsoluteExpiresAt, body);

    private Task<ApiResponse<T>> SendEvidenceMutationAsync<T>(IFormCollection form, string path, JsonElement body, Guid key, CancellationToken token) =>
        apiClient.SendAsync<T>(new(HttpMethod.Post, path, ApiResponseShape.Item, body, Intent: ApiMutationIntent.FromKey(key),
            CsrfToken: form["__RequestVerificationToken"].ToString()), token);

    private void ConfirmContribution(JsonElement data, string requirement)
    {
        if (data.GetProperty("evidenceItemId").GetGuid() == Guid.Empty || data.GetProperty("requirement").GetProperty("requirementCode").GetString() != requirement ||
            data.GetProperty("version").GetProperty("versionNo").GetInt32() != 1 || data.GetProperty("version").GetProperty("status").GetString() != "VIGENTE") throw new ApiProtocolException();
        ExistingRequirements.Add(requirement);
    }

    private JsonResult EvidenceFailure(int status, string? code, string? correlation = null)
    {
        if (status == 401) { sessionState.Invalidate(); ApiCookieBridge.Clear(HttpContext); }
        return new JsonResult(new { message = EvidenceContributionPresentation.Message(status, code), correlationId = correlation }) { StatusCode = status };
    }
}
