using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Cv02Demo;

internal sealed record SeededActor(Guid PersonId, Guid UserId, string StableCode, string RoleCode);

internal sealed record SeededActors(
    SeededActor Direction,
    SeededActor Administration,
    SeededActor SubcoordA,
    SeededActor SubcoordB,
    SeededActor Floor,
    SeededActor Outside);

internal sealed record SeededConfiguration(
    Guid ReleaseId,
    IReadOnlyDictionary<string, Guid> TaskVersionIds,
    IReadOnlyDictionary<string, Guid> RuleIds,
    IReadOnlyDictionary<string, Guid> PolicyIds,
    Guid PeriodId);

internal static class Cv02Seed
{
    public static async Task<SeededActors> ActorsAsync(
        Cv02Database database,
        bool candidatesAvailable,
        CancellationToken cancellationToken)
    {
        await using var scope = database.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var direction = AddActor(context, database, "DIR-CV02", CanonicalRole.Direction, available: true);
        var administration = AddActor(context, database, "ADMIN-CV02", CanonicalRole.Administration, available: true);
        var subcoordA = AddActor(context, database, "SUB-A-CV02", CanonicalRole.Subcoordination, candidatesAvailable);
        var subcoordB = AddActor(context, database, "SUB-B-CV02", CanonicalRole.Subcoordination, candidatesAvailable);
        var floor = AddActor(context, database, "PISO-A-CV02", CanonicalRole.SalesFloor, available: true);
        var outside = AddActor(context, database, "OUTSIDE-CV02", CanonicalRole.SalesFloor, available: true);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();
        return new SeededActors(direction, administration, subcoordA, subcoordB, floor, outside);
    }

    public static async Task<SeededConfiguration> DirectConfigurationAsync(
        Cv02Database database,
        SeededActors actors,
        CancellationToken cancellationToken)
    {
        await using var scope = database.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var releaseId = database.UuidGenerator.NewUuid();
        var release = new ConfigurationRelease(releaseId, BranchScope.LorettaId);
        release.ApplyPublished(
            Published(releaseId),
            versionNo: 1,
            actors.Direction.UserId,
            Cv02Timeline.ConfigurationNow);

        var taskVersions = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var ruleIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var policyIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var taskCode in new[] { "TAR-0005", "TAR-0007", "TAR-0008" })
        {
            var task = TaskDefinitionCatalog.Require(taskCode);
            var taskVersionId = database.UuidGenerator.NewUuid();
            var taskVersion = new TaskDefinitionVersion(
                taskVersionId,
                task.Id,
                versionNo: 1,
                schemaVersion: 1,
                JsonDocument.Parse("{}"),
                releaseId);
            taskVersion.ApplyPublished(Published(taskVersionId), activeForNew: true);

            var policyId = database.UuidGenerator.NewUuid();
            var policy = new EligibilityPolicyVersion(
                policyId,
                task.Id,
                taskVersionId,
                releaseId,
                basedOnId: null,
                versionNo: 1,
                EligibilityPolicyCatalog.RequireRole(taskCode),
                requiresAvailability: true,
                requiredShift: null);
            policy.ApplyPublished(Published(policyId));

            var ruleId = database.UuidGenerator.NewUuid();
            using var schedule = CreateSchedule(taskCode);
            var activation = ActivationPolicyCatalog.Require(taskCode);
            var rule = new ActivationRuleVersion(
                ruleId,
                task.Id,
                taskVersionId,
                releaseId,
                basedOnId: null,
                versionNo: 1,
                activation.Mode,
                schedule.RootElement,
                activation.OriginKeySchema);
            rule.ApplyPublished(Published(ruleId));

            context.TaskDefinitionVersions.Add(taskVersion);
            context.EligibilityPolicyVersions.Add(policy);
            context.ActivationRuleVersions.Add(rule);
            taskVersions[taskCode] = taskVersionId;
            policyIds[taskCode] = policyId;
            ruleIds[taskCode] = ruleId;
        }

        var working = new CalendarDayVersion(
            database.UuidGenerator.NewUuid(),
            BranchScope.LorettaId,
            Cv02Timeline.WorkingDate,
            CalendarContract.WorkingDay,
            isWorkingDay: true,
            releaseId,
            "Calendario sintético CV-02");
        working.ApplyPublished(Published(working.Id));
        var nonWorking = new CalendarDayVersion(
            database.UuidGenerator.NewUuid(),
            BranchScope.LorettaId,
            Cv02Timeline.NonWorkingDate,
            CalendarContract.Holiday,
            isWorkingDay: false,
            releaseId,
            "Calendario sintético CV-02");
        nonWorking.ApplyPublished(Published(nonWorking.Id));

        var week = WeekContract.Calculate(2026, 53);
        var period = new WeekPeriod(
            database.UuidGenerator.NewUuid(),
            BranchScope.LorettaId,
            2026,
            53,
            week.StartsOn,
            week.EndsOn,
            WeekContract.Current);

        context.ConfigurationReleases.Add(release);
        context.CalendarDayVersions.AddRange(working, nonWorking);
        context.WeekPeriods.Add(period);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();
        return new SeededConfiguration(releaseId, taskVersions, ruleIds, policyIds, period.Id);
    }

    public static async Task<(GenerationRequestDetails Request, WorkObligationDetails Obligation)> ManualObligationAsync(
        Cv02Database database,
        SeededActors actors,
        SeededConfiguration configuration,
        string taskCode,
        string origin,
        Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var scope = database.Services.CreateAsyncScope();
        var requestService = scope.ServiceProvider.GetRequiredService<IGenerationRequestService>();
        var materializer = scope.ServiceProvider.GetRequiredService<IWorkObligationMaterializer>();
        var request = await requestService.CreateAsync(new CreateGenerationRequestCommand(
            actors.Direction.UserId,
            idempotencyKey,
            database.UuidGenerator.NewUuid(),
            configuration.RuleIds[taskCode],
            BranchScope.LorettaId,
            configuration.PeriodId,
            ActivationOriginSchemas.ManualReference,
            origin), cancellationToken);
        var obligation = await materializer.MaterializeAsync(new MaterializeWorkObligationCommand(
            request.GenerationRequestId,
            database.UuidGenerator.NewUuid()), cancellationToken);
        return (request, obligation);
    }

    public static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static VersionRecord Published(Guid id) => new(
        id,
        VersionStatuses.Current,
        Cv02Timeline.EffectiveFrom,
        EffectiveTo: null,
        "Configuración sintética CV-02",
        SupersedesId: null,
        RowVersion: 2);

    public static JsonDocument CreateSchedule(string taskCode)
    {
        if (taskCode == "TAR-0005")
        {
            return JsonDocument.Parse(
                """{"kind":"WORKING_DAY_WINDOWS","workingDaysOnly":true,"localTimes":["12:00","17:00"],"timeZone":"America/Mexico_City"}""");
        }

        if (taskCode == "TAR-0026")
        {
            return JsonDocument.Parse(
                """{"kind":"BUSINESS_DAYS_BEFORE_DUE_DATE","businessDaysBefore":3,"localTime":"08:30","timeZone":"America/Mexico_City","adjustDueDateToPreviousBusinessDay":true}""");
        }

        return JsonDocument.Parse("null");
    }

    private static SeededActor AddActor(
        SgolDbContext context,
        Cv02Database database,
        string stableCode,
        string roleCode,
        bool available)
    {
        var personId = database.UuidGenerator.NewUuid();
        var userId = database.UuidGenerator.NewUuid();
        context.People.Add(new Person
        {
            Id = personId,
            StableCode = stableCode,
            DisplayName = $"Persona sintética {stableCode}",
            CreatedAt = Cv02Timeline.ConfigurationNow,
        });
        context.EmploymentVersions.Add(new EmploymentVersion(
            database.UuidGenerator.NewUuid(),
            personId,
            BranchScope.LorettaId,
            EmploymentStatus.Active,
            Cv02Timeline.ConfigurationNow,
            positionText: $"Puesto sintético {roleCode}"));
        context.AppUsers.Add(new AppUser
        {
            Id = userId,
            PersonId = personId,
            Status = AccountStatus.Active,
            MustChangePassword = false,
            MfaEnrolledAt = Cv02Timeline.ConfigurationNow,
            SecurityStamp = $"cv02-{stableCode}",
        });
        context.IdentityCredentials.Add(new IdentityCredential
        {
            UserId = userId,
            UserName = $"cv02.{stableCode.ToLowerInvariant()}",
            NormalizedUserName = $"CV02.{stableCode}",
            PasswordHash = "synthetic-not-a-login-secret",
        });
        context.RoleAssignmentVersions.Add(new RoleAssignmentVersion
        {
            Id = database.UuidGenerator.NewUuid(),
            UserId = userId,
            BranchId = BranchScope.LorettaId,
            RoleCode = roleCode,
            Status = RoleAssignmentStatus.Active,
            ValidFrom = Cv02Timeline.ConfigurationNow,
        });
        context.AvailabilityDayVersions.AddRange(
            new AvailabilityDayVersion(
                database.UuidGenerator.NewUuid(), personId, BranchScope.LorettaId,
                Cv02Timeline.WorkingDate, available, userId),
            new AvailabilityDayVersion(
                database.UuidGenerator.NewUuid(), personId, BranchScope.LorettaId,
                Cv02Timeline.NonWorkingDate, available, userId));
        return new SeededActor(personId, userId, stableCode, roleCode);
    }
}
