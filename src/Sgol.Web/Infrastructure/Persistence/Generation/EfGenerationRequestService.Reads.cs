using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Generation;

public sealed partial class EfGenerationRequestService
{
    public async Task<IReadOnlyList<ManualGenerationOption>> GetOptionsAsync(Guid actor, CancellationToken token = default)
    {
        var role = await GenerationAuthorizationQuery.GetCreatorRoleAsync(dbContext, actor, clock.UtcNow, token)
            ?? throw new GenerationRequestAccessDeniedException();
        var now = clock.UtcNow;
        var candidates = await (from rule in dbContext.ActivationRuleVersions.AsNoTracking()
                                join task in dbContext.TaskDefinitions on rule.TaskDefinitionId equals task.Id
                                join version in dbContext.TaskDefinitionVersions on rule.TaskDefinitionVersionId equals version.Id
                                join eligibility in dbContext.EligibilityPolicyVersions on version.Id equals eligibility.TaskDefinitionVersionId
                                where rule.Status == VersionStatuses.Current && rule.Mode == ActivationModes.Manual &&
                                    rule.OriginKeySchema == ActivationOriginSchemas.ManualReference &&
                                    version.Status == VersionStatuses.Current && version.TaskDefinitionId == task.Id &&
                                    eligibility.Status == VersionStatuses.Current && eligibility.TaskDefinitionId == task.Id &&
                                    rule.EffectiveFrom <= now && (rule.EffectiveTo == null || rule.EffectiveTo > now) &&
                                    version.EffectiveFrom <= now && (version.EffectiveTo == null || version.EffectiveTo > now) &&
                                    eligibility.EffectiveFrom <= now && (eligibility.EffectiveTo == null || eligibility.EffectiveTo > now)
                                orderby task.TaskCode
                                select new { task.Id, task.TaskCode, task.Name, RuleId = rule.Id, VersionId = version.Id, eligibility.RequiredRole })
            .ToListAsync(token);
        var result = new List<ManualGenerationOption>();
        foreach (var item in candidates)
            if (ManualGenerationInput.TaskCodes.Contains(item.TaskCode) && RoleHierarchy.CanAccessLevel(role, item.RequiredRole) &&
                !await HasLegacyCompetitorAsync(item.Id, item.TaskCode, token))
                result.Add(new(item.TaskCode, item.Name, item.VersionId, item.RuleId, BranchScope.LorettaId, ActivationOriginSchemas.ManualReference));
        return result;
    }

    private sealed record ReceiptCursor(Guid Actor, string? Filter, int PageSize, Guid After, DateTimeOffset ExpiresAt);

    public async Task<ReceiptOriginPage> GetReceiptOriginsAsync(Guid actor, string? receiptReference, string? cursor,
        int pageSize, CancellationToken token = default)
    {
        var role = await GenerationAuthorizationQuery.GetCreatorRoleAsync(dbContext, actor, clock.UtcNow, token)
            ?? throw new GenerationRequestAccessDeniedException();
        var incidentId = TaskDefinitionCatalog.Require("TAR-0093").Id;
        var level = await dbContext.EligibilityPolicyVersions.AsNoTracking()
            .Where(e => e.TaskDefinitionId == incidentId && e.Status == VersionStatuses.Current)
            .Select(e => e.RequiredRole).SingleOrDefaultAsync(token);
        if (level is null || !RoleHierarchy.CanAccessLevel(role, level)) throw new GenerationRequestAccessDeniedException();
        if (pageSize is < 1 or > 100) throw InvalidQuery();
        string? filter;
        try { filter = receiptReference is null ? null : ManualGenerationInput.Reference(receiptReference); }
        catch (ManualGenerationException) { throw InvalidQuery(); }
        var protector = protectionProvider?.CreateProtector("SGOL.FRONT-013.receipt-cursor.v2")
            ?? throw new InvalidOperationException("Data Protection is required for receipt pagination.");
        var after = Guid.Empty;
        if (cursor is not null)
        {
            try
            {
                var saved = JsonSerializer.Deserialize<ReceiptCursor>(protector.Unprotect(cursor)) ?? throw InvalidQuery();
                if (saved.Actor != actor || saved.Filter != filter || saved.PageSize != pageSize || saved.ExpiresAt <= clock.UtcNow)
                    throw InvalidQuery();
                after = saved.After;
            }
            catch (Exception e) when (e is CryptographicException or JsonException or FormatException) { throw InvalidQuery(); }
        }
        var rows = new List<ReceiptOrigin>();
        // Fetch bounded batches; invisible rows never consume a page and never enter the response.
        while (rows.Count <= pageSize)
        {
            var batch = await dbContext.WorkObligations.AsNoTracking()
                .Where(o => o.BranchId == BranchScope.LorettaId && o.ManualTaskCode == "TAR-0092" &&
                    o.ExecutionStatus == WorkObligationStatuses.Pending && o.Id.CompareTo(after) > 0)
                .OrderBy(o => o.Id).Take(100).ToListAsync(token);
            if (batch.Count == 0) break;
            foreach (var item in batch)
            {
                after = item.Id;
                if (item.InputPayload is null) continue;
                var input = item.InputPayload.RootElement.GetProperty("input");
                var receipt = input.GetProperty("receiptReference").GetString()!;
                if (filter is not null && receipt != filter) continue;
                try { await RequireVisibleAsync(actor, item.GenerationRequestId, token); }
                catch (GenerationRequestNotFoundException) { continue; }
                rows.Add(new(item.Id, item.GenerationRequestId, receipt, ManualGenerationInput.Date(input, "startedAt"), item.PeriodId));
                if (rows.Count > pageSize) break;
            }
            if (batch.Count < 100 || rows.Count > pageSize) break;
        }
        var hasNext = rows.Count > pageSize;
        var page = rows.Take(pageSize).ToArray();
        var next = hasNext ? protector.Protect(JsonSerializer.Serialize(new ReceiptCursor(actor, filter, pageSize,
            page[^1].ObligationId, clock.UtcNow.AddMinutes(30)))) : null;
        return new(page, next);
    }
    private static ManualGenerationException InvalidQuery() => new("CONSULTA_ORIGEN_INVALIDA", 400);
}
