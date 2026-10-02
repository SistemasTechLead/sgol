using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Primitives;
using Sgol.Continuity.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Reporting.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Auditing;
using Sgol.Web.Presentation.Continuity;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.Reporting;
using Xunit;

namespace Sgol.UnitTests;

public sealed class Front020PresentationTests
{
    [Theory]
    [InlineData("DIRECCION", true)]
    [InlineData("ADMINISTRACION", false)]
    [InlineData("SUBCOORDINACION", false)]
    [InlineData("PISO_VENTAS", false)]
    public void ExistingQueryPermissionsAreProjectedWithoutGrantingDirectionToOtherRoles(string role, bool direction)
    {
        var permissions = RolePermissionProjection.ForRole(role);
        Assert.Contains("PER-INDICADOR-VER", permissions); Assert.Contains("PER-AUDITORIA-VER", permissions);
        Assert.Equal(direction, permissions.Contains("PER-DIRECCION-VER")); Assert.Equal(direction, permissions.Contains("PER-CONTINUIDAD-VER"));
        Assert.Empty(RolePermissionProjection.ForRole("UNKNOWN"));
    }
    [Theory]
    [InlineData("/indicadores?operationisoYear=2026&operationisoWeek=40", true)]
    [InlineData("/auditoria?from=2026-10-01T00:00:00Z&to=2026-10-02T00:00:00Z", true)]
    [InlineData("/auditoria/eventos/01a00000-0000-7000-8000-000000000001", true)]
    [InlineData("/continuidad/reconciliaciones/01a00000-0000-7000-8000-000000000001", true)]
    [InlineData("/continuidad?reason=secret", false)]
    [InlineData("/auditoria?from=invalid", false)]
    [InlineData("/api/v1/audit-events", false)]
    [InlineData("//external.example", false)]
    public void QueryDestinationsRemainInternalClosedAndRequireCurrentAuthorization(string destination, bool valid)
    {
        var helper = new SafeReturnDestination(new EphemeralDataProtectionProvider()); var token = helper.Protect(destination);
        Assert.Equal(valid, token is not null);
        if (token is null) return;
        Assert.Equal(destination, helper.Read(token, authorizedForCurrentPrincipal: _ => true));
        Assert.Null(helper.Read(token)); Assert.Null(helper.Read(token, authorizedForCurrentPrincipal: _ => false)); Assert.Null(helper.Read(token + "altered", authorizedForCurrentPrincipal: _ => true));
    }
    [Theory]
    [InlineData("operationisoYear=2026&operationisoWeek=40", true)]
    [InlineData("operationisoYear=1&operationisoWeek=1", false)]
    [InlineData("directionisoYear=1&directionisoWeek=1", true)]
    [InlineData("operationisoYear=2026", false)]
    [InlineData("operationisoYear=2026&operationisoWeek=54", false)]
    [InlineData("operationisoYear=2026&operationisoWeek=40&operationlevel=OTHER", false)]
    [InlineData("operationlimit=101", false)]
    [InlineData("operationlimit=25&operationlimit=25", false)]
    [InlineData("money=1", false)]
    public void IndicatorFiltersAreClosedAndPeriodsRemainExplicit(string query, bool expected) => Assert.Equal(expected, IndicatorQuery.TryRead(new QueryCollection(Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(query)), out _));
    [Theory]
    [InlineData("from=2026-10-01T00:00:00Z&to=2026-10-02T00:00:00Z", true)]
    [InlineData("from=2026-10-01T00:00:00Z&to=2026-11-02T00:00:00Z", false)]
    [InlineData("from=2026-10-01T00:00:00Z&to=2026-10-01T00:00:00Z", false)]
    [InlineData("from=2026-10-01T00:00:00Z", false)]
    [InlineData("resourceType=password", false)]
    [InlineData("", true)]
    [InlineData("from=2026-10-01T00:00:00Z&to=2026-10-02T00:00:00Z&mode=trace", false)]
    [InlineData("from=2026-10-01T00:00:00Z&to=2026-10-02T00:00:00Z&mode=other", false)]
    public void AuditRequiresUtcIntervalAndRejectsIncompatibleOrUnknownFilters(string query, bool expected) => Assert.Equal(expected, AuditQuery.TryRead(new QueryCollection(Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(query)), out _));
    [Fact]
    public void IndicatorZeroIsValidAndAnomalyIsNeverConvertedToZero()
    {
        var zero = new IndicatorSnapshot(new(2026, 40, new(2026, 9, 28), new(2026, 10, 4), "America/Mexico_City"),
            new("LOR-001", "PISO_VENTAS", ["PISO_VENTAS"], null, null), 0, new(0, 0), new(0, 0), new(0, 0), new(0, 0), new(0, []));
        IndicatorPresentation.Validate(zero);
        Assert.Throws<ApiProtocolException>(() => IndicatorPresentation.Validate(zero with { Validated = new(1, 0) }));
        Assert.Throws<ApiProtocolException>(() => IndicatorPresentation.Validate(zero with { Pending = new(0, 1) }));
        var direction = zero with { BaseObligationsCount = 3, Pending = new(3, 3), Concluded = new(0, 3), Validated = new(0, 3), NonCompliant = new(0, 3), ActiveLoadByPerson = new(3, []) };
        IndicatorPresentation.Validate(direction); // Unassigned obligations do not create a synthetic person or sixth indicator.
    }
    [Theory]
    [InlineData("REQUESTED", false)]
    [InlineData("REFERENCE_CAPTURING", false)]
    [InlineData("REFERENCE_READY", false)]
    [InlineData("RESTORE_STARTED", false)]
    [InlineData("RECONCILING", false)]
    [InlineData("MATCHED", true)]
    [InlineData("DIFFERENT", false)]
    [InlineData("FAILED", false)]
    [InlineData("APPROVED", false)]
    [InlineData("UNKNOWN", false)]
    public void OnlyConfirmedMatchedReportCanOfferApproval(string status, bool expected)
    {
        var data = Report(status); Assert.Equal(expected, ContinuityPresentation.CanApprove(data));
        Assert.False(ContinuityPresentation.CanApprove(data with { DifferencesTruncated = true }));
        Assert.False(ContinuityPresentation.CanApprove(data with { ObservedRpoSeconds = null }));
        Assert.False(ContinuityPresentation.CanApprove(data with { ObservedRtoSeconds = 14401 }));
        Assert.False(ContinuityPresentation.CanApprove(data with { DifferenceCount = 1 }));
    }
    [Theory]
    [InlineData("", false)]
    [InlineData(" x ", true)]
    [InlineData("<secret>", false)]
    [InlineData("a\nb", false)]
    [InlineData("a\tb", true)]
    public void ReasonUsesExistingNormalizationAndBoundaries(string reason, bool valid) => Assert.Equal(valid, ContinuityPresentation.Reason(reason) is not null);
    [Fact]
    public void ExtensiveDifferenceReportRemainsCompleteWithoutInventedPagination()
    {
        var rows = Enumerable.Range(1, 100000).Select(i => new RecoveryDifferenceDetails(i, "evidence", "EVIDENCE_VERSION", "SYNTHETIC-" + i, null, "IDENTITY_MISSING", null, null)).ToArray();
        var report = Report("DIFFERENT") with { DifferenceCount = rows.Length, Differences = rows };
        ContinuityPresentation.Validate(report, report.ReconciliationId, "\"2\""); Assert.Equal(100000, report.Differences.Count); Assert.False(ContinuityPresentation.CanApprove(report));
    }
    [Fact]
    public void IntentionIsBoundToActorResourceAndExpirationAndNeverLogsReason()
    {
        var protector = new ContinuityIntentionProtector(new EphemeralDataProtectionProvider()); var actor = Guid.NewGuid(); var report = Report("MATCHED");
        var intent = new ContinuityIntention(actor, report.ReconciliationId, Guid.NewGuid(), "\"2\"", "Motivo sintético", DateTimeOffset.UtcNow.AddHours(1), report);
        var token = protector.Protect(intent);
        Assert.Equal(intent.Key, protector.Read(token, actor, report.ReconciliationId, DateTimeOffset.UtcNow)!.Key);
        Assert.Null(protector.Read(token, Guid.NewGuid(), report.ReconciliationId, DateTimeOffset.UtcNow));
        Assert.Null(protector.Read(token, actor, Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.Null(protector.Read(token, actor, report.ReconciliationId, DateTimeOffset.UtcNow.AddHours(2)));
        Assert.Null(protector.Read(token + "tampered", actor, report.ReconciliationId, DateTimeOffset.UtcNow));
        Assert.DoesNotContain(intent.Reason, intent.ToString(), StringComparison.Ordinal);
    }
    [Fact]
    public async Task PreparationAndCancellationNeverQueryOrMutateContinuity()
    {
        var session = new Session(); var api = new NoApi(); var protection = new EphemeralDataProtectionProvider();
        var context = new DefaultHttpContext(); context.Request.Path = "/continuidad";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues> { ["reason"] = "Motivo sintético", ["__RequestVerificationToken"] = "synthetic" });
        var page = new Sgol.Web.Pages.Continuity.IndexModel(session, api, protection) { PageContext = new PageContext { HttpContext = context } };
        Assert.IsType<PageResult>(await page.OnPostPrepareAsync(null, default)); Assert.NotNull(page.Prepared);
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues> { ["intention"] = page.Prepared });
        var cancelled = new Sgol.Web.Pages.Continuity.IndexModel(session, api, protection) { PageContext = new PageContext { HttpContext = context } };
        await cancelled.OnPostCancelAsync(null, default); Assert.True(cancelled.Cancelled); Assert.Null(cancelled.Prepared); Assert.Equal(0, api.Calls);
    }
    [Fact]
    public async Task IndicatorItemEnvelopePreservesCountCursorAndCutWithoutInventingCollection()
    {
        var correlation = Guid.CreateVersion7().ToString("D"); using var handler = new Handler(JsonSerializer.Serialize(new { data = new { activeLoadByPerson = new { items = Array.Empty<object>() } }, meta = new { count = 0, nextCursor = "synthetic-next", queriedAt = "2026-10-01T00:00:00Z", correlationId = correlation } }));
        using var http = new HttpClient(handler); var context = new DefaultHttpContext(); context.Request.Scheme = "https"; context.Request.Host = new("sgol.example");
        var result = await new SgolApiClient(http, new HttpContextAccessor { HttpContext = context }).SendAsync<JsonElement>(new(HttpMethod.Get, "/api/v1/indicators", ApiResponseShape.Item));
        Assert.Equal(0, result.Count); Assert.Equal("synthetic-next", result.NextCursor); Assert.NotNull(result.QueriedAt); Assert.Null(result.Items);
    }
    private static RecoveryReconciliationDetails Report(string status) => new(Guid.NewGuid(), Sgol.Organization.Contracts.BranchScope.LorettaId, status, DateTimeOffset.UtcNow, null, 2, null, null, 0, false, 0, 10, null, null, []);
    private sealed class Handler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }
    private sealed class NoApi : ISgolApiClient
    {
        public int Calls { get; private set; }
        public Task<ApiResponse<T>> SendAsync<T>(ApiRequest request, CancellationToken cancellationToken = default) { Calls++; throw new InvalidOperationException("No API expected."); }
    }
    private sealed class Session : IRazorSessionState
    {
        private readonly Guid actor = Guid.NewGuid();
        public bool IsInvalid => false;
        public void Invalidate() { }
        public Task<SessionSnapshot?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<SessionSnapshot?>(new(actor, Guid.NewGuid(), "synthetic", "Persona sintética", "LOR-001", "DIRECCION", ["PER-CONTINUIDAD-VER"], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddHours(8)));
    }
}
