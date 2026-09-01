using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Bootstrap;

public sealed class DirectionBootstrapInput
{
    public required string PersonStableCode { get; init; }

    public required string PersonDisplayName { get; init; }

    public required string UserName { get; init; }

    public required string InitialPassword { get; init; }

    public override string ToString() => $"{nameof(DirectionBootstrapInput)} {{ InitialPassword = [REDACTED] }}";
}

public sealed record DirectionBootstrapResult(Guid PersonId, Guid UserId, Guid RoleAssignmentId);

public sealed class DirectionBootstrapAlreadyCompletedException()
    : InvalidOperationException("The DIRECCION bootstrap path is permanently disabled.");

public sealed class DirectionBootstrapService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IClock clock,
    IUuidGenerator uuidGenerator,
    IPasswordHasher<AppUser> passwordHasher)
{
    private const string BootstrapConstraintName = "PK_direction_bootstrap";

    public async Task<DirectionBootstrapResult> ExecuteAsync(
        DirectionBootstrapInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        Validate(input);

        var now = clock.UtcNow;
        var personId = uuidGenerator.NewUuid();
        var userId = uuidGenerator.NewUuid();
        var roleAssignmentId = uuidGenerator.NewUuid();
        var user = new AppUser
        {
            Id = userId,
            PersonId = personId,
            Status = BootstrapContract.ActiveAccountStatus,
            MustChangePassword = true,
            MfaEnrolledAt = null,
            SecurityStamp = Convert.ToHexString(Guid.NewGuid().ToByteArray()),
        };
        var credential = new IdentityCredential
        {
            UserId = userId,
            UserName = input.UserName.Trim(),
            NormalizedUserName = input.UserName.Trim().ToUpperInvariant(),
            PasswordHash = passwordHasher.HashPassword(user, input.InitialPassword),
        };

        using var afterData = JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 1,
            personId,
            userId,
            roleAssignmentId,
            mustChangePassword = true,
            mfaEnrolled = false,
        });

        var auditEvent = new AuditEvent
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = now,
            ActorUserId = null,
            ActorType = "TECHNICAL_OPERATOR",
            Action = "DIRECTION_BOOTSTRAP_COMPLETED",
            ResourceType = "APP_USER",
            ResourceId = userId,
            BranchId = BootstrapContract.LorettaBranchId,
            CorrelationId = uuidGenerator.NewUuid(),
            AfterData = afterData,
            Outcome = "SUCCESS",
        };

        try
        {
            await auditTransaction.ExecuteAsync(
                auditEvent,
                async criticalWriteCancellationToken =>
                {
                    dbContext.DirectionBootstrapMarkers.Add(new DirectionBootstrapMarker
                    {
                        Singleton = true,
                        CompletedAt = now,
                    });
                    await dbContext.SaveChangesAsync(criticalWriteCancellationToken);

                    dbContext.People.Add(new Person
                    {
                        Id = personId,
                        StableCode = input.PersonStableCode.Trim(),
                        DisplayName = input.PersonDisplayName.Trim(),
                        CreatedAt = now,
                    });
                    dbContext.EmploymentVersions.Add(new EmploymentVersion
                    {
                        Id = uuidGenerator.NewUuid(),
                        PersonId = personId,
                        BranchId = BootstrapContract.LorettaBranchId,
                        Status = BootstrapContract.ActivePersonStatus,
                        ValidFrom = now,
                        RowVersion = 1,
                    });
                    dbContext.AppUsers.Add(user);
                    dbContext.IdentityCredentials.Add(credential);
                    dbContext.RoleAssignmentVersions.Add(new RoleAssignmentVersion
                    {
                        Id = roleAssignmentId,
                        UserId = userId,
                        BranchId = BootstrapContract.LorettaBranchId,
                        RoleCode = BootstrapContract.DirectionRoleCode,
                        Status = BootstrapContract.ActiveRoleStatus,
                        ValidFrom = now,
                    });

                },
                cancellationToken);

            dbContext.ChangeTracker.Clear();
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: BootstrapConstraintName,
            })
        {
            dbContext.ChangeTracker.Clear();
            throw new DirectionBootstrapAlreadyCompletedException();
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }

        return new DirectionBootstrapResult(personId, userId, roleAssignmentId);
    }

    private static void Validate(DirectionBootstrapInput input)
    {
        if (string.IsNullOrWhiteSpace(input.PersonStableCode) ||
            string.IsNullOrWhiteSpace(input.PersonDisplayName) ||
            string.IsNullOrWhiteSpace(input.UserName))
        {
            throw new ArgumentException("Person code, display name and user name are required.");
        }

        if (string.IsNullOrEmpty(input.InitialPassword) || input.InitialPassword.Length < 14)
        {
            throw new ArgumentException("The injected initial password does not meet the approved minimum length.");
        }
    }
}

public static class DirectionBootstrapServiceCollectionExtensions
{
    public static IServiceCollection AddDirectionBootstrap(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<IUuidGenerator, Uuid7Generator>();
        services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
        services.AddScoped<DirectionBootstrapService>();
        return services;
    }
}
