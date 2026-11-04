using Microsoft.EntityFrameworkCore;
using Sgol.Organization.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

// Immutable receipt. Corrections require a separate reviewed operation; availability alone
// must never undo an incidence's economic or operational consequences.
public sealed class OperationalIncidence
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public DateOnly Day { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset At { get; set; }
    public Guid IntentKey { get; set; }
    public string IntentHash { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string Reason { get; set; } = "";
    public string ReceiptJson { get; set; } = "{}";
}

public sealed class OperationalIncidenceDisposition
{
    public Guid Id { get; set; }
    public Guid IncidenceId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid? StageId { get; set; }
    public Guid? ObligationId { get; set; }
    public Guid PersonId { get; set; }
    public DateOnly EvaluationDay { get; set; }
    public Guid? RecipientId { get; set; }
    public string Action { get; set; } = "";
    public bool OverloadAuthorized { get; set; }
    public string BeforeJson { get; set; } = "{}";
    public string AfterJson { get; set; } = "{}";
}

public static class OperationalIncidencePersistence
{
    public static void Configure(ModelBuilder model)
    {
        var i = model.Entity<OperationalIncidence>();
        i.ToTable("operational_incidence"); i.HasKey(x => x.Id);
        i.HasIndex(x => new { x.PersonId, x.Day }).IsUnique();
        i.HasIndex(x => new { x.ActorId, x.IntentKey }).IsUnique();
        i.Property(x => x.IntentHash).HasMaxLength(64);
        i.Property(x => x.Fingerprint).HasMaxLength(64);
        i.Property(x => x.Reason).HasMaxLength(1000);
        i.Property(x => x.ReceiptJson).HasColumnType("jsonb");
        i.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        var d = model.Entity<OperationalIncidenceDisposition>();
        d.ToTable("operational_incidence_disposition"); d.HasKey(x => x.Id);
        d.HasIndex(x => new { x.IncidenceId, x.AssignmentId }).IsUnique();
        d.HasIndex(x => x.ObligationId);
        d.Property(x => x.Action).HasMaxLength(32);
        d.Property(x => x.BeforeJson).HasColumnType("jsonb");
        d.Property(x => x.AfterJson).HasColumnType("jsonb");
        d.HasOne<OperationalIncidence>().WithMany().HasForeignKey(x => x.IncidenceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed record IncidenceDecision(Guid AssignmentId, string Action, Guid? RecipientId = null, bool AuthorizeOverload = false);
public sealed record IncidenceRequest(Guid PersonId, DateOnly Day, Guid Key, string Reason,
    DateTimeOffset NotBefore, IncidenceDecision[] Decisions, bool Reflow = false);
public sealed record IncidenceCase(Guid EntityId, string Task, string Cause, string Effect, string[] Options, string[] Details);
public sealed record IncidenceChange(OperationalAssignment Before, OperationalAssignment? After, string Action,
    Guid? ObligationId, DateOnly EvaluationDay, Guid? StageId = null, bool OverloadAuthorized = false)
{
    public decimal ExcessMinutes { get; init; }
    public string[] Overlaps { get; init; } = [];
    public Guid? PreparedStartId { get; init; }
    public string[] OverloadDays { get; init; } = [];
}
public sealed record IncidenceBalance(Guid PersonId, string Name, decimal Capacity, decimal Before, decimal After);
public sealed record IncidencePersonSummary(Guid PersonId, bool Available, int PendingTasks, int OverdueTasks, decimal PendingMinutes);
public sealed record IncidenceBonusDecision(Guid AssignmentId, Guid? ObligationId, Guid? StageId, Guid PersonId,
    Guid? RecipientId, DateOnly Day, string Action, DateTimeOffset At);
public sealed record IncidencePreview(IncidenceRequest Request, string PersonName, DateTimeOffset CalculatedAt,
    string Fingerprint, IncidenceChange[] Changes, IncidenceCase[] Cases, IncidenceBalance[] Balances)
{
    public bool CanApply => Cases.Length == 0;
}
