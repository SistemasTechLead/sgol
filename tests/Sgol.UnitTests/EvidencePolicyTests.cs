using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Sgol.Configuration.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class EvidencePolicyTests
{
    [Fact]
    public void Catalog_ContainsExactlyTheApprovedEightPoliciesAndTwentySevenRequirements()
    {
        Assert.Equal(TaskDefinitionCatalog.All.Select(item => item.TaskCode).Order(), EvidencePolicyCatalog.All.Keys.Order());
        Assert.Equal(27, EvidencePolicyCatalog.All.Sum(item => item.Value.Count));
    }

    [Theory]
    [InlineData("TAR-0005", "CALCULO_AVANCE,ACCION_O_CONFORMIDAD")]
    [InlineData("TAR-0007", "LIBERACION,MERCANCIA,FECHA_HORA,RETORNO_EXHIBICION")]
    [InlineData("TAR-0008", "EXPEDIENTE,SECUENCIA,DECISION,FUNDAMENTO,AVISO_INTERNO")]
    [InlineData("TAR-0011", "EVALUACION,AUTORIZACION,REPARACION_O_CAMBIO,COMPROBANTES,ENTREGA")]
    [InlineData("TAR-0018", "CHECKLIST_COMPLETO,FOTOGRAFIA_FINAL,PLANOGRAMA_O_LISTA")]
    [InlineData("TAR-0026", "FORM_ADM_02,COMPROBANTE_LOCALIZABLE")]
    [InlineData("TAR-0092", "DOCUMENTO_RECEPCION,F_ENT_001,FOTO_DIFERENCIA_DANO")]
    [InlineData("TAR-0093", "FOTOGRAFIA_INCIDENCIA,ANOTACION_F_ENT_001,CONSTANCIA_AVISO_INTERNO")]
    public void Catalog_ContainsTheExactApprovedRequirementCodes(string taskCode, string expectedCodes)
    {
        Assert.Equal(
            expectedCodes.Split(','),
            EvidencePolicyCatalog.Require(taskCode).Select(item => item.Code));
    }

    [Fact]
    public void Catalog_UsesOnlyTheClosedKindsAndConditionalPhotoContract()
    {
        var all = EvidencePolicyCatalog.All.SelectMany(item => item.Value).ToArray();
        Assert.All(all, item => Assert.Contains(item.Kind, EvidenceRequirementKinds.All));
        Assert.All(all, item => Assert.Contains(item.ConditionCode, EvidenceConditionCodes.All));
        var conditional = Assert.Single(all, item => item.ConditionCode == EvidenceConditionCodes.DifferenceOrDamage);
        Assert.Equal("FOTO_DIFERENCIA_DANO", conditional.Code);
        Assert.Equal(EvidenceRequirementKinds.Photograph, conditional.Kind);

        var finalPhoto = EvidencePolicyCatalog.Require("TAR-0018").Single(item => item.Code == "FOTOGRAFIA_FINAL");
        Assert.Equal(EvidenceConditionCodes.Always, finalPhoto.ConditionCode);
        Assert.DoesNotContain(all, item => item.Code is "CUALQUIERA" or "AL_MENOS_N");
    }

    [Fact]
    public void Validation_NormalizesOrderAndRejectsMissingDuplicateUnknownKindOrCondition()
    {
        var approved = Inputs("TAR-0092").Reverse().ToArray();
        var normalized = EvidencePolicyCatalog.Validate("TAR-0092", approved);
        Assert.Equal(["DOCUMENTO_RECEPCION", "F_ENT_001", "FOTO_DIFERENCIA_DANO"], normalized.Select(item => item.Code));

        Assert.Throws<EvidencePolicyValidationException>(() => EvidencePolicyCatalog.Validate("TAR-0092", approved[..2]));
        Assert.Throws<EvidencePolicyValidationException>(() => EvidencePolicyCatalog.Validate("TAR-0092", [approved[0], approved[0], approved[1]]));
        Assert.Throws<EvidencePolicyValidationException>(() => EvidencePolicyCatalog.Validate(
            "TAR-0092",
            approved.Select(item => item.Code == "F_ENT_001" ? item with { Kind = EvidenceRequirementKinds.Photograph } : item).ToArray()));
        Assert.Throws<EvidencePolicyValidationException>(() => EvidencePolicyCatalog.Validate(
            "TAR-0092",
            approved.Select(item => item.Code == "FOTO_DIFERENCIA_DANO" ? item with { ConditionCode = EvidenceConditionCodes.Always } : item).ToArray()));
        Assert.Throws<TaskDefinitionNotMvpException>(() => EvidencePolicyCatalog.Require("TAR-9999"));
    }

    [Fact]
    public async Task PutEndpoint_UsesStrictBodyAndForwardsHeadersAndClosedCondition()
    {
        var service = new RecordingService();
        var context = AuthenticatedContext();
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        context.Request.Headers.IfMatch = "\"7\"";
        var releaseId = Guid.CreateVersion7();
        using var body = Request("TAR-0092", releaseId);

        var result = await EvidencePolicyApiEndpoints.HandlePutAsync(
            "TAR-0092", body.RootElement, context, service, CancellationToken.None);

        Assert.Equal(201, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("\"1\"", context.Response.Headers.ETag);
        Assert.NotNull(service.Command);
        Assert.Equal(7, service.Command.ExpectedRowVersion);
        Assert.Equal(EvidenceConditionCodes.DifferenceOrDamage, service.Command.Requirements.Last().ConditionCode);
    }

    [Fact]
    public async Task PutEndpoint_RejectsUnknownFieldsAndMapsNonMvpToNotFound()
    {
        var service = new RecordingService();
        var context = AuthenticatedContext();
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        using var invalid = JsonSerializer.SerializeToDocument(new
        {
            releaseId = Guid.CreateVersion7(),
            requirements = Array.Empty<object>(),
            schema = new { },
        });

        var malformed = await EvidencePolicyApiEndpoints.HandlePutAsync(
            "TAR-0005", invalid.RootElement, context, service, CancellationToken.None);
        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(malformed).StatusCode);
        Assert.Null(service.Command);
        Assert.Equal(1, service.RejectionCount);

        service.Exception = new TaskDefinitionNotMvpException();
        using var valid = Request("TAR-0005", Guid.CreateVersion7());
        var missing = await EvidencePolicyApiEndpoints.HandlePutAsync(
            "TAR-9999", valid.RootElement, context, service, CancellationToken.None);
        Assert.Equal(404, Assert.IsAssignableFrom<IStatusCodeHttpResult>(missing).StatusCode);
    }

    [Fact]
    public async Task PutEndpoint_DoesNotLetMalformedInputBypassServerAuthorization()
    {
        var service = new RecordingService { RejectionException = new EvidencePolicyAccessDeniedException() };
        var context = AuthenticatedContext();
        using var body = JsonSerializer.SerializeToDocument(new { });

        var result = await EvidencePolicyApiEndpoints.HandlePutAsync(
            "TAR-0005", body.RootElement, context, service, CancellationToken.None);

        Assert.Equal(403, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.Command);
    }

    private static EvidenceRequirementInput[] Inputs(string taskCode) =>
        EvidencePolicyCatalog.Require(taskCode)
            .Select(item => new EvidenceRequirementInput(item.Code, item.Kind, item.ConditionCode))
            .ToArray();

    private static JsonDocument Request(string taskCode, Guid releaseId)
    {
        var requirements = EvidencePolicyCatalog.Require(taskCode).Select(item => new
        {
            code = item.Code,
            kind = item.Kind,
            condition = item.ConditionCode == EvidenceConditionCodes.Always
                ? null
                : new { code = item.ConditionCode },
        });
        return JsonSerializer.SerializeToDocument(new { releaseId, requirements });
    }

    private static DefaultHttpContext AuthenticatedContext()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString("D"))], "synthetic"));
        return context;
    }

    private sealed class RecordingService : IEvidencePolicyService
    {
        public PutEvidencePolicyCommand? Command { get; private set; }
        public Exception? Exception { get; set; }
        public Exception? RejectionException { get; set; }
        public int RejectionCount { get; private set; }

        public Task<EvidencePolicyVersionDetails> PutAsync(
            PutEvidencePolicyCommand command,
            CancellationToken cancellationToken = default)
        {
            Command = command;
            if (Exception is not null)
            {
                throw Exception;
            }

            var requirements = EvidencePolicyCatalog.Require(command.TaskCode)
                .Select(item => new EvidenceRequirementVersionDetails(
                    item.Code,
                    item.Kind,
                    item.ConditionCode == EvidenceConditionCodes.Always ? null : new EvidenceConditionDetails(item.ConditionCode),
                    true,
                    item.Ordinal))
                .ToArray();
            return Task.FromResult(new EvidencePolicyVersionDetails(
                Guid.CreateVersion7(),
                command.TaskCode,
                Guid.CreateVersion7(),
                command.ReleaseId,
                1,
                "BORRADOR",
                null,
                null,
                null,
                null,
                null,
                requirements,
                1));
        }

        public Task RecordRejectionAsync(
            Guid actorUserId,
            Guid correlationId,
            CancellationToken cancellationToken = default)
        {
            if (RejectionException is not null)
            {
                throw RejectionException;
            }

            RejectionCount++;
            return Task.CompletedTask;
        }
    }
}
