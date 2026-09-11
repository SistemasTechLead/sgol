using Sgol.Assignment.Contracts;
using Sgol.Configuration.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Notifications.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Reporting.Contracts;
using Sgol.Validation.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Assignment;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Execution;
using Sgol.Web.Infrastructure.Persistence.Evidence;
using Sgol.Web.Infrastructure.Persistence.Generation;
using Sgol.Web.Infrastructure.Persistence.Identity;
using Sgol.JobInfrastructure;
using Sgol.Web.Infrastructure.Persistence.Organization;
using Sgol.Web.Infrastructure.Persistence.Notifications;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Infrastructure.Persistence.Versioning;
using Sgol.Web.Infrastructure.Persistence.Validation;
using Sgol.Web.Infrastructure.Evidence;

namespace Sgol.Web.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddSgolPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSgolJobInfrastructure(configuration);
        services.AddScoped<AuditTransaction>();
        services.AddScoped<VersioningTransaction>();
        services.AddScoped<EfConfigurationReleaseService>();
        services.AddScoped<IConfigurationReleaseService>(provider => provider.GetRequiredService<EfConfigurationReleaseService>());
        services.AddScoped<ITaskDefinitionService, EfTaskDefinitionService>();
        services.AddScoped<IEligibilityPolicyService, EfEligibilityPolicyService>();
        services.AddScoped<IActivationPolicyService, EfActivationPolicyService>();
        services.AddScoped<IEvidencePolicyService, EfEvidencePolicyService>();
        services.AddScoped<IValidationPolicyService, EfValidationPolicyService>();
        services.AddScoped<ICalendarService, EfCalendarService>();
        services.AddScoped<IWeekPeriodService, EfWeekPeriodService>();
        services.AddScoped<IWorkPlanService, EfWorkPlanService>();
        services.AddScoped<IPlanPublicationService, EfPlanPublicationService>();
        services.AddScoped<IGenerationRequestService, EfGenerationRequestService>();
        services.AddScoped<IWorkObligationMaterializer, EfWorkObligationMaterializer>();
        services.AddScoped<IEligibilityEvaluationService, EfEligibilityEvaluationService>();
        services.AddScoped<IAutomaticAssignmentService, EfAutomaticAssignmentService>();
        services.AddScoped<IAssignmentCorrectionService, EfAssignmentCorrectionService>();
        services.AddScoped<IActiveLoadReader, EfActiveLoadReader>();
        services.AddScoped<IObligationQueryReader, EfObligationQueryReader>();
        services.AddScoped<IHierarchySupervisionReader>(provider =>
            provider.GetRequiredService<IObligationQueryReader>() as IHierarchySupervisionReader
            ?? throw new InvalidOperationException("The obligation reader must provide hierarchical supervision."));
        EvidenceInfrastructureServiceCollectionExtensions.AddFailClosedAdapters(services);
        services.AddScoped<IEvidenceContributionService, EfEvidenceContributionService>();
        services.AddScoped<IEvidenceReviewService, EfEvidenceReviewService>();
        services.AddScoped<IEvidenceConclusionReviewService, EfEvidenceConclusionReviewService>();
        services.AddScoped<IObligationConclusionService, EfObligationConclusionService>();
        services.AddScoped<IValidationRequirementWriter, EfValidationRequirementWriter>();
        services.AddScoped<IValidationDecisionService, EfValidationDecisionService>();
        services.AddScoped<IInboxReader, EfInboxReader>();
        services.AddScoped<IInternalNoticeService, EfInternalNoticeService>();
        services.AddScoped<IInternalNoticeWriter, EfInternalNoticeWriter>();
        services.AddDirectionBootstrap();
        services.AddScoped<IBranchCatalogReader, EfBranchCatalogReader>();
        services.AddScoped<IPersonAdministrationService, EfPersonAdministrationService>();
        services.AddScoped<IAvailabilityAdministrationService, EfAvailabilityAdministrationService>();
        services.AddScoped<IAccountAdministrationService, EfAccountAdministrationService>();
        services.AddScoped<EfRoleAssignmentService>();
        services.AddScoped<IRoleAssignmentService>(provider => provider.GetRequiredService<EfRoleAssignmentService>());
        services.AddScoped<IRoleHierarchyResolver>(provider => provider.GetRequiredService<EfRoleAssignmentService>());

        return services;
    }
}
