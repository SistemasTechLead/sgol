using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Identity;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class RoleAdministrationPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 18, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task DirectionAssignsCanonicalRoleWithoutChangingAccountCredentialPersonOrEmployment()
    {
        var actorUserId = await ResetAndSeedUserAsync("ACTOR-DIRECTION", CanonicalRole.Direction);
        var targetUserId = await SeedUserAsync("TARGET-INITIAL", roleCode: null);
        await using var context = CreateContext();
        var credentialBefore = await context.IdentityCredentials.AsNoTracking()
            .SingleAsync(item => item.UserId == targetUserId);
        var employmentBefore = await CurrentEmploymentAsync(context, targetUserId);
        var service = CreateService(context, NewUuidGenerator(Now), Now);

        var result = await service.ChangeAsync(Command(
            actorUserId,
            targetUserId,
            CanonicalRole.Administration,
            "Asignación inicial sintética"));

        var assignment = await context.RoleAssignmentVersions.AsNoTracking()
            .SingleAsync(item => item.UserId == targetUserId);
        var target = await context.AppUsers.AsNoTracking().SingleAsync(item => item.Id == targetUserId);
        var credentialAfter = await context.IdentityCredentials.AsNoTracking()
            .SingleAsync(item => item.UserId == targetUserId);
        var employmentAfter = await CurrentEmploymentAsync(context, targetUserId);
        var audit = await context.AuditEvents.AsNoTracking()
            .SingleAsync(item => item.Action == "ROLE_ASSIGNED");

        Assert.Equal(CanonicalRole.Administration, assignment.RoleCode);
        Assert.Equal(RoleAssignmentStatus.Active, assignment.Status);
        Assert.Null(assignment.ValidTo);
        Assert.Equal(1, assignment.RowVersion);
        Assert.Single(result.Assignment.History);
        Assert.Equal(AccountStatus.Active, target.Status);
        Assert.Equal(credentialBefore.PasswordHash, credentialAfter.PasswordHash);
        Assert.Equal(employmentBefore.Id, employmentAfter.Id);
        Assert.Equal("Puesto sintético", employmentAfter.PositionText);
        Assert.Equal("Turno sintético", employmentAfter.ShiftText);
        Assert.Null(audit.BeforeData);
        Assert.Equal(CanonicalRole.Administration, audit.AfterData!.RootElement.GetProperty("roleCode").GetString());
        AssertSafeAudit(audit);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ChangeAndRevokePreserveHistoryRotateSecurityStampAndLeaveNoActiveRole()
    {
        var actorUserId = await ResetAndSeedUserAsync("ACTOR-CYCLE", CanonicalRole.Direction);
        var targetUserId = await SeedUserAsync("TARGET-CYCLE", CanonicalRole.SalesFloor);
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator(Now), Now);
        var stampBefore = await SecurityStampAsync(context, targetUserId);

        var changed = await service.ChangeAsync(Command(
            actorUserId,
            targetUserId,
            CanonicalRole.Subcoordination,
            "Cambio sintético",
            expectedRowVersion: 1));
        var stampAfterChange = await SecurityStampAsync(context, targetUserId);
        var revoked = await service.ChangeAsync(Command(
            actorUserId,
            targetUserId,
            roleCode: null,
            "Revocación sintética",
            expectedRowVersion: 2));
        var stampAfterRevoke = await SecurityStampAsync(context, targetUserId);

        var history = await context.RoleAssignmentVersions.AsNoTracking()
            .Where(item => item.UserId == targetUserId)
            .OrderBy(item => item.RowVersion)
            .ThenBy(item => item.ValidTo == null)
            .ThenBy(item => item.ValidFrom)
            .ThenBy(item => item.Id)
            .ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.All(history, item => Assert.Equal(RoleAssignmentStatus.Superseded, item.Status));
        Assert.All(history, item => Assert.NotNull(item.ValidTo));
        Assert.Equal(history[0].Id, history[1].SupersedesId);
        Assert.NotEqual(stampBefore, stampAfterChange);
        Assert.NotEqual(stampAfterChange, stampAfterRevoke);
        Assert.Equal(2, changed.Assignment.History.Count);
        Assert.Equal(2, revoked.Assignment.History.Count);
        Assert.False(await context.RoleAssignmentVersions.AnyAsync(item =>
            item.UserId == targetUserId && item.Status == RoleAssignmentStatus.Active));
        var audits = await context.AuditEvents.AsNoTracking()
            .Where(item => item.ResourceId == targetUserId && item.ResourceType == "ROLE_ASSIGNMENT")
            .ToListAsync();
        Assert.Contains(audits, item =>
            item.Action == "ROLE_CHANGED" &&
            item.BeforeData!.RootElement.GetProperty("roleCode").GetString() == CanonicalRole.SalesFloor &&
            item.AfterData!.RootElement.GetProperty("roleCode").GetString() == CanonicalRole.Subcoordination);
        Assert.Contains(audits, item =>
            item.Action == "ROLE_REVOKED" &&
            item.BeforeData!.RootElement.GetProperty("roleCode").GetString() == CanonicalRole.Subcoordination &&
            item.AfterData!.RootElement.GetProperty("roleCode").ValueKind == System.Text.Json.JsonValueKind.Null);
        Assert.All(audits, AssertSafeAudit);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task InvalidTargetsRoleAndSecondSimultaneousRoleAreRejectedWithoutPartialEffect()
    {
        var actorUserId = await ResetAndSeedUserAsync("ACTOR-REJECT", CanonicalRole.Direction);
        var activeUserId = await SeedUserAsync("TARGET-ACTIVE", CanonicalRole.Administration);
        var inactiveUserId = await SeedUserAsync("TARGET-INACTIVE", roleCode: null, accountStatus: AccountStatus.Inactive);
        var outOfScopeUserId = await SeedUserAsync("TARGET-SCOPE", roleCode: null, activeEmployment: false);
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator(Now), Now);

        await Assert.ThrowsAsync<RoleTargetNotFoundException>(() => service.ChangeAsync(Command(
            actorUserId,
            Guid.CreateVersion7(),
            CanonicalRole.SalesFloor,
            "Cuenta inexistente")));
        await Assert.ThrowsAsync<RoleTargetInactiveException>(() => service.ChangeAsync(Command(
            actorUserId,
            inactiveUserId,
            CanonicalRole.SalesFloor,
            "Cuenta inactiva")));
        await Assert.ThrowsAsync<RoleTargetOutOfScopeException>(() => service.ChangeAsync(Command(
            actorUserId,
            outOfScopeUserId,
            CanonicalRole.SalesFloor,
            "Cuenta fuera de alcance")));
        await Assert.ThrowsAsync<RoleValidationException>(() => service.ChangeAsync(Command(
            actorUserId,
            activeUserId,
            "DIRECTOR",
            "Rol no canónico")));
        await Assert.ThrowsAsync<RoleIfMatchRequiredException>(() => service.ChangeAsync(Command(
            actorUserId,
            activeUserId,
            CanonicalRole.SalesFloor,
            "Segundo rol sin versión")));

        var activeRoles = await context.RoleAssignmentVersions.AsNoTracking()
            .Where(item => item.UserId == activeUserId && item.Status == RoleAssignmentStatus.Active)
            .ToListAsync();
        Assert.Single(activeRoles);
        Assert.Equal(CanonicalRole.Administration, activeRoles[0].RoleCode);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ActorWithoutRoleAdminPermissionIsDeniedAuditedAndHasNoEffect()
    {
        var actorUserId = await ResetAndSeedUserAsync("ACTOR-DENIED", CanonicalRole.Administration);
        var targetUserId = await SeedUserAsync("TARGET-DENIED", roleCode: null);
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator(Now), Now);

        await Assert.ThrowsAsync<RoleAccessDeniedException>(() => service.ChangeAsync(Command(
            actorUserId,
            targetUserId,
            CanonicalRole.SalesFloor,
            "Intento no autorizado")));

        Assert.False(await context.RoleAssignmentVersions.AnyAsync(item => item.UserId == targetUserId));
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(), item =>
            item.Action == "ROLE_ADMIN_ACCESS_DENIED" && item.Outcome == "DENIED");
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task HierarchyUsesOnlyCanonicalRoleAndIgnoresPositionAndShift()
    {
        await ResetAndSeedUserAsync("ACTOR-SEED", CanonicalRole.Direction);
        var administration = await SeedUserAsync("ROLE-ADMIN", CanonicalRole.Administration, "Director", "Matutino");
        var administrationPeer = await SeedUserAsync("ROLE-ADMIN-PEER", CanonicalRole.Administration, "Piso", "Nocturno");
        var subcoordination = await SeedUserAsync("ROLE-SUB", CanonicalRole.Subcoordination, "Director", "Nocturno");
        var salesFloor = await SeedUserAsync("ROLE-FLOOR", CanonicalRole.SalesFloor, "Administración", "Matutino");
        var direction = await SeedUserAsync("ROLE-DIRECTION", CanonicalRole.Direction, "Piso", "Nocturno");
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator(Now), Now);

        Assert.True(await service.CanAccessUserAsync(administration, administration));
        Assert.True(await service.CanAccessUserAsync(administration, subcoordination));
        Assert.True(await service.CanAccessUserAsync(administration, salesFloor));
        Assert.False(await service.CanAccessUserAsync(administration, administrationPeer));
        Assert.False(await service.CanAccessUserAsync(administration, direction));
        Assert.False(await service.CanAccessUserAsync(salesFloor, administration));
        Assert.True(await service.CanAccessUserAsync(direction, administrationPeer));

        var roleBefore = await CurrentRoleAsync(context, subcoordination);
        var personId = await context.AppUsers.AsNoTracking()
            .Where(item => item.Id == subcoordination)
            .Select(item => item.PersonId)
            .SingleAsync();
        var employment = await context.EmploymentVersions
            .SingleAsync(item => item.PersonId == personId && item.ValidTo == null);
        var successor = employment.CreateSuccessor(
            Guid.CreateVersion7(),
            EmploymentStatus.Active,
            "Piso",
            "Matutino",
            Now.AddMinutes(1));
        context.EmploymentVersions.Add(successor);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var roleAfter = await CurrentRoleAsync(context, subcoordination);
        Assert.Equal(roleBefore.Id, roleAfter.Id);
        Assert.Equal(roleBefore.RoleCode, roleAfter.RoleCode);
        Assert.True(await service.CanAccessUserAsync(administration, subcoordination));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task IdempotencyReplaysSameBodyConflictsOnDifferentBodyAndSeparatesTargets()
    {
        var actorUserId = await ResetAndSeedUserAsync("ACTOR-IDEMPOTENT", CanonicalRole.Direction);
        var firstTarget = await SeedUserAsync("TARGET-IDEM-1", roleCode: null);
        var secondTarget = await SeedUserAsync("TARGET-IDEM-2", roleCode: null);
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator(Now), Now);
        var sharedKey = Guid.CreateVersion7();
        var firstCommand = Command(
            actorUserId,
            firstTarget,
            CanonicalRole.Administration,
            "Asignación idempotente",
            key: sharedKey);

        var first = await service.ChangeAsync(firstCommand);
        var replay = await service.ChangeAsync(firstCommand);
        await Assert.ThrowsAsync<RoleIdempotencyConflictException>(() => service.ChangeAsync(Command(
            actorUserId,
            firstTarget,
            CanonicalRole.Subcoordination,
            "Contenido distinto",
            key: sharedKey)));
        var otherScope = await service.ChangeAsync(Command(
            actorUserId,
            secondTarget,
            CanonicalRole.SalesFloor,
            "Otro alcance",
            key: sharedKey));

        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.False(otherScope.Replayed);
        Assert.Equal(2, await context.IdempotencyRecords.CountAsync(item => item.Key == sharedKey));
        Assert.Equal(2, await context.RoleAssignmentVersions.CountAsync(item =>
            item.UserId == firstTarget || item.UserId == secondTarget));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task PostgreSqlConstraintRejectsSecondActiveRole()
    {
        await ResetAndSeedUserAsync("ACTOR-CONSTRAINT", CanonicalRole.Direction);
        var targetUserId = await SeedUserAsync("TARGET-CONSTRAINT", CanonicalRole.Administration);
        await using var context = CreateContext();
        context.RoleAssignmentVersions.Add(NewRole(targetUserId, CanonicalRole.SalesFloor));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        context.ChangeTracker.Clear();
        Assert.Single(await context.RoleAssignmentVersions.AsNoTracking()
            .Where(item => item.UserId == targetUserId && item.Status == RoleAssignmentStatus.Active)
            .ToListAsync());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ConcurrentInitialAssignmentsProduceOneActiveRoleAndOneRejectedWriter()
    {
        var actorUserId = await ResetAndSeedUserAsync("ACTOR-RACE", CanonicalRole.Direction);
        var targetUserId = await SeedUserAsync("TARGET-RACE", roleCode: null);
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstService = CreateService(firstContext, NewUuidGenerator(Now), Now);
        var secondService = CreateService(secondContext, NewUuidGenerator(Now.AddMilliseconds(1)), Now.AddMilliseconds(1));

        var outcomes = await Task.WhenAll(
            CaptureAsync(() => firstService.ChangeAsync(Command(
                actorUserId,
                targetUserId,
                CanonicalRole.Administration,
                "Carrera uno"))),
            CaptureAsync(() => secondService.ChangeAsync(Command(
                actorUserId,
                targetUserId,
                CanonicalRole.SalesFloor,
                "Carrera dos"))));

        Assert.Single(outcomes, item => item is RoleAssignmentMutationResult);
        Assert.Single(outcomes, item => item is RoleIfMatchRequiredException);
        await using var verification = CreateContext();
        Assert.Single(await verification.RoleAssignmentVersions.AsNoTracking()
            .Where(item => item.UserId == targetUserId && item.Status == RoleAssignmentStatus.Active)
            .ToListAsync());
        Assert.Equal(1, await verification.AuditEvents.CountAsync(item =>
            item.ResourceId == targetUserId && item.Action == "ROLE_ASSIGNED"));
        Assert.Empty(firstContext.ChangeTracker.Entries());
        Assert.Empty(secondContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task AuditAndFunctionalFailuresRollbackRoleStampIdempotencyAndLeaveTrackerClean()
    {
        var actorUserId = await ResetAndSeedUserAsync("ACTOR-ROLLBACK", CanonicalRole.Direction);
        var auditTarget = await SeedUserAsync("TARGET-AUDIT-ROLLBACK", CanonicalRole.SalesFloor);
        var functionalTarget = await SeedUserAsync("TARGET-FUNCTIONAL-ROLLBACK", roleCode: null);
        var auditId = Guid.CreateVersion7();
        await SeedAuditAsync(actorUserId, auditId);

        await using (var auditContext = CreateContext())
        {
            var assignmentId = Guid.CreateVersion7();
            var service = CreateService(
                auditContext,
                new SequenceUuidGenerator(assignmentId, auditId),
                Now.AddMinutes(1));
            var stampBefore = await SecurityStampAsync(auditContext, auditTarget);
            await Assert.ThrowsAsync<DbUpdateException>(() => service.ChangeAsync(Command(
                actorUserId,
                auditTarget,
                CanonicalRole.Subcoordination,
                "Falla de auditoría",
                expectedRowVersion: 1)));
            Assert.False(await auditContext.RoleAssignmentVersions.AsNoTracking()
                .AnyAsync(item => item.Id == assignmentId));
            Assert.Equal(stampBefore, await SecurityStampAsync(auditContext, auditTarget));
            Assert.False(await auditContext.IdempotencyRecords.AsNoTracking()
                .AnyAsync(item => item.ResourceId == assignmentId));
            Assert.Empty(auditContext.ChangeTracker.Entries());
        }

        await using (var functionalContext = CreateContext())
        {
            await functionalContext.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION sgol_reject_synthetic_role() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'synthetic role rejection';
                END;
                $$ LANGUAGE plpgsql;
                CREATE TRIGGER synthetic_reject_role
                BEFORE INSERT ON role_assignment_version
                FOR EACH ROW EXECUTE FUNCTION sgol_reject_synthetic_role();
                """);
            var service = CreateService(functionalContext, NewUuidGenerator(Now.AddMinutes(2)), Now.AddMinutes(2));
            await Assert.ThrowsAsync<DbUpdateException>(() => service.ChangeAsync(Command(
                actorUserId,
                functionalTarget,
                CanonicalRole.SalesFloor,
                "Falla funcional")));
            Assert.False(await functionalContext.RoleAssignmentVersions.AsNoTracking()
                .AnyAsync(item => item.UserId == functionalTarget));
            Assert.False(await functionalContext.AuditEvents.AsNoTracking()
                .AnyAsync(item => item.ResourceId == functionalTarget));
            Assert.Empty(functionalContext.ChangeTracker.Entries());
        }
    }

    private async Task<Guid> ResetAndSeedUserAsync(string stableCode, string roleCode)
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        return await SeedUserAsync(stableCode, roleCode);
    }

    private async Task<Guid> SeedUserAsync(
        string stableCode,
        string? roleCode,
        string positionText = "Puesto sintético",
        string shiftText = "Turno sintético",
        string accountStatus = AccountStatus.Active,
        bool activeEmployment = true)
    {
        await using var context = CreateContext();
        var personId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        context.People.Add(new Person
        {
            Id = personId,
            StableCode = stableCode,
            DisplayName = "Persona sintética",
            CreatedAt = Now,
        });
        if (activeEmployment)
        {
            context.EmploymentVersions.Add(new EmploymentVersion(
                Guid.CreateVersion7(),
                personId,
                BranchScope.LorettaId,
                EmploymentStatus.Active,
                Now,
                positionText: positionText,
                shiftText: shiftText));
        }

        context.AppUsers.Add(new AppUser
        {
            Id = userId,
            PersonId = personId,
            Status = accountStatus,
            MustChangePassword = false,
            MfaEnrolledAt = Now,
            SecurityStamp = $"synthetic-stamp-{userId:N}",
        });
        context.IdentityCredentials.Add(new IdentityCredential
        {
            UserId = userId,
            UserName = $"user.{userId:N}",
            NormalizedUserName = $"USER.{userId:N}".ToUpperInvariant(),
            PasswordHash = "synthetic-hash-not-a-secret",
        });
        if (roleCode is not null)
        {
            context.RoleAssignmentVersions.Add(NewRole(userId, roleCode));
        }

        await context.SaveChangesAsync();
        return userId;
    }

    private async Task SeedAuditAsync(Guid actorUserId, Guid auditId)
    {
        await using var context = CreateContext();
        context.AuditEvents.Add(new AuditEvent
        {
            Id = auditId,
            OccurredAt = Now,
            ActorUserId = actorUserId,
            ActorType = "APP_USER",
            Action = "SYNTHETIC_EXISTING_EVENT",
            ResourceType = "ROLE_ASSIGNMENT",
            BranchId = BranchScope.LorettaId,
            CorrelationId = Guid.CreateVersion7(),
            Outcome = "SUCCESS",
        });
        await context.SaveChangesAsync();
    }

    private SgolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new SgolDbContext(options);
    }

    private static EfRoleAssignmentService CreateService(
        SgolDbContext context,
        IUuidGenerator uuidGenerator,
        DateTimeOffset now) => new(
            context,
            new AuditTransaction(context),
            new FixedClock(now),
            uuidGenerator);

    private static ChangeRoleAssignmentCommand Command(
        Guid actorUserId,
        Guid userId,
        string? roleCode,
        string reason,
        long? expectedRowVersion = null,
        Guid? key = null) => new(
            actorUserId,
            key ?? Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            userId,
            roleCode,
            reason,
            expectedRowVersion);

    private static RoleAssignmentVersion NewRole(Guid userId, string roleCode) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        BranchId = BranchScope.LorettaId,
        RoleCode = roleCode,
        Status = RoleAssignmentStatus.Active,
        ValidFrom = Now,
        RowVersion = 1,
    };

    private static async Task<EmploymentVersion> CurrentEmploymentAsync(SgolDbContext context, Guid userId)
    {
        var personId = await context.AppUsers.AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => item.PersonId)
            .SingleAsync();
        return await context.EmploymentVersions.AsNoTracking()
            .SingleAsync(item => item.PersonId == personId && item.ValidTo == null);
    }

    private static Task<RoleAssignmentVersion> CurrentRoleAsync(SgolDbContext context, Guid userId) =>
        context.RoleAssignmentVersions.AsNoTracking()
            .SingleAsync(item => item.UserId == userId && item.Status == RoleAssignmentStatus.Active);

    private static Task<string> SecurityStampAsync(SgolDbContext context, Guid userId) =>
        context.AppUsers.AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => item.SecurityStamp)
            .SingleAsync();

    private static void AssertSafeAudit(AuditEvent audit)
    {
        var text = (audit.BeforeData?.RootElement.GetRawText() ?? string.Empty) +
            (audit.AfterData?.RootElement.GetRawText() ?? string.Empty);
        Assert.DoesNotContain("securityStamp", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("totp", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recovery", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("position", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("shift", text, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<object> CaptureAsync(Func<Task<RoleAssignmentMutationResult>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static Uuid7Generator NewUuidGenerator(DateTimeOffset now) => new(new FixedClock(now));

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class SequenceUuidGenerator(params Guid[] values) : IUuidGenerator
    {
        private readonly Queue<Guid> _values = new(values);

        public Guid NewUuid() => _values.Dequeue();
    }
}
