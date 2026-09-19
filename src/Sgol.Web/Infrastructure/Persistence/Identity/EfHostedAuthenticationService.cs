using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Authentication;

namespace Sgol.Web.Infrastructure.Persistence.Identity;

internal sealed class EfHostedAuthenticationService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IClock clock,
    IUuidGenerator uuidGenerator,
    IPasswordHasher<AppUser> passwordHasher,
    TotpSecretProtector secretProtector,
    AuthenticationTelemetry telemetry) : IHostedAuthenticationService
{
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
    private readonly AppUser _dummyUser = new()
    {
        Id = Guid.Empty,
        PersonId = Guid.Empty,
        Status = AccountStatus.Inactive,
        MustChangePassword = true,
        SecurityStamp = "DUMMY",
    };
    private string? _dummyPasswordHash;

    public async Task<AuthenticationFlowResult> LoginAsync(
        string userName,
        string password,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserName = RequireUserName(userName).ToUpperInvariant();
        RequirePasswordInput(password);
        var credentialSeed = await dbContext.IdentityCredentials.AsNoTracking()
            .SingleOrDefaultAsync(item => item.NormalizedUserName == normalizedUserName, cancellationToken);
        if (credentialSeed is null)
        {
            _ = passwordHasher.VerifyHashedPassword(
                _dummyUser,
                _dummyPasswordHash ??= passwordHasher.HashPassword(_dummyUser, "Dummy password value never used 2026!"),
                password);
            await WriteAuditOnlyAsync(null, correlationId, "AUTH_LOGIN_FAILED", "INVALID_CREDENTIALS", "DENIED", cancellationToken);
            telemetry.LoginFailed();
            throw new AuthenticationFailedException();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var user = await LockUserAsync(credentialSeed.UserId, cancellationToken);
        var credential = await dbContext.IdentityCredentials.SingleAsync(item => item.UserId == user.Id, cancellationToken);
        var verification = passwordHasher.VerifyHashedPassword(user, credential.PasswordHash, password);
        var now = clock.UtcNow;

        if (verification == PasswordVerificationResult.Failed)
        {
            if (user.LockoutEndUtc is not DateTimeOffset activeLockout || activeLockout <= now)
            {
                ApplyFailedAttempt(user, now);
            }
            dbContext.AuditEvents.Add(NewAudit(null, correlationId, "AUTH_LOGIN_FAILED", null, "INVALID_CREDENTIALS", "DENIED"));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            telemetry.LoginFailed();
            throw new AuthenticationFailedException();
        }

        if (user.LockoutEndUtc is DateTimeOffset lockoutEnd && lockoutEnd > now)
        {
            dbContext.AuditEvents.Add(NewAudit(user.Id, correlationId, "AUTH_LOGIN_FAILED", user.Id, "ACCOUNT_LOCKED", "DENIED"));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            telemetry.LockoutObserved();
            throw new AuthenticationAccountLockedException(lockoutEnd);
        }

        var identity = await LoadEligibleIdentityAsync(user.Id, cancellationToken);
        if (identity is null || user.Status != AccountStatus.Active)
        {
            ApplyFailedAttempt(user, now);
            dbContext.AuditEvents.Add(NewAudit(null, correlationId, "AUTH_LOGIN_FAILED", null, "INVALID_CREDENTIALS", "DENIED"));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            telemetry.LoginFailed();
            throw new AuthenticationFailedException();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            credential.PasswordHash = passwordHasher.HashPassword(user, password);
        }

        await ConsumePendingChallengesAsync(user.Id, now, cancellationToken);
        var hasTotp = await dbContext.MfaTotpCredentials.AsNoTracking()
            .AnyAsync(item => item.UserId == user.Id && item.RevokedAt == null, cancellationToken);
        string purpose;
        string nextStep;
        if (user.MustChangePassword)
        {
            purpose = AuthenticationChallengePurpose.ChangePassword;
            nextStep = AuthenticationNextStep.ChangePassword;
        }
        else if (user.MfaEnrolledAt is null && !hasTotp)
        {
            purpose = AuthenticationChallengePurpose.EnrollMfa;
            nextStep = AuthenticationNextStep.EnrollMfa;
        }
        else if (user.MfaEnrolledAt is not null && hasTotp)
        {
            purpose = AuthenticationChallengePurpose.VerifyMfa;
            nextStep = AuthenticationNextStep.VerifyMfa;
        }
        else
        {
            dbContext.AuditEvents.Add(NewAudit(user.Id, correlationId, "AUTH_LOGIN_FAILED", user.Id, "MFA_STATE_INCONSISTENT", "DENIED"));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            telemetry.LoginFailed();
            throw new AuthenticationMfaStateInconsistentException();
        }

        var challenge = NewChallenge(user, purpose, now);
        dbContext.AuthenticationChallenges.Add(challenge);
        dbContext.AuditEvents.Add(NewAudit(user.Id, correlationId, "AUTH_PASSWORD_ACCEPTED", user.Id, null, "SUCCESS"));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        telemetry.LoginSucceeded();
        return new AuthenticationFlowResult(challenge.Id, nextStep, challenge.ExpiresAt);
    }

    public async Task<AuthenticationFlowResult> ChangePasswordAsync(
        Guid? challengeId,
        Guid? authenticatedUserId,
        string currentPassword,
        string newPassword,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        RequirePasswordInput(currentPassword);
        ValidateNewPassword(newPassword);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        AuthenticationChallenge? challenge = null;
        Guid userId;
        if (challengeId is Guid suppliedChallenge)
        {
            challenge = await LockChallengeAsync(suppliedChallenge, AuthenticationChallengePurpose.ChangePassword, cancellationToken);
            userId = challenge.UserId;
        }
        else if (authenticatedUserId is Guid authenticated && authenticated != Guid.Empty)
        {
            userId = authenticated;
        }
        else
        {
            throw new AuthenticationChallengeInvalidException();
        }

        var user = await LockUserAsync(userId, cancellationToken);
        EnsureChallengeCurrent(challenge, user);
        var credential = await dbContext.IdentityCredentials.SingleAsync(item => item.UserId == userId, cancellationToken);
        if (passwordHasher.VerifyHashedPassword(user, credential.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
        {
            throw new AuthenticationFailedException();
        }

        if (passwordHasher.VerifyHashedPassword(user, credential.PasswordHash, newPassword) != PasswordVerificationResult.Failed ||
            string.Equals(credential.NormalizedUserName, newPassword.ToUpperInvariant(), StringComparison.Ordinal))
        {
            throw new AuthenticationPasswordPolicyException("La nueva contraseña debe ser diferente de la contraseña actual y del usuario.");
        }

        var now = clock.UtcNow;
        credential.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        user.MustChangePassword = false;
        user.SecurityStamp = NewSecurityStamp();
        user.AuthenticationRowVersion++;
        if (challenge is not null)
        {
            ConsumeChallenge(challenge, now);
        }
        await ConsumePendingChallengesAsync(user.Id, now, cancellationToken, challenge?.Id);
        var hasTotp = await dbContext.MfaTotpCredentials.AsNoTracking()
            .AnyAsync(item => item.UserId == user.Id && item.RevokedAt == null, cancellationToken);
        var nextPurpose = hasTotp ? AuthenticationChallengePurpose.VerifyMfa : AuthenticationChallengePurpose.EnrollMfa;
        var nextStep = hasTotp ? AuthenticationNextStep.VerifyMfa : AuthenticationNextStep.EnrollMfa;
        var nextChallenge = NewChallenge(user, nextPurpose, now);
        dbContext.AuthenticationChallenges.Add(nextChallenge);
        dbContext.AuditEvents.Add(NewAudit(user.Id, correlationId, "AUTH_PASSWORD_CHANGED", user.Id, null, "SUCCESS"));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return new AuthenticationFlowResult(nextChallenge.Id, nextStep, nextChallenge.ExpiresAt);
    }

    public async Task<MfaEnrollmentResult> BeginMfaEnrollmentAsync(
        Guid challengeId,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var challenge = await LockChallengeAsync(challengeId, AuthenticationChallengePurpose.EnrollMfa, cancellationToken);
        var user = await LockUserAsync(challenge.UserId, cancellationToken);
        EnsureChallengeCurrent(challenge, user);
        var credential = await dbContext.IdentityCredentials.AsNoTracking()
            .SingleAsync(item => item.UserId == user.Id, cancellationToken);
        var created = challenge.ProtectedTotpSecret is null;
        var secret = created
            ? TotpCodes.GenerateSecret()
            : secretProtector.Unprotect(user.Id, challenge.ProtectedTotpSecret!);
        if (created)
        {
            challenge.ProtectedTotpSecret = secretProtector.Protect(user.Id, secret);
            challenge.RowVersion++;
            dbContext.AuditEvents.Add(NewAudit(user.Id, correlationId, "AUTH_MFA_ENROLLMENT_STARTED", user.Id, null, "SUCCESS"));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return new MfaEnrollmentResult(secret, TotpCodes.CreateOtpAuthUri(credential.UserName, secret), challenge.ExpiresAt);
    }

    public async Task<MfaCompletionResult> ConfirmMfaEnrollmentAsync(
        Guid challengeId,
        string totpCode,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var challenge = await LockChallengeAsync(challengeId, AuthenticationChallengePurpose.EnrollMfa, cancellationToken);
        var user = await LockUserAsync(challenge.UserId, cancellationToken);
        EnsureChallengeCurrent(challenge, user);
        if (challenge.ProtectedTotpSecret is null)
        {
            throw new AuthenticationChallengeInvalidException();
        }

        var now = clock.UtcNow;
        var secret = secretProtector.Unprotect(user.Id, challenge.ProtectedTotpSecret);
        if (!TotpCodes.TryValidate(secret, totpCode, now, null, out var acceptedStep))
        {
            await RecordMfaFailureAsync(user, challenge, correlationId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            telemetry.MfaFailed();
            throw new AuthenticationMfaCodeInvalidException();
        }

        await RevokeMfaAsync(user.Id, now, cancellationToken);
        dbContext.MfaTotpCredentials.Add(new MfaTotpCredential
        {
            Id = uuidGenerator.NewUuid(),
            UserId = user.Id,
            ProtectedSecret = secretProtector.Protect(user.Id, secret),
            CreatedAt = now,
            EnrolledAt = now,
            LastAcceptedTimeStep = acceptedStep,
        });
        var recoveryCodes = AddRecoveryCodes(user, now);
        user.MfaEnrolledAt = now;
        user.SecurityStamp = NewSecurityStamp();
        ResetLockout(user);
        ConsumeChallenge(challenge, now);
        dbContext.AuditEvents.Add(NewAudit(user.Id, correlationId, "AUTH_MFA_ENROLLED", user.Id, null, "SUCCESS"));
        await dbContext.SaveChangesAsync(cancellationToken);
        var session = await BuildSessionAsync(user, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        telemetry.MfaSucceeded();
        return new MfaCompletionResult(session, recoveryCodes, AuthenticationNextStep.RecoveryCodes);
    }

    public async Task<MfaCompletionResult> VerifyMfaAsync(
        Guid challengeId,
        string? totpCode,
        string? recoveryCode,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        if ((totpCode is null) == (recoveryCode is null))
        {
            throw new AuthenticationValidationException("Debe enviarse exactamente un código TOTP o de recuperación.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var challenge = await LockChallengeAsync(challengeId, AuthenticationChallengePurpose.VerifyMfa, cancellationToken);
        var user = await LockUserAsync(challenge.UserId, cancellationToken);
        EnsureChallengeCurrent(challenge, user);
        var now = clock.UtcNow;
        if (totpCode is not null)
        {
            var credential = await dbContext.MfaTotpCredentials
                .SingleOrDefaultAsync(item => item.UserId == user.Id && item.RevokedAt == null, cancellationToken)
                ?? throw new AuthenticationMfaCodeInvalidException();
            var secret = secretProtector.Unprotect(user.Id, credential.ProtectedSecret);
            if (!TotpCodes.TryValidate(secret, totpCode, now, credential.LastAcceptedTimeStep, out var acceptedStep))
            {
                await RecordMfaFailureAsync(user, challenge, correlationId, now, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                telemetry.MfaFailed();
                throw new AuthenticationMfaCodeInvalidException();
            }

            credential.LastAcceptedTimeStep = acceptedStep;
            credential.RowVersion++;
            ConsumeChallenge(challenge, now);
            ResetLockout(user);
            dbContext.AuditEvents.Add(NewAudit(user.Id, correlationId, "AUTH_MFA_SUCCEEDED", user.Id, "TOTP", "SUCCESS"));
            await dbContext.SaveChangesAsync(cancellationToken);
            var session = await BuildSessionAsync(user, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            telemetry.MfaSucceeded();
            return new MfaCompletionResult(session, [], AuthenticationNextStep.Authenticated);
        }

        var activeCodes = await dbContext.MfaRecoveryCodes
            .Where(item => item.UserId == user.Id && item.ConsumedAt == null && item.RevokedAt == null)
            .ToListAsync(cancellationToken);
        var matched = activeCodes.FirstOrDefault(item =>
            passwordHasher.VerifyHashedPassword(user, item.CodeHash, recoveryCode!) != PasswordVerificationResult.Failed);
        if (matched is null)
        {
            await RecordMfaFailureAsync(user, challenge, correlationId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            telemetry.MfaFailed();
            throw new AuthenticationMfaCodeInvalidException();
        }

        matched.ConsumedAt = now;
        matched.RowVersion++;
        challenge.Purpose = AuthenticationChallengePurpose.RegenerateRecoveryCodes;
        challenge.FailedAttemptCount = 0;
        challenge.ExpiresAt = now.Add(ChallengeLifetime);
        challenge.RowVersion++;
        dbContext.AuditEvents.Add(NewAudit(user.Id, correlationId, "AUTH_RECOVERY_CODE_USED", user.Id, null, "SUCCESS"));
        await dbContext.SaveChangesAsync(cancellationToken);
        var restrictedSession = await BuildSessionAsync(user, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        telemetry.RecoveryCodeUsed();
        return new MfaCompletionResult(restrictedSession, [], AuthenticationNextStep.RegenerateRecoveryCodes);
    }

    public async Task<MfaCompletionResult> RegenerateRecoveryCodesAsync(
        Guid? challengeId,
        Guid? authenticatedUserId,
        string currentPassword,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        RequirePasswordInput(currentPassword);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        AuthenticationChallenge? challenge = null;
        Guid userId;
        if (challengeId is Guid suppliedChallenge)
        {
            challenge = await LockChallengeAsync(suppliedChallenge, AuthenticationChallengePurpose.RegenerateRecoveryCodes, cancellationToken);
            userId = challenge.UserId;
        }
        else if (authenticatedUserId is Guid authenticated && authenticated != Guid.Empty)
        {
            userId = authenticated;
        }
        else
        {
            throw new AuthenticationChallengeInvalidException();
        }

        var user = await LockUserAsync(userId, cancellationToken);
        EnsureChallengeCurrent(challenge, user);
        var credential = await dbContext.IdentityCredentials.SingleAsync(item => item.UserId == user.Id, cancellationToken);
        if (passwordHasher.VerifyHashedPassword(user, credential.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
        {
            throw new AuthenticationFailedException();
        }

        var now = clock.UtcNow;
        var existing = await dbContext.MfaRecoveryCodes
            .Where(item => item.UserId == user.Id && item.ConsumedAt == null && item.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var item in existing)
        {
            item.RevokedAt = now;
            item.RowVersion++;
        }

        var recoveryCodes = AddRecoveryCodes(user, now);
        user.SecurityStamp = NewSecurityStamp();
        ResetLockout(user);
        if (challenge is not null)
        {
            ConsumeChallenge(challenge, now);
        }
        await ConsumePendingChallengesAsync(user.Id, now, cancellationToken, challenge?.Id);
        dbContext.AuditEvents.Add(NewAudit(user.Id, correlationId, "AUTH_RECOVERY_CODES_REGENERATED", user.Id, null, "SUCCESS"));
        await dbContext.SaveChangesAsync(cancellationToken);
        var session = await BuildSessionAsync(user, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return new MfaCompletionResult(session, recoveryCodes, AuthenticationNextStep.RecoveryCodes);
    }

    public async Task<SessionSnapshot> GetSessionAsync(
        Guid userId,
        string securityStamp,
        DateTimeOffset mfaAuthenticatedAt,
        DateTimeOffset absoluteExpiresAt,
        CancellationToken cancellationToken = default)
    {
        var session = await ValidateSessionAsync(
            userId, securityStamp, mfaAuthenticatedAt, absoluteExpiresAt, cancellationToken)
            ?? throw new AuthenticationSessionInvalidException();
        return new SessionSnapshot(
            session.UserId,
            session.PersonId,
            session.UserName,
            session.DisplayName,
            BranchScope.LorettaCode,
            session.RoleCode,
            session.Permissions,
            session.MfaAuthenticatedAt,
            clock.UtcNow.AddMinutes(30) <= session.AbsoluteExpiresAt
                ? clock.UtcNow.AddMinutes(30)
                : session.AbsoluteExpiresAt,
            session.AbsoluteExpiresAt);
    }

    public async Task<AuthenticatedSession?> ValidateSessionAsync(
        Guid userId,
        string securityStamp,
        DateTimeOffset mfaAuthenticatedAt,
        DateTimeOffset absoluteExpiresAt,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(securityStamp) ||
            mfaAuthenticatedAt > now || absoluteExpiresAt <= now || absoluteExpiresAt - mfaAuthenticatedAt > SessionLifetime)
        {
            return null;
        }

        var user = await dbContext.AppUsers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null || user.Status != AccountStatus.Active || user.MustChangePassword ||
            user.MfaEnrolledAt is null || !string.Equals(user.SecurityStamp, securityStamp, StringComparison.Ordinal))
        {
            dbContext.ChangeTracker.Clear();
            return null;
        }

        var identity = await LoadEligibleIdentityAsync(userId, cancellationToken);
        var hasTotp = await dbContext.MfaTotpCredentials.AsNoTracking()
            .CountAsync(item => item.UserId == userId && item.RevokedAt == null, cancellationToken) == 1;
        dbContext.ChangeTracker.Clear();
        return identity is null || !hasTotp
            ? null
            : ToSession(identity, user.SecurityStamp, mfaAuthenticatedAt, absoluteExpiresAt);
    }

    public Task RecordSessionRejectedAsync(
        Guid? userId,
        Guid correlationId,
        CancellationToken cancellationToken = default) =>
        WriteAuditOnlyAsync(
            userId,
            correlationId,
            "AUTH_SESSION_REJECTED",
            "SESSION_INVALID",
            "DENIED",
            cancellationToken);

    public Task RecordLogoutAsync(
        Guid? userId,
        Guid correlationId,
        CancellationToken cancellationToken = default) =>
        WriteAuditOnlyAsync(
            userId,
            correlationId,
            "AUTH_LOGOUT",
            reason: null,
            outcome: "SUCCESS",
            cancellationToken);

    private async Task<EligibleIdentity?> LoadEligibleIdentityAsync(Guid userId, CancellationToken cancellationToken)
    {
        var account = await (
            from user in dbContext.AppUsers.AsNoTracking()
            join credential in dbContext.IdentityCredentials.AsNoTracking() on user.Id equals credential.UserId
            join person in dbContext.People.AsNoTracking() on user.PersonId equals person.Id
            where user.Id == userId && user.Status == AccountStatus.Active
            select new { user.Id, user.PersonId, credential.UserName, person.DisplayName })
            .SingleOrDefaultAsync(cancellationToken);
        if (account is null)
        {
            return null;
        }

        var roles = await dbContext.RoleAssignmentVersions.AsNoTracking()
            .Where(item => item.UserId == userId && item.BranchId == BranchScope.LorettaId &&
                item.Status == RoleAssignmentStatus.Active && item.ValidTo == null)
            .Select(item => item.RoleCode)
            .ToListAsync(cancellationToken);
        var employments = await dbContext.EmploymentVersions.AsNoTracking()
            .CountAsync(item => item.PersonId == account.PersonId && item.BranchId == BranchScope.LorettaId &&
                item.Status == EmploymentStatus.Active && item.ValidTo == null, cancellationToken);
        return roles.Count == 1 && CanonicalRole.IsDefined(roles[0]) && employments == 1
            ? new EligibleIdentity(account.Id, account.PersonId, account.UserName, account.DisplayName, roles[0])
            : null;
    }

    private async Task<AuthenticatedSession> BuildSessionAsync(AppUser user, DateTimeOffset mfaAt, CancellationToken cancellationToken)
    {
        var identity = await LoadEligibleIdentityAsync(user.Id, cancellationToken)
            ?? throw new AuthenticationFailedException();
        return ToSession(identity, user.SecurityStamp, mfaAt, mfaAt.Add(SessionLifetime));
    }

    private static AuthenticatedSession ToSession(
        EligibleIdentity identity,
        string securityStamp,
        DateTimeOffset mfaAt,
        DateTimeOffset absoluteExpiresAt) => new(
            identity.UserId,
            identity.PersonId,
            identity.UserName,
            identity.DisplayName,
            identity.RoleCode,
            securityStamp,
            mfaAt,
            absoluteExpiresAt,
            RolePermissionProjection.ForRole(identity.RoleCode));

    private async Task<AppUser> LockUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.AppUsers.FromSqlInterpolated($"SELECT * FROM app_user WHERE id = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new AuthenticationFailedException();

    private async Task<AuthenticationChallenge> LockChallengeAsync(
        Guid challengeId,
        string purpose,
        CancellationToken cancellationToken)
    {
        var challenge = await dbContext.AuthenticationChallenges
            .FromSqlInterpolated($"SELECT * FROM authentication_challenge WHERE id = {challengeId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (challenge is null || challenge.Purpose != purpose || challenge.Status != AuthenticationChallengeStatus.Pending ||
            challenge.ExpiresAt <= clock.UtcNow)
        {
            throw new AuthenticationChallengeInvalidException();
        }

        return challenge;
    }

    private static void EnsureChallengeCurrent(AuthenticationChallenge? challenge, AppUser user)
    {
        if (challenge is not null && !string.Equals(challenge.SecurityStampSnapshot, user.SecurityStamp, StringComparison.Ordinal))
        {
            throw new AuthenticationChallengeInvalidException();
        }
    }

    private AuthenticationChallenge NewChallenge(AppUser user, string purpose, DateTimeOffset now) => new()
    {
        Id = uuidGenerator.NewUuid(),
        UserId = user.Id,
        Purpose = purpose,
        Status = AuthenticationChallengeStatus.Pending,
        SecurityStampSnapshot = user.SecurityStamp,
        CreatedAt = now,
        ExpiresAt = now.Add(ChallengeLifetime),
    };

    private async Task ConsumePendingChallengesAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        Guid? excludedChallengeId = null)
    {
        var pending = await dbContext.AuthenticationChallenges
            .Where(item => item.UserId == userId &&
                item.Status == AuthenticationChallengeStatus.Pending &&
                (excludedChallengeId == null || item.Id != excludedChallengeId))
            .ToListAsync(cancellationToken);
        foreach (var item in pending)
        {
            ConsumeChallenge(item, now);
        }
    }

    private static void ConsumeChallenge(AuthenticationChallenge challenge, DateTimeOffset now)
    {
        challenge.Status = AuthenticationChallengeStatus.Consumed;
        challenge.ConsumedAt = now;
        challenge.ProtectedTotpSecret = null;
        challenge.RowVersion++;
    }

    private async Task RecordMfaFailureAsync(
        AppUser user,
        AuthenticationChallenge challenge,
        Guid correlationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        challenge.FailedAttemptCount++;
        challenge.RowVersion++;
        ApplyFailedAttempt(user, now);
        if (challenge.FailedAttemptCount >= 5)
        {
            ConsumeChallenge(challenge, now);
        }

        dbContext.AuditEvents.Add(NewAudit(user.Id, correlationId, "AUTH_MFA_FAILED", user.Id, "INVALID_CODE", "DENIED"));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyFailedAttempt(AppUser user, DateTimeOffset now)
    {
        user.AccessFailedCount++;
        user.AuthenticationRowVersion++;
        if (user.AccessFailedCount < 5)
        {
            return;
        }

        user.AccessFailedCount = 0;
        user.LockoutLevel++;
        var minutes = user.LockoutLevel switch
        {
            1 => 15,
            2 => 30,
            _ => 60,
        };
        user.LockoutEndUtc = now.AddMinutes(minutes);
    }

    private static void ResetLockout(AppUser user)
    {
        user.AccessFailedCount = 0;
        user.LockoutLevel = 0;
        user.LockoutEndUtc = null;
        user.AuthenticationRowVersion++;
    }

    private async Task RevokeMfaAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var credentials = await dbContext.MfaTotpCredentials
            .Where(item => item.UserId == userId && item.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var item in credentials)
        {
            item.RevokedAt = now;
            item.RowVersion++;
        }

        var codes = await dbContext.MfaRecoveryCodes
            .Where(item => item.UserId == userId && item.ConsumedAt == null && item.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var item in codes)
        {
            item.RevokedAt = now;
            item.RowVersion++;
        }
    }

    private IReadOnlyList<string> AddRecoveryCodes(AppUser user, DateTimeOffset now)
    {
        var codes = TotpCodes.GenerateRecoveryCodes();
        foreach (var code in codes)
        {
            dbContext.MfaRecoveryCodes.Add(new MfaRecoveryCode
            {
                Id = uuidGenerator.NewUuid(),
                UserId = user.Id,
                CodeHash = passwordHasher.HashPassword(user, code),
                CreatedAt = now,
            });
        }

        return codes;
    }

    private async Task WriteAuditOnlyAsync(
        Guid? actorUserId,
        Guid correlationId,
        string action,
        string? reason,
        string outcome,
        CancellationToken cancellationToken) =>
        await auditTransaction.ExecuteAsync(
            NewAudit(actorUserId, correlationId, action, actorUserId, reason, outcome),
            _ => Task.CompletedTask,
            cancellationToken);

    private AuditEvent NewAudit(
        Guid? actorUserId,
        Guid correlationId,
        string action,
        Guid? resourceId,
        string? reason,
        string outcome)
    {
        using var afterData = action == "AUTH_PASSWORD_ACCEPTED"
            ? JsonSerializer.SerializeToDocument(new { schemaVersion = 1, nextStepRequired = true })
            : null;
        return new AuditEvent
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = clock.UtcNow,
            ActorUserId = actorUserId,
            ActorType = actorUserId is null ? "UNKNOWN" : "APP_USER",
            Action = action,
            ResourceType = "AUTHENTICATION",
            ResourceId = resourceId,
            BranchId = actorUserId is null ? null : BranchScope.LorettaId,
            CorrelationId = correlationId,
            AfterData = afterData is null ? null : JsonDocument.Parse(afterData.RootElement.GetRawText()),
            Reason = reason,
            Outcome = outcome,
        };
    }

    private static string RequireUserName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 200)
        {
            throw new AuthenticationValidationException("El usuario es obligatorio y no puede exceder 200 caracteres.");
        }

        return value.Trim();
    }

    private static void RequirePasswordInput(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 128)
        {
            throw new AuthenticationValidationException("La contraseña es obligatoria y no puede exceder 128 caracteres.");
        }
    }

    private static void ValidateNewPassword(string value)
    {
        RequirePasswordInput(value);
        if (value.Length < 14)
        {
            throw new AuthenticationPasswordPolicyException("La contraseña debe contener entre 14 y 128 caracteres.");
        }

        var normalized = value.ToUpperInvariant();
        if (normalized is "PASSWORDPASSWORD" or "CONTRASENACONTRASENA" or "12345678901234" or "QWERTYQWERTYQW")
        {
            throw new AuthenticationPasswordPolicyException("La contraseña forma parte de la lista local de valores comunes.");
        }
    }

    private static string NewSecurityStamp() => Convert.ToHexString(Guid.NewGuid().ToByteArray());

    private sealed record EligibleIdentity(
        Guid UserId,
        Guid PersonId,
        string UserName,
        string DisplayName,
        string RoleCode);
}
