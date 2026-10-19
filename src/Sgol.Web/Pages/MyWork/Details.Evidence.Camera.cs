using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Sgol.Web.Presentation.ApiClient;

namespace Sgol.Web.Pages.MyWork;

public sealed partial class DetailsModel
{
    public async Task<IActionResult> OnGetEvidenceDownloadAsync(string obligationId, Guid fileId, CancellationToken token)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (await sessionState.GetAsync(token) is null) return Redirect("/acceso");
        var response = await apiClient.SendAsync<JsonElement>(new(HttpMethod.Get, $"/api/v1/files/{fileId:D}/download", ApiResponseShape.Item), token);
        if (!response.IsSuccess) return StatusCode(response.Status);
        var url = response.Data.GetProperty("download").GetProperty("url").GetString();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) return StatusCode(503);
        return Redirect(uri.AbsoluteUri);
    }
    public async Task<IActionResult> OnPostBeginPhotoCameraAsync(string obligationId, CancellationToken token)
    {
        var form = await EvidenceFormAsync(token);
        var session = await sessionState.GetAsync(token);
        if (session is null) return EvidenceFailure(401, "AUTENTICACION_REQUERIDA");
        if (form is null) return EvidenceFailure(400, "CSRF_INVALIDO");
        await OnGetAsync(obligationId, token);
        if (Detail is null || Error is not null) return EvidenceFailure(404, "OBLIGACION_NO_ENCONTRADA");
        if (form.ContainsKey("replacement"))
        { if (!RestoreReplacement(form, session.UserId, Detail.ObligationId)) return EvidenceFailure(403, "ACCESO_DENEGADO"); }
        else { if (!CanContribute) return EvidenceFailure(403, "ACCESO_DENEGADO"); ChooseRequirement(form["requirementCode"]); }
        if (SelectedRequirement?.Kind != "FOTOGRAFIA" || EvidenceError is not null) return EvidenceFailure(422, "REQUISITO_EVIDENCIA_INVALIDO");
        var response = await SendEvidenceMutationAsync<JsonElement>(form, $"/api/v1/obligations/{Detail.ObligationId:D}/camera-sessions",
            JsonSerializer.SerializeToElement(new { requirementCode = SelectedRequirement.RequirementCode }), Guid.NewGuid(), token);
        return response.IsSuccess ? new JsonResult(response.Data) : EvidenceFailure(response.Status, response.ErrorCode, response.CorrelationId);
    }

    public async Task<IActionResult> OnPostContributePhotographsAsync(string obligationId, CancellationToken token)
    {
        var form = await EvidenceFormAsync(token);
        var session = await sessionState.GetAsync(token);
        if (session is null) return EvidenceFailure(401, "AUTENTICACION_REQUERIDA");
        if (form is null || form.Keys.Any(k => k is not ("__RequestVerificationToken" or "intentions")) ||
            !Sgol.Web.Presentation.MyWork.MyWorkQuery.CanonicalId(obligationId, out var id)) return EvidenceFailure(400, "CSRF_INVALIDO");
        try
        {
            var value = form["intentions"].ToString();
            if (value.Length > 120000) return EvidenceFailure(400, "SOLICITUD_EVIDENCIA_INVALIDA");
            var raw = JsonSerializer.Deserialize<string[]>(value);
            if (raw is null || raw.Length is < 1 or > 5) return EvidenceFailure(400, "SOLICITUD_EVIDENCIA_INVALIDA");
            var flows = raw.Select(t => Intentions.Read(t, session.UserId, id, "file", DateTimeOffset.UtcNow)).ToArray();
            if (flows.Any(f => f?.FileId is null)) return EvidenceFailure(400, "SOLICITUD_EVIDENCIA_INVALIDA");
            var first = flows[0]!;
            if (flows.Any(f => f!.Requirement != first.Requirement || f.ReplacementItem != first.ReplacementItem ||
                f.ReplacementRowVersion != first.ReplacementRowVersion || f.ReplacementVersionNo != first.ReplacementVersionNo || f.Reason != first.Reason))
                return EvidenceFailure(400, "SOLICITUD_EVIDENCIA_INVALIDA");
            var fileIds = flows.Select(f => f!.FileId!.Value).ToArray();
            if (fileIds.Distinct().Count() != fileIds.Length) return EvidenceFailure(400, "SOLICITUD_EVIDENCIA_INVALIDA");
            var path = first.ReplacementItem is not null ? ReplacementPath(id, first) : $"/api/v1/obligations/{id:D}/evidence";
            var body = first.ReplacementItem is not null ? JsonSerializer.SerializeToElement(new { fileId = fileIds[0], fileIds, reason = first.Reason }) :
                JsonSerializer.SerializeToElement(new { requirementCode = first.Requirement, fileId = fileIds[0], fileIds });
            var response = await SendEvidenceMutationAsync<JsonElement>(form, path, body, first.ContributionKey, token,
                first.ReplacementItem is not null ? ReplacementETag(first) : null);
            if (!response.IsSuccess) return EvidenceFailure(response.Status, response.ErrorCode, response.CorrelationId);
            if (first.ReplacementItem is not null) ConfirmReplacement(response.Data, first); else ConfirmContribution(response.Data, first.Requirement);
            if (!response.Data.GetProperty("files").EnumerateArray().Select(x => x.GetProperty("fileId").GetGuid()).SequenceEqual(fileIds)) throw new ApiProtocolException();
            return new JsonResult(new { message = first.ReplacementItem is null ? "Se aportaron las fotografías. Se guardó su primera versión." : "Se sustituyeron las fotografías. La versión anterior permanece en historia." });
        }
        catch (Exception e) when (e is JsonException or ApiProtocolException or InvalidOperationException or KeyNotFoundException or FormatException)
        { return EvidenceFailure(400, "SOLICITUD_EVIDENCIA_INVALIDA"); }
    }
}
