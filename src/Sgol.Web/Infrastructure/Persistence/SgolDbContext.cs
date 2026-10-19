using Microsoft.EntityFrameworkCore;
using Sgol.Assignment.Contracts;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Notifications.Contracts;
using Sgol.Validation.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Assignment;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Generation;
using Sgol.Web.Infrastructure.Persistence.Evidence;
using Sgol.Web.Infrastructure.Persistence.Execution;
using Sgol.JobInfrastructure;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Infrastructure.Persistence.Validation;

namespace Sgol.Web.Infrastructure.Persistence;

public sealed class SgolDbContext(DbContextOptions<SgolDbContext> options, IConfiguration? configuration = null) : DbContext(options)
{
    public bool LegacyWorkflowEnabled => configuration?.GetValue<bool>("LegacyWorkflow:Enabled") == true;
    public DbSet<OperationalAttachment> OperationalAttachments => Set<OperationalAttachment>();
    public DbSet<StoredPlanCapacityAdjustment> PlanCapacityAdjustments => Set<StoredPlanCapacityAdjustment>();

    public DbSet<OperationalPhotograph> OperationalPhotographs => Set<OperationalPhotograph>();
    public DbSet<OperationalCaptureSession> OperationalCaptureSessions => Set<OperationalCaptureSession>();
    public DbSet<Reporting.ReportTemplate> ReportTemplates => Set<Reporting.ReportTemplate>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<Person> People => Set<Person>();

    public DbSet<EmploymentVersion> EmploymentVersions => Set<EmploymentVersion>();

    public DbSet<AvailabilityDayVersion> AvailabilityDayVersions => Set<AvailabilityDayVersion>();

    public DbSet<AppUser> AppUsers => Set<AppUser>();

    public DbSet<IdentityCredential> IdentityCredentials => Set<IdentityCredential>();

    public DbSet<RoleAssignmentVersion> RoleAssignmentVersions => Set<RoleAssignmentVersion>();

    public DbSet<AuthenticationChallenge> AuthenticationChallenges => Set<AuthenticationChallenge>();

    public DbSet<MfaTotpCredential> MfaTotpCredentials => Set<MfaTotpCredential>();

    public DbSet<MfaRecoveryCode> MfaRecoveryCodes => Set<MfaRecoveryCode>();

    public DbSet<DirectionBootstrapMarker> DirectionBootstrapMarkers => Set<DirectionBootstrapMarker>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public DbSet<ConfigurationRelease> ConfigurationReleases => Set<ConfigurationRelease>();

    public DbSet<CalendarDayVersion> CalendarDayVersions => Set<CalendarDayVersion>();

    public DbSet<TaskDefinition> TaskDefinitions => Set<TaskDefinition>();

    public DbSet<TaskDefinitionVersion> TaskDefinitionVersions => Set<TaskDefinitionVersion>();

    public DbSet<EligibilityPolicyVersion> EligibilityPolicyVersions => Set<EligibilityPolicyVersion>();

    public DbSet<ActivationRuleVersion> ActivationRuleVersions => Set<ActivationRuleVersion>();

    public DbSet<EvidencePolicyVersion> EvidencePolicyVersions => Set<EvidencePolicyVersion>();

    public DbSet<EvidenceRequirementVersion> EvidenceRequirementVersions => Set<EvidenceRequirementVersion>();

    public DbSet<ValidationPolicyVersion> ValidationPolicyVersions => Set<ValidationPolicyVersion>();

    public DbSet<WeekPeriod> WeekPeriods => Set<WeekPeriod>();

    public DbSet<WorkPlan> WorkPlans => Set<WorkPlan>();

    public DbSet<DirectionPreparedPlan> DirectionPreparedPlans => Set<DirectionPreparedPlan>();
    public DbSet<DirectionPreparedPlanItem> DirectionPreparedPlanItems => Set<DirectionPreparedPlanItem>();
    public DbSet<OperationalTask> OperationalTasks => Set<OperationalTask>();
    public DbSet<OperationalAssignment> OperationalAssignments => Set<OperationalAssignment>();
    public DbSet<OperationalCapacity> OperationalCapacities => Set<OperationalCapacity>();
    public DbSet<OperationalProgressEntry> OperationalProgressEntries => Set<OperationalProgressEntry>();

    public DbSet<PlanVersion> PlanVersions => Set<PlanVersion>();

    public DbSet<PlanVersionObligation> PlanVersionObligations => Set<PlanVersionObligation>();

    public DbSet<GenerationRequest> GenerationRequests => Set<GenerationRequest>();

    public DbSet<WorkObligation> WorkObligations => Set<WorkObligation>();

    public DbSet<FileObject> FileObjects => Set<FileObject>();

    public DbSet<EvidenceItem> EvidenceItems => Set<EvidenceItem>();

    public DbSet<EvidenceVersion> EvidenceVersions => Set<EvidenceVersion>();

    public DbSet<EvidenceReviewSnapshot> EvidenceReviewSnapshots => Set<EvidenceReviewSnapshot>();

    public DbSet<ExecutionResult> ExecutionResults => Set<ExecutionResult>();

    public DbSet<InternalNotice> InternalNotices => Set<InternalNotice>();

    public DbSet<ValidationRequirement> ValidationRequirements => Set<ValidationRequirement>();

    public DbSet<ValidationDecisionVersion> ValidationDecisionVersions => Set<ValidationDecisionVersion>();

    public DbSet<EligibilityEvaluation> EligibilityEvaluations => Set<EligibilityEvaluation>();

    public DbSet<EligibilityCandidate> EligibilityCandidates => Set<EligibilityCandidate>();

    public DbSet<AssignmentVersion> AssignmentVersions => Set<AssignmentVersion>();

    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    public DbSet<ScheduledJobRun> ScheduledJobRuns => Set<ScheduledJobRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new Identity.ScreenAccountConfiguration());
        var screenDevices = new Identity.ScreenDeviceConfiguration();
        modelBuilder.ApplyConfiguration<Identity.ScreenLinkRequest>(screenDevices);
        modelBuilder.ApplyConfiguration<Identity.ScreenBrowserTicket>(screenDevices);
        OperationalSequenceSchema.Configure(modelBuilder);
        PreparedProcessStart.Configure(modelBuilder);
        modelBuilder.ApplyConfiguration(new OperationalAttachmentConfiguration());
        modelBuilder.ApplyConfiguration(new StoredPlanCapacityAdjustmentConfiguration());
        modelBuilder.ApplyConfiguration(new PlanCapacityPreviousSegmentConfiguration());
        modelBuilder.ApplyConfiguration(new Reporting.ReportTemplateConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        modelBuilder.ApplyConfiguration(new DirectionPreparedPlanConfiguration());
        modelBuilder.ApplyConfiguration(new DirectionPreparedPlanItemConfiguration());
        modelBuilder.ApplyConfiguration(new OperationalTaskConfiguration());
        var schedules = new PersonalScheduleConfiguration();
        modelBuilder.ApplyConfiguration<PersonalScheduleVersion>(schedules);
        modelBuilder.ApplyConfiguration<PersonalScheduleException>(schedules);
        modelBuilder.ApplyConfiguration<ClosingRotationVersion>(schedules);
        modelBuilder.ApplyConfiguration<ClosingCoverageVersion>(schedules);
        modelBuilder.ApplyConfiguration<TemporalAssignmentState>(schedules);
        modelBuilder.ApplyConfiguration(new OperationalOccasionConfiguration());
        modelBuilder.ApplyConfiguration(new OperationalReferenceFileConfiguration());
        modelBuilder.ApplyConfiguration(new OperationalCaptureSessionConfiguration());
        modelBuilder.ApplyConfiguration(new OperationalPhotographConfiguration());
        modelBuilder.ApplyConfiguration(new OperationalAssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new OperationalCapacityConfiguration());
        modelBuilder.ApplyConfiguration(new OperationalProgressConfiguration());
        modelBuilder.ApplyConfiguration(new BranchConfiguration());
        modelBuilder.ApplyConfiguration(new PersonConfiguration());
        modelBuilder.ApplyConfiguration(new Sgol.Web.Infrastructure.Persistence.Organization.SpecializationConfiguration());
        modelBuilder.ApplyConfiguration(new Sgol.Web.Infrastructure.Persistence.Organization.PersonSpecializationConfiguration());
        modelBuilder.ApplyConfiguration(new EmploymentVersionConfiguration());
        modelBuilder.ApplyConfiguration(new AvailabilityDayVersionConfiguration());
        modelBuilder.ApplyConfiguration(new AppUserConfiguration());
        modelBuilder.ApplyConfiguration(new IdentityCredentialConfiguration());
        modelBuilder.ApplyConfiguration(new RoleAssignmentVersionConfiguration());
        modelBuilder.ApplyConfiguration(new AuthenticationChallengeConfiguration());
        modelBuilder.ApplyConfiguration(new MfaTotpCredentialConfiguration());
        modelBuilder.ApplyConfiguration(new MfaRecoveryCodeConfiguration());
        modelBuilder.ApplyConfiguration(new DirectionBootstrapMarkerConfiguration());
        modelBuilder.ApplyConfiguration(new IdempotencyRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ConfigurationReleaseConfiguration());
        modelBuilder.ApplyConfiguration(new CalendarDayVersionConfiguration());
        modelBuilder.ApplyConfiguration(new TaskDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new TaskDefinitionVersionConfiguration());
        modelBuilder.ApplyConfiguration(new EligibilityPolicyVersionConfiguration());
        modelBuilder.ApplyConfiguration(new ActivationRuleVersionConfiguration());
        modelBuilder.ApplyConfiguration(new EvidenceRequirementCatalogConfiguration());
        modelBuilder.ApplyConfiguration(new EvidencePolicyVersionConfiguration());
        modelBuilder.ApplyConfiguration(new EvidenceRequirementVersionConfiguration());
        modelBuilder.ApplyConfiguration(new ValidationPolicyVersionConfiguration());
        modelBuilder.ApplyConfiguration(new WeekPeriodConfiguration());
        modelBuilder.ApplyConfiguration(new WorkPlanConfiguration());
        modelBuilder.ApplyConfiguration(new PlanVersionConfiguration());
        modelBuilder.ApplyConfiguration(new PlanVersionObligationConfiguration());
        modelBuilder.ApplyConfiguration(new GenerationRequestConfiguration());
        modelBuilder.ApplyConfiguration(new WorkObligationConfiguration());
        modelBuilder.ApplyConfiguration(new FileObjectConfiguration());
        modelBuilder.ApplyConfiguration(new EvidenceItemConfiguration());
        modelBuilder.ApplyConfiguration(new EvidenceVersionConfiguration());
        modelBuilder.ApplyConfiguration(new EvidenceVersionFileConfiguration());
        modelBuilder.ApplyConfiguration(new EvidenceReviewSnapshotConfiguration());
        modelBuilder.ApplyConfiguration(new ExecutionResultConfiguration());
        modelBuilder.ApplyConfiguration(new Notifications.InternalNoticeConfiguration());
        modelBuilder.ApplyConfiguration(new ValidationRequirementConfiguration());
        modelBuilder.ApplyConfiguration(new ValidationDecisionVersionConfiguration());
        modelBuilder.ApplyConfiguration(new EligibilityEvaluationConfiguration());
        modelBuilder.ApplyConfiguration(new EligibilityCandidateConfiguration());
        modelBuilder.ApplyConfiguration(new AssignmentVersionConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxEventConfiguration());
        modelBuilder.ApplyConfiguration(new ScheduledJobRunConfiguration());
    }
}
