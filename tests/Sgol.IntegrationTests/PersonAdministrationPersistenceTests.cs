using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Organization;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class PersonAdministrationPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 18, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task CreateThenDeactivate_PreservesHistoryAndTwoAuditEvents()
    {
        var actorUserId = await ResetAndSeedActorAsync(BootstrapContract.DirectionRoleCode);
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator(Now), Now);

        var created = await service.CreateAsync(new CreatePersonCommand(
            actorUserId,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "PER-001",
            "Persona sintética"));
        var current = Assert.Single(created.Person.EmploymentHistory);
        var deactivated = await service.ChangeEmploymentAsync(new ChangeEmploymentCommand(
            actorUserId,
            Guid.CreateVersion7(),
            created.Person.Id,
            EmploymentStatus.Inactive,
            current.RowVersion,
            "Baja sintética",
            Guid.CreateVersion7()));

        Assert.Equal([EmploymentStatus.Active, EmploymentStatus.Inactive],
            deactivated.Person.EmploymentHistory.Select(version => version.Status));
        Assert.NotNull(deactivated.Person.EmploymentHistory[0].ValidTo);
        Assert.Equal(deactivated.Person.EmploymentHistory[0].Id, deactivated.Person.EmploymentHistory[1].SupersedesId);
        Assert.Single(deactivated.Person.EmploymentHistory, version => version.ValidTo is null);
        Assert.Equal(2, await context.AuditEvents.CountAsync(item => item.ResourceId == created.Person.Id));
        Assert.Equal(2, await context.IdempotencyRecords.CountAsync(item => item.ResourceId == created.Person.Id));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task RepeatedCreate_IsIdempotentAndDuplicateCodeHasNoEffect()
    {
        var actorUserId = await ResetAndSeedActorAsync(BootstrapContract.DirectionRoleCode);
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator(Now), Now);
        var idempotencyKey = Guid.CreateVersion7();
        var command = new CreatePersonCommand(
            actorUserId,
            idempotencyKey,
            Guid.CreateVersion7(),
            "PER-002",
            "Persona sintética dos");

        var first = await service.CreateAsync(command);
        var replay = await service.CreateAsync(command);

        Assert.True(replay.Replayed);
        Assert.Equal(first.Person.Id, replay.Person.Id);
        await Assert.ThrowsAsync<PersonIdempotencyConflictException>(() =>
            service.CreateAsync(command with { DisplayName = "Contenido distinto" }));
        await Assert.ThrowsAsync<PersonCodeConflictException>(() =>
            service.CreateAsync(command with
            {
                IdempotencyKey = Guid.CreateVersion7(),
                DisplayName = "Otra persona sintética",
            }));
        Assert.Equal(2, await context.People.CountAsync());
        Assert.Equal(1, await context.AuditEvents.CountAsync(item => item.ResourceId == first.Person.Id));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task DatabaseConstraint_RejectsBlankStableCodeWithoutEffect()
    {
        await ResetAndSeedActorAsync(BootstrapContract.DirectionRoleCode);
        await using var context = CreateContext();
        var personId = Guid.CreateVersion7();
        context.People.Add(new Person
        {
            Id = personId,
            StableCode = "   ",
            DisplayName = "Persona inválida sintética",
            CreatedAt = Now,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        context.ChangeTracker.Clear();
        Assert.False(await context.People.AsNoTracking().AnyAsync(person => person.Id == personId));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task NonDirectionAndUnknownScope_AreDeniedAndAuditedWithoutBusinessEffect()
    {
        var actorUserId = await ResetAndSeedActorAsync("ADMINISTRACION");
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator(Now), Now);

        await Assert.ThrowsAsync<PersonAccessDeniedException>(() =>
            service.ListAsync(actorUserId, Guid.CreateVersion7()));

        context.RoleAssignmentVersions.Add(new RoleAssignmentVersion
        {
            Id = Guid.CreateVersion7(),
            UserId = actorUserId,
            BranchId = Guid.CreateVersion7(),
            RoleCode = BootstrapContract.DirectionRoleCode,
            Status = BootstrapContract.ActiveRoleStatus,
            ValidFrom = Now,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<PersonAccessDeniedException>(() =>
            service.ListAsync(actorUserId, Guid.CreateVersion7()));
        Assert.Equal(2, await context.AuditEvents.CountAsync(item => item.Action == "PERSON_ACCESS_DENIED"));
        Assert.Equal(1, await context.People.CountAsync());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task InactiveDirectionPerson_IsDeniedAndAuditedWithoutBusinessEffect()
    {
        var actorUserId = await ResetAndSeedActorAsync(
            BootstrapContract.DirectionRoleCode,
            EmploymentStatus.Inactive);
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator(Now), Now);

        await Assert.ThrowsAsync<PersonAccessDeniedException>(() =>
            service.ListAsync(actorUserId, Guid.CreateVersion7()));

        Assert.Single(await context.AuditEvents
            .AsNoTracking()
            .Where(item => item.Action == "PERSON_ACCESS_DENIED" && item.ActorUserId == actorUserId)
            .ToListAsync());
        Assert.Equal(1, await context.People.CountAsync());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task AuditInsertFailure_RollsBackPersonVersionAndIdempotencyRecord()
    {
        var actorUserId = await ResetAndSeedActorAsync(BootstrapContract.DirectionRoleCode);
        var duplicateAuditId = Guid.CreateVersion7();
        await using (var seedContext = CreateContext())
        {
            seedContext.AuditEvents.Add(new AuditEvent
            {
                Id = duplicateAuditId,
                OccurredAt = Now,
                ActorUserId = actorUserId,
                ActorType = "APP_USER",
                Action = "SYNTHETIC_EXISTING_EVENT",
                ResourceType = "PERSON",
                BranchId = BranchScope.LorettaId,
                CorrelationId = Guid.CreateVersion7(),
                Outcome = "SUCCESS",
            });
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext();
        var ids = new SequenceUuidGenerator(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            duplicateAuditId);
        var service = CreateService(context, ids, Now);
        var idempotencyKey = Guid.CreateVersion7();

        await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateAsync(new CreatePersonCommand(
            actorUserId,
            idempotencyKey,
            Guid.CreateVersion7(),
            "PER-ROLLBACK",
            "Persona rollback sintética")));

        Assert.False(await context.People.AsNoTracking().AnyAsync(person => person.StableCode == "PER-ROLLBACK"));
        Assert.False(await context.IdempotencyRecords.AsNoTracking().AnyAsync(record => record.Key == idempotencyKey));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ConcurrentEmploymentChanges_PersistExactlyOneSuccessor()
    {
        var actorUserId = await ResetAndSeedActorAsync(BootstrapContract.DirectionRoleCode);
        Guid personId;
        await using (var createContext = CreateContext())
        {
            var createService = CreateService(createContext, NewUuidGenerator(Now), Now);
            var created = await createService.CreateAsync(new CreatePersonCommand(
                actorUserId,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                "PER-CONCURRENT",
                "Persona concurrencia sintética"));
            personId = created.Person.Id;
        }

        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstService = CreateService(firstContext, NewUuidGenerator(Now.AddMinutes(1)), Now.AddMinutes(1));
        var secondService = CreateService(secondContext, NewUuidGenerator(Now.AddMinutes(1)), Now.AddMinutes(1));
        var firstTask = CaptureAsync(() => firstService.ChangeEmploymentAsync(new ChangeEmploymentCommand(
            actorUserId,
            Guid.CreateVersion7(),
            personId,
            EmploymentStatus.Inactive,
            1,
            "Primera carrera sintética",
            Guid.CreateVersion7())));
        var secondTask = CaptureAsync(() => secondService.ChangeEmploymentAsync(new ChangeEmploymentCommand(
            actorUserId,
            Guid.CreateVersion7(),
            personId,
            EmploymentStatus.Inactive,
            1,
            "Segunda carrera sintética",
            Guid.CreateVersion7())));

        var outcomes = await Task.WhenAll(firstTask, secondTask);

        Assert.Single(outcomes, exception => exception is null);
        Assert.Single(outcomes, exception => exception is PersonVersionConflictException);
        await using var assertionContext = CreateContext();
        Assert.Equal(2, await assertionContext.EmploymentVersions.CountAsync(item => item.PersonId == personId));
        Assert.Equal(1, await assertionContext.EmploymentVersions.CountAsync(
            item => item.PersonId == personId && item.ValidTo == null));
        Assert.Equal(2, await assertionContext.AuditEvents.CountAsync(item => item.ResourceId == personId));
        Assert.Empty(firstContext.ChangeTracker.Entries());
        Assert.Empty(secondContext.ChangeTracker.Entries());
    }

    private async Task<Guid> ResetAndSeedActorAsync(
        string roleCode,
        string employmentStatus = EmploymentStatus.Active)
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        var personId = Guid.CreateVersion7();
        var actorUserId = Guid.CreateVersion7();
        context.People.Add(new Person
        {
            Id = personId,
            StableCode = $"ACTOR-{actorUserId:N}",
            DisplayName = "Actor sintético",
            CreatedAt = Now,
        });
        context.EmploymentVersions.Add(new EmploymentVersion(
            Guid.CreateVersion7(),
            personId,
            BranchScope.LorettaId,
            employmentStatus,
            Now));
        context.AppUsers.Add(new AppUser
        {
            Id = actorUserId,
            PersonId = personId,
            Status = BootstrapContract.ActiveAccountStatus,
            MustChangePassword = false,
            MfaEnrolledAt = Now,
            SecurityStamp = "synthetic-security-stamp",
        });
        context.RoleAssignmentVersions.Add(new RoleAssignmentVersion
        {
            Id = Guid.CreateVersion7(),
            UserId = actorUserId,
            BranchId = BranchScope.LorettaId,
            RoleCode = roleCode,
            Status = BootstrapContract.ActiveRoleStatus,
            ValidFrom = Now,
        });
        await context.SaveChangesAsync();
        return actorUserId;
    }

    private SgolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new SgolDbContext(options);
    }

    private static EfPersonAdministrationService CreateService(
        SgolDbContext context,
        IUuidGenerator uuidGenerator,
        DateTimeOffset now) => new(
            context,
            new AuditTransaction(context),
            new FixedClock(now),
            uuidGenerator);

    private static Uuid7Generator NewUuidGenerator(DateTimeOffset now) => new(new FixedClock(now));

    private static async Task<Exception?> CaptureAsync(Func<Task<PersonMutationResult>> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

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
