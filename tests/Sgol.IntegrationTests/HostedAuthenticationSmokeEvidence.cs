using System.Text.Json;
using Xunit;

namespace Sgol.IntegrationTests;

internal enum HostedAuthenticationSmokeStage
{
    CSRF,
    LOGIN,
    PASSWORD_CHANGE,
    MFA_ENROLL,
    MFA_CONFIRM,
    MFA_VERIFY,
    RECOVERY_REGENERATE,
    SESSION_VALIDATE,
    LOGOUT,
    MFA_RESET,
    CLEANUP,
}

internal enum HostedAuthenticationSmokeScenario
{
    FIRST_ACCESS,
    FOUR_ROLES,
    RECOVERY_CODE,
    LOCKOUT,
    INVALIDATION,
    ADMINISTRATIVE_RESET,
}

internal enum HostedAuthenticationSmokeError
{
    NONE,
    PRECONDITION_FAILED,
    HOST_START_FAILED,
    HOST_NOT_READY,
    HTTP_CONTRACT_FAILED,
    DATABASE_CONTRACT_FAILED,
    PROCESS_EXIT_FAILED,
    CLEANUP_FAILED,
    UNEXPECTED_FAILURE,
}

internal sealed record HostedAuthenticationSmokeEvidence(
    string Stage,
    string Scenario,
    int Exit,
    string ErrorCode,
    string State)
{
    public static HostedAuthenticationSmokeEvidence Success(
        HostedAuthenticationSmokeStage stage,
        HostedAuthenticationSmokeScenario scenario) =>
        new(stage.ToString(), scenario.ToString(), 0, HostedAuthenticationSmokeError.NONE.ToString(), "PASSED");

    public static HostedAuthenticationSmokeEvidence Failure(
        HostedAuthenticationSmokeStage stage,
        HostedAuthenticationSmokeScenario scenario,
        HostedAuthenticationSmokeError error,
        int exit = 1) =>
        new(stage.ToString(), scenario.ToString(), exit, error.ToString(), "FAILED");

    public string ToJson() => JsonSerializer.Serialize(new
    {
        stage = Stage,
        scenario = Scenario,
        exit = Exit,
        errorCode = ErrorCode,
        state = State,
    });
}

public sealed class HostedAuthenticationSmokeEvidenceTests
{
    [Fact]
    public void EvidenceContainsOnlyClosedSanitizedFields()
    {
        const string forbidden = "synthetic-secret-marker";
        var evidence = HostedAuthenticationSmokeEvidence.Failure(
            HostedAuthenticationSmokeStage.MFA_VERIFY,
            HostedAuthenticationSmokeScenario.RECOVERY_CODE,
            HostedAuthenticationSmokeError.HTTP_CONTRACT_FAILED);

        using var document = JsonDocument.Parse(evidence.ToJson());
        var properties = document.RootElement.EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["errorCode", "exit", "scenario", "stage", "state"], properties);
        Assert.Equal("MFA_VERIFY", document.RootElement.GetProperty("stage").GetString());
        Assert.Equal("RECOVERY_CODE", document.RootElement.GetProperty("scenario").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("exit").GetInt32());
        Assert.Equal("HTTP_CONTRACT_FAILED", document.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal("FAILED", document.RootElement.GetProperty("state").GetString());
        Assert.DoesNotContain(forbidden, evidence.ToJson(), StringComparison.Ordinal);
    }
}
