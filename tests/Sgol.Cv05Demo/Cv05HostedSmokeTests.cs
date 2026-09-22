using System.Net;
using Sgol.Identity.Contracts;
using Xunit;

namespace Sgol.Cv05Demo;

[Trait("Category", "CV05_FOCUSED")]
public sealed class Cv05HostedSmokeTests
{
    [Fact]
    public async Task HttpsAndHostedAuthenticationUseDisposableRealDependencies()
    {
        var infrastructure = new Cv05Infrastructure("sha256:" + new string('a', 64));
        string? failure = null;
        try
        {
            await infrastructure.StartAsync(CancellationToken.None);
            var account = new DemoAccount(infrastructure.DirectionUserId,
                infrastructure.DirectionPersonId, infrastructure.DirectionUserName,
                CanonicalRole.Direction, infrastructure.DirectionTemporaryPassword,
                infrastructure.DirectionNewPassword, "DIR-CV05");
            var session = await HostedAuthenticationClient.CompleteFirstAccessAsync(
                infrastructure.CreateClient(), account, CancellationToken.None);
            using var response = await session.Client.GetAsync("/api/v1/auth/session");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("https", infrastructure.BaseAddress.Scheme);
            Assert.Equal("127.0.0.1", infrastructure.BaseAddress.Host);
        }
        catch (DemoFailureException exception)
        {
            failure = $"{exception.Phase}:{exception.Code}:{exception.Data["Category"] ?? "NONE"}:" +
                $"{exception.Data["Health"] ?? "NONE"}";
        }
        catch
        {
            failure = "CV05_UNEXPECTED_FAILURE";
        }
        finally
        {
            if (!await infrastructure.CleanupAsync())
                failure = failure is null ? "CV05_CLEANUP_FAILED" : $"{failure};CV05_CLEANUP_FAILED";
        }
        Assert.Null(failure);
    }
}
