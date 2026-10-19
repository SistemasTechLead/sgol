using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Identity;

public sealed class ScreenAccountService(SgolDbContext db, IDirectionPreparedPlanService direction, IClock clock)
{
    public async Task<ScreenAccount> CreateAsync(Guid actor, string code, string name, string reason,
        CancellationToken token = default)
    {
        code = code.Trim().ToUpperInvariant();
        name = name.Trim();
        reason = reason.Trim();
        if (!Regex.IsMatch(code, "^[A-Z][A-Z0-9-]{2,63}$", RegexOptions.CultureInvariant) ||
            name.Length is < 1 or > 120 || reason.Length is < 1 or > 1000)
            throw new ArgumentException("Código, nombre o motivo de la pantalla inválido.");

        await using var tx = await db.Database.BeginTransactionAsync(token);
        // Serializes provisioning and duplicate retries; does not take the planning lock.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(71652026101901)", token);
        // Use the existing Direction authorization; a screen can never administer accounts.
        await direction.AuthorizeAsync(actor, token);
        var existing = await db.Set<ScreenAccount>().AsNoTracking().SingleOrDefaultAsync(x => x.Code == code, token);
        if (existing is not null)
        {
            if (existing.DisplayName != name || existing.BranchId != BranchScope.LorettaId)
                throw new InvalidOperationException("El código de pantalla ya existe con otros datos.");
            await tx.CommitAsync(token);
            return existing;
        }

        var account = new ScreenAccount
        {
            Id = Guid.CreateVersion7(), BranchId = BranchScope.LorettaId, Code = code,
            DisplayName = name, CreatedAt = clock.UtcNow, CreatedBy = actor
        };
        // No person, employment, role, password, token, capacity or assignment is created.
        db.Set<ScreenAccount>().Add(account);
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.CreateVersion7(), OccurredAt = account.CreatedAt, ActorUserId = actor,
            ActorType = "USER", Action = "SCREEN_ACCOUNT_CREATED", ResourceType = "SCREEN_ACCOUNT",
            ResourceId = account.Id, BranchId = account.BranchId, CorrelationId = Guid.CreateVersion7(),
            AfterData = JsonSerializer.SerializeToDocument(new
            {
                account.Code, account.DisplayName, account.Purpose, account.Status,
                CanReceiveTasks = false, CountsForCapacity = false
            }),
            Reason = reason, Outcome = "SUCCESS"
        });
        await db.SaveChangesAsync(token);
        await tx.CommitAsync(token);
        return account;
    }
}
