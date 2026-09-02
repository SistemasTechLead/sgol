using Sgol.BuildingBlocks.Versioning;

namespace Sgol.Configuration.Contracts;

public static class ConfigurationAuthorization
{
    public const string Administer = "PER-CONFIG-ADMIN";
}

public sealed class ConfigurationRelease : IVersionedEntity
{
    private ConfigurationRelease()
    {
    }

    public ConfigurationRelease(Guid id, Guid branchId)
    {
        var draft = VersioningRules.CreateDraft(id);
        Id = draft.Id;
        BranchId = branchId;
        Status = draft.Status;
        RowVersion = draft.RowVersion;
    }

    public Guid Id { get; private init; }

    public Guid BranchId { get; private init; }

    public int? VersionNo { get; private set; }

    public string Status { get; private set; } = null!;

    public DateTimeOffset? EffectiveFrom { get; private set; }

    public DateTimeOffset? EffectiveTo { get; private set; }

    public string? Reason { get; private set; }

    public Guid? PublishedBy { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public Guid? SupersedesId { get; private set; }

    public long RowVersion { get; private set; }

    public void ApplyPublished(VersionRecord published, int versionNo, Guid actorUserId, DateTimeOffset publishedAt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(versionNo, 1);
        if (published.Id != Id || published.Status != VersionStatuses.Current)
        {
            throw new VersioningStateException("The publication plan does not match this release.");
        }

        Apply(published);
        VersionNo = versionNo;
        PublishedBy = actorUserId;
        PublishedAt = publishedAt;
    }

    public void ApplySuperseded(VersionRecord superseded)
    {
        if (superseded.Id != Id || superseded.Status != VersionStatuses.Superseded)
        {
            throw new VersioningStateException("The substitution plan does not match this release.");
        }

        Apply(superseded);
    }

    public VersionRecord ToVersionRecord() => new(
        Id,
        Status,
        EffectiveFrom,
        EffectiveTo,
        Reason,
        SupersedesId,
        RowVersion);

    private void Apply(VersionRecord version)
    {
        Status = version.Status;
        EffectiveFrom = version.EffectiveFrom;
        EffectiveTo = version.EffectiveTo;
        Reason = version.Reason;
        SupersedesId = version.SupersedesId;
        RowVersion = version.RowVersion;
    }
}

public sealed record ConfigurationReleaseDetails(
    Guid Id,
    int? VersionNo,
    string Status,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? Reason,
    Guid? PublishedBy,
    DateTimeOffset? PublishedAt,
    Guid? SupersedesId,
    long RowVersion);

public sealed record CreateConfigurationReleaseCommand(
    Guid ActorUserId,
    Guid IdempotencyKey,
    Guid CorrelationId);

public sealed record PublishConfigurationReleaseCommand(
    Guid ActorUserId,
    Guid IdempotencyKey,
    Guid CorrelationId,
    Guid ReleaseId,
    long ExpectedRowVersion,
    DateTimeOffset EffectiveFrom,
    string Reason);

public interface IConfigurationReleaseService
{
    Task<IReadOnlyList<ConfigurationReleaseDetails>> ListAsync(
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationReleaseDetails> CreateDraftAsync(
        CreateConfigurationReleaseCommand command,
        CancellationToken cancellationToken = default);

    Task<ConfigurationReleaseDetails> PublishAsync(
        PublishConfigurationReleaseCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class ConfigurationAccessDeniedException()
    : Exception($"{ConfigurationAuthorization.Administer} is required for LOR-001.");

public sealed class ConfigurationReleaseNotFoundException() : Exception("The configuration release does not exist.");

public sealed class ConfigurationIdempotencyConflictException()
    : Exception("The idempotency key was already used with different content.");
