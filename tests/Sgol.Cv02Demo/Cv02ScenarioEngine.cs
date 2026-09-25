using System.Text.Json;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.JobInfrastructure;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Generation;
using Sgol.Worker;

namespace Sgol.Cv02Demo;

internal sealed class Cv02ScenarioEngine(Cv02Database database)
{
    public async Task<DemoScenarioResult> RunAsync(
        string scenarioId,
        CancellationToken cancellationToken = default)
    {
        _ = ScenarioCatalog.Require(scenarioId);
        var startedAt = DateTimeOffset.UtcNow;
        await database.ResetAsync(cancellationToken);
        database.Clock.Set(Cv02Timeline.ConfigurationNow);

        var facts = scenarioId switch
        {
            "S01" => await PublishConfigurationAsync(cancellationToken),
            "S02" => await ManualReplayAsync(cancellationToken),
            "S03" => await WorkingRecurrenceAsync(cancellationToken),
            "S04" => await NonWorkingRecurrenceAsync(cancellationToken),
            "S05" => await ExplainAndAssignAsync(cancellationToken),
            "S06" => await NoCandidateAsync(cancellationToken),
            "S07" => await CorrectAssignmentAsync(cancellationToken),
            "S08" => await PublishIncrementallyAsync(cancellationToken),
            "S09" => await FullReplayAsync(cancellationToken),
            "S10" => await LateObligationAsync(cancellationToken),
            "S11" => await RejectOutsideHierarchyAsync(cancellationToken),
            "S12" => await RecoverPartialBatchAsync(cancellationToken),
            "S13" => await ConfirmCutLimitsAsync(cancellationToken),
            _ => throw new DemoScenarioAssertionException(),
        };

        return new DemoScenarioResult(
            scenarioId,
            "PASSED",
            "El escenario terminó con el resultado esperado.",
            "CV02_SCENARIO_PASSED",
            startedAt,
            DateTimeOffset.UtcNow,
            facts);
    }

    private async Task<IReadOnlyDictionary<string, string>> PublishConfigurationAsync(
        CancellationToken cancellationToken)
    {
        var actors = await Cv02Seed.ActorsAsync(database, candidatesAvailable: true, cancellationToken);
        await using var scope = database.Services.CreateAsyncScope();
        var releaseService = scope.ServiceProvider.GetRequiredService<IConfigurationReleaseService>();
        var taskService = scope.ServiceProvider.GetRequiredService<ITaskDefinitionService>();
        var policyService = scope.ServiceProvider.GetRequiredService<IEligibilityPolicyService>();
        var activationService = scope.ServiceProvider.GetRequiredService<IActivationPolicyService>();
        var calendarService = scope.ServiceProvider.GetRequiredService<ICalendarService>();

        var definitionRelease = await releaseService.CreateDraftAsync(new(
            actors.Direction.UserId,
            database.UuidGenerator.NewUuid(),
            database.UuidGenerator.NewUuid()), cancellationToken);
        using var emptyPayload = JsonDocument.Parse("{}");
        foreach (var task in TaskDefinitionCatalog.All)
        {
            await taskService.CreateVersionAsync(new CreateTaskDefinitionVersionCommand(
                actors.Direction.UserId,
                database.UuidGenerator.NewUuid(),
                database.UuidGenerator.NewUuid(),
                task.TaskCode,
                definitionRelease.Id,
                1,
                emptyPayload.RootElement.Clone()), cancellationToken);
        }

        await releaseService.PublishAsync(new PublishConfigurationReleaseCommand(
            actors.Direction.UserId,
            database.UuidGenerator.NewUuid(),
            database.UuidGenerator.NewUuid(),
            definitionRelease.Id,
            definitionRelease.RowVersion,
            Cv02Timeline.EffectiveFrom.AddDays(-1),
            "Definiciones sintéticas CV-02"), cancellationToken);

        var currentDefinitions = (await taskService.ListAsync(
                actors.Direction.UserId,
                database.UuidGenerator.NewUuid(),
                cancellationToken))
            .ToDictionary(item => item.TaskCode, item => item.Current!.Id, StringComparer.Ordinal);
        var policyRelease = await releaseService.CreateDraftAsync(new(
            actors.Direction.UserId,
            database.UuidGenerator.NewUuid(),
            database.UuidGenerator.NewUuid()), cancellationToken);

        foreach (var task in TaskDefinitionCatalog.All)
        {
            await policyService.PutAsync(new PutEligibilityPolicyCommand(
                actors.Direction.UserId,
                database.UuidGenerator.NewUuid(),
                database.UuidGenerator.NewUuid(),
                task.TaskCode,
                policyRelease.Id,
                EligibilityPolicyCatalog.RequireRole(task.TaskCode),
                RequiresAvailability: true,
                RequiredShift: null,
                ExpectedRowVersion: null), cancellationToken);
            using var schedule = Cv02Seed.CreateSchedule(task.TaskCode);
            var activation = ActivationPolicyCatalog.Require(task.TaskCode);
            await activationService.PutAsync(new PutActivationPolicyCommand(
                actors.Direction.UserId,
                database.UuidGenerator.NewUuid(),
                database.UuidGenerator.NewUuid(),
                task.TaskCode,
                currentDefinitions[task.TaskCode],
                policyRelease.Id,
                activation.Mode,
                schedule.RootElement.Clone(),
                activation.OriginKeySchema,
                ExpectedRowVersion: null), cancellationToken);
        }

        await calendarService.PutAsync(new PutCalendarDayCommand(
            actors.Direction.UserId,
            database.UuidGenerator.NewUuid(),
            database.UuidGenerator.NewUuid(),
            Cv02Timeline.WorkingDate,
            policyRelease.Id,
            CalendarContract.WorkingDay,
            IsWorkingDay: true,
            "Laborable sintético CV-02",
            ExpectedRowVersion: null), cancellationToken);
        await calendarService.PutAsync(new PutCalendarDayCommand(
            actors.Direction.UserId,
            database.UuidGenerator.NewUuid(),
            database.UuidGenerator.NewUuid(),
            Cv02Timeline.NonWorkingDate,
            policyRelease.Id,
            CalendarContract.Holiday,
            IsWorkingDay: false,
            "Inhábil sintético CV-02",
            ExpectedRowVersion: null), cancellationToken);
        await releaseService.PublishAsync(new PublishConfigurationReleaseCommand(
            actors.Direction.UserId,
            database.UuidGenerator.NewUuid(),
            database.UuidGenerator.NewUuid(),
            policyRelease.Id,
            policyRelease.RowVersion,
            Cv02Timeline.EffectiveFrom,
            "Configuración funcional CV-02"), cancellationToken);

        database.Clock.Set(Cv02Timeline.WorkingCutoff);
        var currentDays = await calendarService.GetAsync(
            actors.Direction.UserId,
            database.UuidGenerator.NewUuid(),
            Cv02Timeline.NonWorkingDate,
            Cv02Timeline.NonWorkingDate,
            cancellationToken);
        Require(currentDays.Single().DayType == CalendarContract.Holiday);
        var successorRelease = await releaseService.CreateDraftAsync(new(
            actors.Direction.UserId,
            database.UuidGenerator.NewUuid(),
            database.UuidGenerator.NewUuid()), cancellationToken);
        await calendarService.PutAsync(new PutCalendarDayCommand(
            actors.Direction.UserId,
            database.UuidGenerator.NewUuid(),
            database.UuidGenerator.NewUuid(),
            Cv02Timeline.NonWorkingDate,
            successorRelease.Id,
            CalendarContract.ExtraordinaryClosure,
            IsWorkingDay: false,
            "Sustitución histórica CV-02",
            ExpectedRowVersion: null), cancellationToken);
        await releaseService.PublishAsync(new PublishConfigurationReleaseCommand(
            actors.Direction.UserId,
            database.UuidGenerator.NewUuid(),
            database.UuidGenerator.NewUuid(),
            successorRelease.Id,
            successorRelease.RowVersion,
            Cv02Timeline.EffectiveFrom.AddDays(7),
            "Calendario sucesor CV-02"), cancellationToken);

        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var taskId = TaskDefinitionCatalog.Require("TAR-0005").Id;
        var calendarHistory = await context.CalendarDayVersions.AsNoTracking()
            .Where(item => item.LocalDate == Cv02Timeline.NonWorkingDate)
            .OrderBy(item => item.EffectiveFrom)
            .ToArrayAsync(cancellationToken);
        Require(calendarHistory.Length == 2);
        Require(calendarHistory[0].Status == VersionStatuses.Superseded);
        Require(calendarHistory[1].Status == VersionStatuses.Current);
        Require(await context.ActivationRuleVersions.AsNoTracking().CountAsync(
            item => item.TaskDefinitionId == taskId && item.Status == VersionStatuses.Current,
            cancellationToken) == 1);
        Require(await context.EligibilityPolicyVersions.AsNoTracking().CountAsync(
            item => item.TaskDefinitionId == taskId && item.Status == VersionStatuses.Current,
            cancellationToken) == 1);

        return Facts(
            ("definiciones", "8"),
            ("reglasActivacion", "8"),
            ("politicasElegibilidad", "8"),
            ("historialCalendario", "SUSTITUIDA→VIGENTE"));
    }

    private async Task<IReadOnlyDictionary<string, string>> ManualReplayAsync(
        CancellationToken cancellationToken)
    {
        var (actors, configuration) = await DirectSeedAsync(candidatesAvailable: true, cancellationToken);
        var key = database.UuidGenerator.NewUuid();
        var first = await Cv02Seed.ManualObligationAsync(
            database, actors, configuration, "TAR-0007", "CV02-MANUAL-001", key, cancellationToken);
        var replay = await Cv02Seed.ManualObligationAsync(
            database, actors, configuration, "TAR-0007", "CV02-MANUAL-001", key, cancellationToken);
        Require(first.Request.Result == GenerationRequestResults.Accepted);
        Require(replay.Request.Result == GenerationRequestResults.Recovered);
        Require(first.Request.GenerationRequestId == replay.Request.GenerationRequestId);
        Require(first.Obligation.ObligationId == replay.Obligation.ObligationId);

        await using var scope = database.Services.CreateAsyncScope();
        var requestService = scope.ServiceProvider.GetRequiredService<IGenerationRequestService>();
        await RequireThrowsAsync<GenerationRequestIdempotencyConflictException>(() =>
            requestService.CreateAsync(new CreateGenerationRequestCommand(
                actors.Direction.UserId,
                key,
                database.UuidGenerator.NewUuid(),
                configuration.RuleIds["TAR-0007"],
                BranchScope.LorettaId,
                configuration.PeriodId,
                ActivationOriginSchemas.ManualReference,
                "CV02-MANUAL-CONFLICT"), cancellationToken));
        var snapshot = await new Cv02DemoReader(database).ReadAsync(cancellationToken);
        Require(snapshot.GenerationRequests == 1 && snapshot.Obligations == 1);
        return Facts(("solicitudes", "1"), ("obligaciones", "1"), ("reintento", "RECUPERADA"));
    }

    private async Task<IReadOnlyDictionary<string, string>> WorkingRecurrenceAsync(
        CancellationToken cancellationToken)
    {
        await DirectSeedAsync(candidatesAvailable: true, cancellationToken);
        database.Clock.Set(Cv02Timeline.WorkingCutoff);
        var exitCode = await RunWorkerAsync(Cv02Timeline.WorkingCutoff, faultGate: null, cancellationToken);
        Require(exitCode == 0);
        var snapshot = await new Cv02DemoReader(database).ReadAsync(cancellationToken);
        Require(snapshot.GenerationRequests == 2);
        Require(snapshot.Obligations == 2);
        Require(snapshot.ScheduledRuns == 1);
        return Facts(("ventanas", "12:00,17:00"), ("solicitudes", "2"), ("actor", "SYSTEM"));
    }

    private async Task<IReadOnlyDictionary<string, string>> NonWorkingRecurrenceAsync(
        CancellationToken cancellationToken)
    {
        await DirectSeedAsync(candidatesAvailable: true, cancellationToken);
        database.Clock.Set(Cv02Timeline.WorkingCutoff);
        Require(await RunWorkerAsync(Cv02Timeline.WorkingCutoff, null, cancellationToken) == 0);
        var before = await new Cv02DemoReader(database).ReadAsync(cancellationToken);
        database.Clock.Set(Cv02Timeline.NonWorkingCutoff);
        Require(await RunWorkerAsync(Cv02Timeline.NonWorkingCutoff, null, cancellationToken) == 0);
        var after = await new Cv02DemoReader(database).ReadAsync(cancellationToken);
        Require(after.GenerationRequests == before.GenerationRequests);
        Require(after.Obligations == before.Obligations);

        await using var scope = database.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        Require(await context.AuditEvents.AsNoTracking().CountAsync(
            item => item.Action == "RECURRENCE_OMITTED",
            cancellationToken) == 2);
        return Facts(("fechaInhabil", Cv02Timeline.NonWorkingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), ("hechosNuevos", "0"));
    }

    private async Task<IReadOnlyDictionary<string, string>> ExplainAndAssignAsync(
        CancellationToken cancellationToken)
    {
        var (actors, configuration) = await DirectSeedAsync(candidatesAvailable: true, cancellationToken);
        var chain = await CreateEvaluateAssignAsync(
            actors, configuration, "TAR-0008", "CV02-ASSIGN-001", cancellationToken);
        Require(chain.Evaluation.Result == EligibilityResults.EligibleCandidates);
        Require(chain.Assignment.Result == AutomaticAssignmentResults.Created);
        Require(chain.Assignment.WinnerPersonId == actors.SubcoordA.PersonId);
        Require(chain.Evaluation.Candidates.Count(item => item.IsEligible) == 2);
        return Facts(("elegibles", "2"), ("ganador", actors.SubcoordA.StableCode), ("explicacion", "persistida"));
    }

    private async Task<IReadOnlyDictionary<string, string>> NoCandidateAsync(
        CancellationToken cancellationToken)
    {
        var (actors, configuration) = await DirectSeedAsync(candidatesAvailable: false, cancellationToken);
        var chain = await CreateEvaluateAssignAsync(
            actors, configuration, "TAR-0008", "CV02-NO-CANDIDATE", cancellationToken);
        Require(chain.Evaluation.Result == EligibilityResults.NoEligibleCandidate);
        Require(chain.Assignment.Result == AutomaticAssignmentResults.NoEligibleCandidate);
        Require(chain.Assignment.AssignmentId is null && chain.Assignment.WinnerPersonId is null);
        var snapshot = await new Cv02DemoReader(database).ReadAsync(cancellationToken);
        Require(snapshot.Assignments == 0);
        return Facts(("resultado", EligibilityResults.NoEligibleCandidate), ("asignaciones", "0"));
    }

    private async Task<IReadOnlyDictionary<string, string>> CorrectAssignmentAsync(
        CancellationToken cancellationToken)
    {
        var (actors, configuration) = await DirectSeedAsync(candidatesAvailable: true, cancellationToken);
        var chain = await CreateEvaluateAssignAsync(
            actors, configuration, "TAR-0008", "CV02-CORRECTION", cancellationToken);
        await using var scope = database.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var obligation = await context.WorkObligations.AsNoTracking()
            .SingleAsync(item => item.Id == chain.Obligation.ObligationId, cancellationToken);
        var result = await scope.ServiceProvider.GetRequiredService<IAssignmentCorrectionService>()
            .CorrectAsync(new CorrectAssignmentCommand(
                actors.Administration.UserId,
                database.UuidGenerator.NewUuid(),
                database.UuidGenerator.NewUuid(),
                obligation.Id,
                actors.SubcoordB.PersonId,
                chain.Evaluation.EvaluationId,
                "Corrección sintética CV-02",
                obligation.RowVersion), cancellationToken);
        Require(result.NewResponsiblePersonId == actors.SubcoordB.PersonId);
        var statuses = await context.AssignmentVersions.AsNoTracking()
            .Where(item => item.ObligationId == obligation.Id)
            .OrderBy(item => item.AssignedAt)
            .Select(item => item.Status)
            .ToArrayAsync(cancellationToken);
        Require(statuses.SequenceEqual([AssignmentVersionStatuses.Superseded, AssignmentVersionStatuses.Current]));
        return Facts(("historia", "SUSTITUIDA→VIGENTE"), ("motivo", "registrado"));
    }

    private async Task<IReadOnlyDictionary<string, string>> PublishIncrementallyAsync(CancellationToken cancellationToken)
    {
        var (actors, configuration) = await DirectSeedAsync(true, cancellationToken);
        _ = await CreateEvaluateAssignAsync(actors, configuration, "TAR-0008", "CV02-PLAN-INITIAL", cancellationToken);
        await using var scope = database.Services.CreateAsyncScope();
        var plans = scope.ServiceProvider.GetRequiredService<IWorkPlanService>();
        var publications = scope.ServiceProvider.GetRequiredService<IPlanPublicationService>();
        var ensureKey = database.UuidGenerator.NewUuid();
        var firstEnsure = await plans.EnsureAsync(new EnsureWorkPlanCommand(
            actors.Administration.UserId, ensureKey, database.UuidGenerator.NewUuid(),
            BranchScope.LorettaId, 2026, 53), cancellationToken);
        var replay = await plans.EnsureAsync(new EnsureWorkPlanCommand(
            actors.Administration.UserId, ensureKey, database.UuidGenerator.NewUuid(),
            BranchScope.LorettaId, 2026, 53), cancellationToken);
        Require(firstEnsure.PlanId == replay.PlanId && replay.Result == WorkPlanResults.Recovered);
        var initial = await publications.PublishAsync(new PublishWorkPlanCommand(
            actors.Administration.UserId, database.UuidGenerator.NewUuid(), database.UuidGenerator.NewUuid(),
            firstEnsure.PlanId, firstEnsure.RowVersion), cancellationToken);
        Require(initial.Result == PlanPublicationResults.Initial && initial.Obligations.Count == 1);

        _ = await CreateEvaluateAssignAsync(actors, configuration, "TAR-0007", "CV02-PLAN-LATE", cancellationToken);
        var incremental = await publications.PublishAsync(new PublishWorkPlanCommand(
            actors.Administration.UserId, database.UuidGenerator.NewUuid(), database.UuidGenerator.NewUuid(),
            firstEnsure.PlanId, initial.RowVersion), cancellationToken);
        Require(incremental.Result == PlanPublicationResults.Incremental);
        Require(incremental.Obligations.Count == 2 && incremental.AddedObligations.Count == 1);

        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var statuses = await context.PlanVersions.AsNoTracking().OrderBy(item => item.VersionNo)
            .Select(item => item.Status).ToArrayAsync(cancellationToken);
        Require(statuses.SequenceEqual([PlanVersionStatuses.Superseded, PlanVersionStatuses.Current]));
        return Facts(("planUnico", firstEnsure.PlanId.ToString("N")), ("versiones", "2"), ("incremento", "1"));
    }

    private async Task<IReadOnlyDictionary<string, string>> FullReplayAsync(CancellationToken cancellationToken)
    {
        var (actors, configuration) = await DirectSeedAsync(true, cancellationToken);
        var key = database.UuidGenerator.NewUuid();
        var concurrentManual = await Task.WhenAll(
            Cv02Seed.ManualObligationAsync(database, actors, configuration, "TAR-0008",
                "CV02-FULL-REPLAY", key, cancellationToken),
            Cv02Seed.ManualObligationAsync(database, actors, configuration, "TAR-0008",
                "CV02-FULL-REPLAY", key, cancellationToken));
        var first = concurrentManual[0];
        var replay = concurrentManual[1];
        Require(first.Request.GenerationRequestId == replay.Request.GenerationRequestId);
        Require(first.Obligation.ObligationId == replay.Obligation.ObligationId);

        EligibilityEvaluationDetails evaluation;
        await using (var scope = database.Services.CreateAsyncScope())
        {
            evaluation = await scope.ServiceProvider.GetRequiredService<IEligibilityEvaluationService>()
                .EvaluateAsync(new EvaluateEligibilityCommand(database.UuidGenerator.NewUuid(),
                    database.UuidGenerator.NewUuid(), first.Obligation.ObligationId,
                    Cv02Timeline.WorkingDate, EligibilityDateSources.ManualRequest), cancellationToken);
        }
        var assignmentKey = database.UuidGenerator.NewUuid();
        var concurrentAssignments = await Task.WhenAll(
            AssignAsync(first.Obligation.ObligationId, evaluation.EvaluationId, assignmentKey, cancellationToken),
            AssignAsync(first.Obligation.ObligationId, evaluation.EvaluationId, assignmentKey, cancellationToken));
        Require(concurrentAssignments.Select(item => item.AssignmentId).Distinct().Count() == 1);

        var planKey = database.UuidGenerator.NewUuid();
        var concurrentPlans = await Task.WhenAll(
            EnsurePlanAsync(actors.Administration.UserId, planKey, cancellationToken),
            EnsurePlanAsync(actors.Administration.UserId, planKey, cancellationToken));
        Require(concurrentPlans.Select(item => item.PlanId).Distinct().Count() == 1);
        database.Clock.Set(Cv02Timeline.WorkingCutoff);
        Require(await RunWorkerAsync(Cv02Timeline.WorkingCutoff, null, cancellationToken) == 0);
        var before = await new Cv02DemoReader(database).ReadAsync(cancellationToken);
        Require(await RunWorkerAsync(Cv02Timeline.WorkingCutoff, null, cancellationToken) == 0);
        var after = await new Cv02DemoReader(database).ReadAsync(cancellationToken);
        Require(after.GenerationRequests == before.GenerationRequests && after.Obligations == before.Obligations);
        Require(after.Evaluations == before.Evaluations && after.Assignments == before.Assignments);
        Require(after.Plans == before.Plans);
        Require(after.Assignments == 3 && after.Plans == 1);
        return Facts(("repeticion", "convergente"),
            ("concurrencia", "PostgreSQL"),
            ("solicitudes", after.GenerationRequests.ToString(CultureInfo.InvariantCulture)),
            ("planes", "1"));
    }

    private async Task<IReadOnlyDictionary<string, string>> LateObligationAsync(CancellationToken cancellationToken)
    {
        _ = await PublishIncrementallyAsync(cancellationToken);
        await using var scope = database.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var versions = await context.PlanVersions.AsNoTracking().OrderBy(item => item.VersionNo)
            .Select(item => new { item.Status, item.SupersedesId }).ToArrayAsync(cancellationToken);
        Require(versions.Length == 2 && versions[0].Status == PlanVersionStatuses.Superseded);
        Require(versions[1].Status == PlanVersionStatuses.Current && versions[1].SupersedesId is not null);
        return Facts(("anterior", "histórica"), ("nueva", "VIGENTE"), ("totalVersiones", "2"));
    }

    private async Task<IReadOnlyDictionary<string, string>> RejectOutsideHierarchyAsync(CancellationToken cancellationToken)
    {
        var (actors, configuration) = await DirectSeedAsync(true, cancellationToken);
        var chain = await CreateEvaluateAssignAsync(
            actors, configuration, "TAR-0008", "CV02-AUTH-NEGATIVE", cancellationToken);
        await using var scope = database.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var obligation = await context.WorkObligations.AsNoTracking()
            .SingleAsync(item => item.Id == chain.Obligation.ObligationId, cancellationToken);
        var plan = await scope.ServiceProvider.GetRequiredService<IWorkPlanService>().EnsureAsync(
            new EnsureWorkPlanCommand(actors.Administration.UserId, database.UuidGenerator.NewUuid(),
                database.UuidGenerator.NewUuid(), BranchScope.LorettaId, 2026, 53), cancellationToken);
        var before = await new Cv02DemoReader(database).ReadAsync(cancellationToken);
        await RequireThrowsAsync<AssignmentCorrectionAccessDeniedException>(() =>
            scope.ServiceProvider.GetRequiredService<IAssignmentCorrectionService>().CorrectAsync(
                new CorrectAssignmentCommand(actors.Outside.UserId, database.UuidGenerator.NewUuid(),
                    database.UuidGenerator.NewUuid(), obligation.Id, actors.SubcoordB.PersonId,
                    chain.Evaluation.EvaluationId, "Intento sintético fuera de jerarquía", obligation.RowVersion),
                cancellationToken));
        await RequireThrowsAsync<PlanPublicationAccessDeniedException>(() =>
            scope.ServiceProvider.GetRequiredService<IPlanPublicationService>().PublishAsync(
                new PublishWorkPlanCommand(actors.Outside.UserId, database.UuidGenerator.NewUuid(),
                    database.UuidGenerator.NewUuid(), plan.PlanId, plan.RowVersion), cancellationToken));
        var after = await new Cv02DemoReader(database).ReadAsync(cancellationToken);
        Require(after.Assignments == before.Assignments);
        Require(after.PlanVersions == before.PlanVersions && after.PlanItems == before.PlanItems);
        return Facts(("correccion", "ACCESO_DENEGADO"), ("publicacion", "ACCESO_DENEGADO"), ("efectosParciales", "0"));
    }

    private async Task<IReadOnlyDictionary<string, string>> RecoverPartialBatchAsync(CancellationToken cancellationToken)
    {
        await DirectSeedAsync(true, cancellationToken);
        database.Clock.Set(Cv02Timeline.WorkingCutoff);
        var gate = new DemoFaultGate { FailSecondOccurrenceOnce = true };
        Require(await RunWorkerAsync(Cv02Timeline.WorkingCutoff, gate, cancellationToken) == 1);
        var partial = await new Cv02DemoReader(database).ReadAsync(cancellationToken);
        Require(partial.GenerationRequests == 1 && partial.Obligations == 1);
        Require(await RunWorkerAsync(Cv02Timeline.WorkingCutoff, gate, cancellationToken) == 0);
        var recovered = await new Cv02DemoReader(database).ReadAsync(cancellationToken);
        Require(recovered.GenerationRequests == 2 && recovered.Obligations == 2);
        return Facts(("antes", "1"), ("despues", "2"), ("faltantesRecuperados", "1"));
    }

    private async Task<IReadOnlyDictionary<string, string>> ConfirmCutLimitsAsync(CancellationToken cancellationToken)
    {
        await DirectSeedAsync(true, cancellationToken);
        database.Clock.Set(Cv02Timeline.WorkingCutoff);
        Require(await RunWorkerAsync(Cv02Timeline.WorkingCutoff, null, cancellationToken) == 0);
        await using var scope = database.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        Require(!await context.PlanVersions.AsNoTracking().AnyAsync(cancellationToken));
        Require(await context.WorkObligations.AsNoTracking().AllAsync(
            item => item.ExecutionStatus == WorkObligationStatuses.Pending, cancellationToken));
        Require(!context.Model.GetEntityTypes().Any(item =>
            item.ClrType.Name.Contains("Evidence", StringComparison.OrdinalIgnoreCase) ||
            item.ClrType.Name.Contains("Validation", StringComparison.OrdinalIgnoreCase)));
        Require(!await context.GenerationRequests.AsNoTracking().AnyAsync(
            item => item.OriginReference.Contains("TAR-0026"), cancellationToken));
        return Facts(("publicacionAutomatica", "0"), ("conclusiones", "0"), ("evidenciasValidaciones", "0"));
    }

    private async Task<(SeededActors Actors, SeededConfiguration Configuration)> DirectSeedAsync(
        bool candidatesAvailable, CancellationToken cancellationToken)
    {
        var actors = await Cv02Seed.ActorsAsync(database, candidatesAvailable, cancellationToken);
        var configuration = await Cv02Seed.DirectConfigurationAsync(database, actors, cancellationToken);
        database.Clock.Set(Cv02Timeline.WorkingCutoff);
        return (actors, configuration);
    }

    private async Task<AssignmentChain> CreateEvaluateAssignAsync(
        SeededActors actors, SeededConfiguration configuration, string taskCode, string origin,
        CancellationToken cancellationToken)
    {
        var generated = await Cv02Seed.ManualObligationAsync(database, actors, configuration, taskCode,
            origin, database.UuidGenerator.NewUuid(), cancellationToken);
        await using var scope = database.Services.CreateAsyncScope();
        var evaluation = await scope.ServiceProvider.GetRequiredService<IEligibilityEvaluationService>()
            .EvaluateAsync(new EvaluateEligibilityCommand(database.UuidGenerator.NewUuid(),
                database.UuidGenerator.NewUuid(), generated.Obligation.ObligationId,
                Cv02Timeline.WorkingDate, EligibilityDateSources.ManualRequest), cancellationToken);
        var assignment = await scope.ServiceProvider.GetRequiredService<IAutomaticAssignmentService>()
            .AssignAsync(new AssignObligationCommand(database.UuidGenerator.NewUuid(),
                generated.Obligation.ObligationId, evaluation.EvaluationId,
                database.UuidGenerator.NewUuid()), cancellationToken);
        return new AssignmentChain(generated.Obligation, evaluation, assignment);
    }

    private async Task<AutomaticAssignmentResult> AssignAsync(
        Guid obligationId, Guid evaluationId, Guid assignmentKey, CancellationToken cancellationToken)
    {
        await using var scope = database.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAutomaticAssignmentService>()
            .AssignAsync(new AssignObligationCommand(assignmentKey, obligationId, evaluationId,
                database.UuidGenerator.NewUuid()), cancellationToken);
    }

    private async Task<WorkPlanEnsureResult> EnsurePlanAsync(
        Guid actorUserId, Guid planKey, CancellationToken cancellationToken)
    {
        await using var scope = database.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IWorkPlanService>()
            .EnsureAsync(new EnsureWorkPlanCommand(actorUserId, planKey, database.UuidGenerator.NewUuid(),
                BranchScope.LorettaId, 2026, 53), cancellationToken);
    }

    private Task<int> RunWorkerAsync(DateTimeOffset scheduledFor, DemoFaultGate? faultGate,
        CancellationToken cancellationToken) => WorkerApplication.RunAsync(
        ["run-job", "--job", RecurringGenerationContract.JobName, "--scheduled-for", scheduledFor.UtcDateTime.ToString("O")],
        services =>
        {
            services.AddLogging(logging => logging.ClearProviders());
            services.AddSgolRecurringGeneration();
            services.RemoveAll<IClock>();
            services.RemoveAll<IUuidGenerator>();
            services.AddSingleton<IClock>(database.Clock);
            services.AddSingleton<IUuidGenerator>(database.UuidGenerator);
            if (faultGate is not null)
            {
                services.RemoveAll<IRecurringOccurrenceProcessor>();
                services.AddScoped<EfRecurringOccurrenceProcessor>();
                services.AddSingleton(faultGate);
                services.AddScoped<IRecurringOccurrenceProcessor>(provider =>
                    new FaultingOccurrenceProcessor(provider.GetRequiredService<EfRecurringOccurrenceProcessor>(),
                        provider.GetRequiredService<DemoFaultGate>()));
            }
        },
        writeOutput: _ => { },
        configuration: database.Configuration,
        cancellationToken: cancellationToken);

    private static Dictionary<string, string> Facts(params (string Key, string Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

    private static void Require(bool condition)
    {
        if (!condition) throw new DemoScenarioAssertionException();
    }

    private static async Task RequireThrowsAsync<TException>(Func<Task> action) where TException : Exception
    {
        try { await action(); }
        catch (TException) { return; }
        throw new DemoScenarioAssertionException();
    }

    private sealed record AssignmentChain(
        WorkObligationDetails Obligation,
        EligibilityEvaluationDetails Evaluation,
        AutomaticAssignmentResult Assignment);

    private sealed class DemoFaultGate
    {
        public bool FailSecondOccurrenceOnce { get; set; }
        public int Calls;
    }

    private sealed class FaultingOccurrenceProcessor(EfRecurringOccurrenceProcessor inner, DemoFaultGate gate)
        : IRecurringOccurrenceProcessor
    {
        public Task<RecurringOccurrenceResult?> ProcessAsync(RecurringWindow window, Guid correlationId,
            CancellationToken cancellationToken = default)
        {
            if (gate.FailSecondOccurrenceOnce && Interlocked.Increment(ref gate.Calls) == 2)
            {
                gate.FailSecondOccurrenceOnce = false;
                throw new JobExecutionException("SYNTHETIC_PARTIAL_BATCH_FAILURE");
            }
            return inner.ProcessAsync(window, correlationId, cancellationToken);
        }
    }
}
