using System.Diagnostics.Metrics;

namespace Sgol.Web.Infrastructure.Authentication;

internal sealed class AuthenticationTelemetry : IDisposable
{
    private readonly Meter meter = new("Sgol.Authentication", "1.0.0");
    private readonly Counter<long> loginSucceeded;
    private readonly Counter<long> loginFailed;
    private readonly Counter<long> lockoutObserved;
    private readonly Counter<long> mfaSucceeded;
    private readonly Counter<long> mfaFailed;
    private readonly Counter<long> recoveryCodeUsed;
    private readonly Counter<long> sessionRejected;
    private readonly Counter<long> rateLimited;

    public AuthenticationTelemetry()
    {
        loginSucceeded = meter.CreateCounter<long>("sgol.authentication.login.succeeded");
        loginFailed = meter.CreateCounter<long>("sgol.authentication.login.failed");
        lockoutObserved = meter.CreateCounter<long>("sgol.authentication.lockout.observed");
        mfaSucceeded = meter.CreateCounter<long>("sgol.authentication.mfa.succeeded");
        mfaFailed = meter.CreateCounter<long>("sgol.authentication.mfa.failed");
        recoveryCodeUsed = meter.CreateCounter<long>("sgol.authentication.recovery_code.used");
        sessionRejected = meter.CreateCounter<long>("sgol.authentication.session.rejected");
        rateLimited = meter.CreateCounter<long>("sgol.authentication.rate_limited");
    }

    public void LoginSucceeded() => loginSucceeded.Add(1);
    public void LoginFailed() => loginFailed.Add(1);
    public void LockoutObserved() => lockoutObserved.Add(1);
    public void MfaSucceeded() => mfaSucceeded.Add(1);
    public void MfaFailed() => mfaFailed.Add(1);
    public void RecoveryCodeUsed() => recoveryCodeUsed.Add(1);
    public void SessionRejected() => sessionRejected.Add(1);
    public void RateLimited() => rateLimited.Add(1);

    public void Dispose() => meter.Dispose();
}
