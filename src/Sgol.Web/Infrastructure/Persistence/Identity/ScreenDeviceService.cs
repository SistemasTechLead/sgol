using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Identity;

public sealed class ScreenDeviceException(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
}

public sealed class ScreenDeviceService(SgolDbContext db, IDirectionPreparedPlanService direction, IClock clock)
{
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static string Secret() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    private static ScreenDeviceException Invalid() => new(401, "SCREEN_CREDENTIAL_INVALID");

    public async Task<ScreenLinkRequest> RequestLinkAsync(string code, string hash, string name, CancellationToken ct)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        name = (name ?? "").Trim();
        if (code.Length > 64 || !Regex.IsMatch(hash ?? "", "^[A-F0-9]{64}$") || name.Length is < 1 or > 120 || name.Any(char.IsControl))
            throw new ScreenDeviceException(400, "SCREEN_LINK_INVALID");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Lock(ct);
        var account = await db.Set<ScreenAccount>().SingleOrDefaultAsync(x => x.Code == code && x.BranchId == BranchScope.LorettaId, ct);
        if (account is null || account.Status == ScreenAccount.Active) throw new ScreenDeviceException(409, "SCREEN_NOT_AVAILABLE_FOR_LINK");
        var now = clock.UtcNow;
        var duplicate = await db.Set<ScreenLinkRequest>().SingleOrDefaultAsync(x => x.ScreenId == account.Id &&
            x.CredentialHash == hash && x.ExpiresAt > now && x.ApprovedAt == null && x.CancelledAt == null, ct);
        if (duplicate is not null) { await tx.CommitAsync(ct); return duplicate; }
        if (await db.Set<ScreenLinkRequest>().CountAsync(x => x.ScreenId == account.Id && x.ExpiresAt > now && x.ApprovedAt == null && x.CancelledAt == null, ct) >= 5)
            throw new ScreenDeviceException(429, "SCREEN_LINK_LIMIT");
        var request = new ScreenLinkRequest
        {
            Id = Guid.CreateVersion7(), ScreenId = account.Id, CredentialHash = hash!, DeviceName = name,
            ApprovalCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(5)), CreatedAt = now, ExpiresAt = now.AddMinutes(15)
        };
        db.Add(request);
        Audit(account.Id, null, "SCREEN_LINK_REQUESTED", new { request.Id, request.DeviceName, request.ExpiresAt });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return request;
    }

    public async Task<ScreenAccount> ApproveAsync(Guid actor, string approvalCode, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Lock(ct);
        await direction.AuthorizeAsync(actor, ct);
        var code = approvalCode.Trim().ToUpperInvariant();
        var request = await db.Set<ScreenLinkRequest>().SingleOrDefaultAsync(x => x.ApprovalCode == code, ct);
        if (request is null || request.ExpiresAt <= clock.UtcNow || request.ApprovedAt != null || request.CancelledAt != null)
            throw new ScreenDeviceException(409, "SCREEN_LINK_EXPIRED_OR_USED");
        var account = await db.Set<ScreenAccount>().SingleAsync(x => x.Id == request.ScreenId, ct);
        if (account.Status == ScreenAccount.Active) throw new ScreenDeviceException(409, "SCREEN_ALREADY_LINKED");
        account.CredentialHash = request.CredentialHash;
        account.SessionStamp = Guid.CreateVersion7();
        account.LinkedAt = clock.UtcNow;
        account.DeviceName = request.DeviceName;
        account.Status = ScreenAccount.Active;
        request.ApprovedAt = clock.UtcNow;
        Audit(account.Id, actor, "SCREEN_LINK_APPROVED", new { request.Id, account.DeviceName, account.Status });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return account;
    }

    public async Task RevokeAsync(Guid actor, string code, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) throw new ArgumentException("Escribe un motivo.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Lock(ct);
        await direction.AuthorizeAsync(actor, ct);
        var account = await db.Set<ScreenAccount>().SingleAsync(x => x.Code == code.Trim().ToUpperInvariant(), ct);
        account.CredentialHash = null;
        account.SessionStamp = null;
        account.Status = ScreenAccount.Revoked;
        Audit(account.Id, actor, "SCREEN_ACCESS_REVOKED", new { account.Code, account.Status }, reason.Trim());
        // Cancel every outstanding pairing attempt when revoking a device.
        await db.Set<ScreenLinkRequest>().Where(x => x.ScreenId == account.Id && x.ApprovedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CancelledAt, clock.UtcNow), ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<string?> IssueTicketAsync(Guid id, string secret, CancellationToken ct)
    {
        if (!Regex.IsMatch(secret ?? "", "^[A-Za-z0-9_-]{43}$")) throw Invalid();
        var hash = Hash(secret!);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Lock(ct);
        var account = await db.Set<ScreenAccount>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (account is null) throw Invalid();
        if (account.Status != ScreenAccount.Active || account.CredentialHash is null || account.SessionStamp is null ||
            !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(hash), Encoding.ASCII.GetBytes(account.CredentialHash)))
        {
            if (account.Status != ScreenAccount.Active && await db.Set<ScreenLinkRequest>().AnyAsync(x => x.ScreenId == id &&
                x.CredentialHash == hash && x.ApprovedAt == null && x.CancelledAt == null && x.ExpiresAt > clock.UtcNow, ct))
            { await tx.CommitAsync(ct); return null; }
            throw Invalid();
        }
        var value = Secret();
        db.Add(new ScreenBrowserTicket
        {
            Id = Guid.CreateVersion7(), ScreenId = id, SessionStamp = account.SessionStamp.Value,
            TicketHash = Hash(value), ExpiresAt = clock.UtcNow.AddSeconds(60)
        });
        await db.Set<ScreenBrowserTicket>().Where(x => x.ExpiresAt < clock.UtcNow.AddDays(-1)).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return value;
    }

    public async Task<ScreenAccount> RedeemAsync(string value, CancellationToken ct)
    {
        if (!Regex.IsMatch(value ?? "", "^[A-Za-z0-9_-]{43}$")) throw Invalid();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Lock(ct);
        var hash = Hash(value!);
        var ticket = await db.Set<ScreenBrowserTicket>().SingleOrDefaultAsync(x => x.TicketHash == hash, ct);
        if (ticket is null || ticket.UsedAt != null || ticket.ExpiresAt <= clock.UtcNow) throw Invalid();
        var account = await ValidateAsync(ticket.ScreenId, ticket.SessionStamp, ct) ?? throw Invalid();
        ticket.UsedAt = clock.UtcNow;
        Audit(account.Id, null, "SCREEN_SESSION_STARTED", new { ticket.Id });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return account;
    }

    public Task<ScreenAccount?> ValidateAsync(Guid id, Guid stamp, CancellationToken ct) => db.Set<ScreenAccount>().AsNoTracking()
        .SingleOrDefaultAsync(x => x.Id == id && x.BranchId == BranchScope.LorettaId && x.Purpose == ScreenAccount.BoardReadOnly &&
            x.Status == ScreenAccount.Active && x.SessionStamp == stamp && x.CredentialHash != null, ct);

    private Task<int> Lock(CancellationToken ct) => db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(71652026101901)", ct);
    private void Audit(Guid id, Guid? actor, string action, object data, string? reason = null) => db.AuditEvents.Add(new AuditEvent
    {
        Id = Guid.CreateVersion7(), OccurredAt = clock.UtcNow, ActorType = actor is null ? "DEVICE" : "USER",
        ActorUserId = actor, Action = action, ResourceType = "SCREEN_ACCOUNT", ResourceId = id,
        BranchId = BranchScope.LorettaId, CorrelationId = Guid.CreateVersion7(),
        AfterData = JsonSerializer.SerializeToDocument(data), Reason = reason, Outcome = "SUCCESS"
    });
}
