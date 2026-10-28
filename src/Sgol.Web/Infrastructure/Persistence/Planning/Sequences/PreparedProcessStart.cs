using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class PreparedProcessStart
{
    public string? AgendaJson { get; set; }
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public Guid ProcessId { get; set; }
    public Guid VersionId { get; set; }
    public string StartKey { get; set; }="";
    public DateTimeOffset StartsAt { get; set; }
    public string Participants { get; set; }="{}";
    public string Status { get; set; }="PREPARADO";
    public long Revision { get; set; }=1;
    public static void Configure(ModelBuilder model)
    {
        var b=model.Entity<PreparedProcessStart>();
        b.ToTable("operational_process_plan_start",t=>t.HasCheckConstraint("CK_process_plan_start","\"Status\" IN ('PREPARADO','ACTIVADO') AND \"Revision\" > 0"));
        b.HasKey(p=>p.Id); b.HasIndex(p=>p.StartKey).IsUnique(); b.Property(p=>p.StartKey).HasMaxLength(160);
        b.Property(p=>p.AgendaJson).HasColumnType("jsonb");
        b.Property(p=>p.Participants).HasColumnType("jsonb"); b.Property(p=>p.Status).HasMaxLength(16); b.Property(p=>p.Revision).IsConcurrencyToken();
        b.HasOne<DirectionPreparedPlan>().WithMany().HasForeignKey(p=>p.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperationalSequenceVersion>().WithMany().HasForeignKey(p=>new {p.ProcessId,p.VersionId}).HasPrincipalKey(v=>new {v.SequenceId,v.Id}).OnDelete(DeleteBehavior.Restrict);
    }
}
