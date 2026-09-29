using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Idempotency;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed partial class GenerationRequestPersistenceTests
{
    private static async Task<GenerationRequestDetails> SeedLegacyRequestAsync(SgolDbContext context, Scenario scenario)
    {
        var command = Command(scenario, Guid.CreateVersion7()) with { InputPayload = null };
        var actor = command.ActorUserId.ToString("D");
        var scope = IdempotencyProtocol.Scope(actor, "GENERATION_REQUEST_CREATE", "new:LOR-001");
        var hash = IdempotencyProtocol.HashCanonical("GENERATION_REQUEST_CREATE", actor, "new:LOR-001",
            new { command.RuleVersionId, command.BranchId, command.PeriodId, command.OriginType, command.OriginReference });
        var request = new GenerationRequest(Guid.CreateVersion7(), command.IdempotencyKey, hash, command.RuleVersionId,
            command.BranchId, command.PeriodId, command.OriginType, command.OriginReference, command.ActorUserId, Now);
        var details = new GenerationRequestDetails(request.Id, request.RuleVersionId, request.BranchId, request.PeriodId,
            request.OriginType, request.OriginReference, request.Result, request.RequestedBy, request.RequestedAt, null, null);
        context.GenerationRequests.Add(request);
        context.IdempotencyRecords.Add(IdempotencyProtocol.Completed(scope, command.IdempotencyKey, hash, "GENERATION_REQUEST",
            request.Id, 201, details, Now, DateTimeOffset.MaxValue, responseLocation: $"/api/v1/generation-requests/{request.Id:D}"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return details;
    }

    [Fact]
    public async Task LegacyReplayRemainsHistoricalAndBlocksUncomparableNewOrigin()
    {
        var scenario = await ResetAndSeedAsync(CanonicalRole.Direction);
        await using var context = CreateContext();
        var legacy = await SeedLegacyRequestAsync(context, scenario);
        var row = await context.GenerationRequests.AsNoTracking().SingleAsync();
        var service = CreateService(context);
        var recovered = await service.CreateAsync(Command(scenario, row.IdempotencyKey) with { InputPayload = null });
        Assert.Equal(legacy.GenerationRequestId, recovered.GenerationRequestId);
        Assert.Null(recovered.ObligationId);
        Assert.Empty(await service.GetOptionsAsync(scenario.ActorUserId));
        var blocked = await Assert.ThrowsAsync<ManualGenerationException>(() => service.CreateAsync(Command(scenario, Guid.CreateVersion7())));
        Assert.Equal("GENERACION_LEGACY_REQUIERE_REVISION", blocked.Code);
        var oldBody = await Assert.ThrowsAsync<ManualGenerationException>(() => service.CreateAsync(Command(scenario, Guid.CreateVersion7()) with { InputPayload = null }));
        Assert.Equal("GENERATION_REQUEST_SCHEMA_REQUERIDO", oldBody.Code);
        await Assert.ThrowsAsync<GenerationRequestIdempotencyConflictException>(() => service.CreateAsync(Command(scenario, row.IdempotencyKey)));
        Assert.Empty(await context.WorkObligations.ToListAsync());
        Assert.Equal(1, await context.GenerationRequests.CountAsync());
    }

    [Fact]
    public async Task DifferentKeysConcurrentlyRecoverBothStableIdsWithoutDuplicateAuditCreation()
    {
        var scenario = await ResetAndSeedAsync(CanonicalRole.Direction);
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var context = CreateContext();
            return await CreateService(context).CreateAsync(Command(scenario, Guid.CreateVersion7()));
        }));
        Assert.Single(results.Select(r => r.GenerationRequestId).Distinct());
        Assert.Single(results.Select(r => r.ObligationId).Distinct());
        Assert.Single(results, r => r.Result == GenerationRequestResults.Accepted);
        await using var verify = CreateContext();
        Assert.Equal(1, await verify.WorkObligations.CountAsync());
        Assert.Equal(1, await verify.AuditEvents.CountAsync(a => a.Action == "WORK_OBLIGATION_CREATED"));
        Assert.Equal(20, await verify.IdempotencyRecords.CountAsync());
    }

    [Fact]
    public async Task FunctionalIdentityDoesNotSilentlyAcceptDifferentOriginData()
    {
        var scenario = await ResetAndSeedAsync(CanonicalRole.Direction);
        await using var context = CreateContext();
        var service = CreateService(context);
        var command = Command(scenario, Guid.CreateVersion7());
        var first = await service.CreateAsync(command);
        var changed = System.Text.Json.Nodes.JsonNode.Parse(command.InputPayload!.Value.GetRawText())!;
        changed["merchandiseReference"] = "OTHER";
        var conflict = await Assert.ThrowsAsync<ManualGenerationException>(() => service.CreateAsync(command with
        { IdempotencyKey = Guid.CreateVersion7(), InputPayload = JsonSerializer.SerializeToElement(changed) }));
        Assert.Equal("ORIGEN_YA_REGISTRADO", conflict.Code);
        var detail = await service.GetAsync(scenario.ActorUserId, Guid.CreateVersion7(), first.GenerationRequestId);
        Assert.NotNull(detail.InputPayload);
        Assert.Null(first.InputPayload);
        Assert.Equal(first.ObligationId, detail.ObligationId);
        Assert.Equal(1, await context.WorkObligations.CountAsync());
    }

    [Fact]
    public async Task RevokedCreatorCannotRecoverOrReadPersistedInput()
    {
        var scenario = await ResetAndSeedAsync(CanonicalRole.Direction);
        await using var context = CreateContext();
        var service = CreateService(context);
        var command = Command(scenario, Guid.CreateVersion7());
        var first = await service.CreateAsync(command);
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE role_assignment_version SET status = 'SUSTITUIDO', valid_to = {Now.AddHours(1)} WHERE user_id = {scenario.ActorUserId}");
        await Assert.ThrowsAsync<GenerationRequestAccessDeniedException>(() => service.CreateAsync(command));
        await Assert.ThrowsAsync<GenerationRequestNotFoundException>(() => service.GetAsync(scenario.ActorUserId, Guid.CreateVersion7(), first.GenerationRequestId));
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FutureRoleOrEmploymentCannotCreateOrDiscoverManualOrigins(bool futureRole)
    {
        var scenario = await ResetAndSeedAsync(CanonicalRole.Direction);
        await using var context = CreateContext();
        if (futureRole)
            await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE role_assignment_version SET valid_from = {Now.AddDays(1)} WHERE user_id = {scenario.ActorUserId}");
        else
            await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE employment_version SET valid_from = {Now.AddDays(1)} WHERE person_id = (SELECT person_id FROM app_user WHERE id = {scenario.ActorUserId})");
        var service = CreateService(context);
        await Assert.ThrowsAsync<GenerationRequestAccessDeniedException>(() => service.GetOptionsAsync(scenario.ActorUserId));
        await Assert.ThrowsAsync<GenerationRequestAccessDeniedException>(() => service.CreateAsync(Command(scenario, Guid.CreateVersion7())));
        Assert.Empty(await context.GenerationRequests.ToListAsync());
        Assert.Empty(await context.WorkObligations.ToListAsync());
    }
}
