using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed partial class HostedAuthenticationPostgreSqlTests
{
    [Fact]
    public async Task TechEvid003HostedDownloadRequiresCompletedSessionAndRoutesInvalidUuidToClosedProblem()
    {
        using var factory = CreateFactory();
        var account = NewAccount(CanonicalRole.Direction);
        await SeedAsync(factory, [account]);
        var path = "/api/v1/files/" + Guid.CreateVersion7().ToString("D") + "/download";
        using (var anonymous = CreateClient(factory))
        {
            using var response = await anonymous.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore == true);
        }
        using (var preMfa = CreateClient(factory))
        {
            var csrf = await GetCsrfAsync(preMfa);
            using var login = await LoginAsync(preMfa, account.UserName, account.TemporaryPassword, csrf);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            using var response = await preMfa.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("AUTENTICACION_REQUERIDA", await ProblemCodeAsync(response));
        }
        var authenticated = await CompleteFirstAccessAsync(factory, account);
        using var client = authenticated.Client;
        using (var response = await client.GetAsync(path))
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("ARCHIVO_NO_ENCONTRADO", await ProblemCodeAsync(response));
            Assert.True(response.Headers.CacheControl?.NoStore == true);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
        using (var invalid = await client.GetAsync("/api/v1/files/not-a-uuid/download"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Equal("SOLICITUD_DESCARGA_INVALIDA", await ProblemCodeAsync(invalid));
        }
        using (var query = await client.GetAsync(path + "?ttl=300"))
            Assert.Equal(HttpStatusCode.BadRequest, query.StatusCode);
        using (var request = new HttpRequestMessage(HttpMethod.Get, path))
        {
            request.Headers.IfNoneMatch.Add(new("\"synthetic\""));
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore == true);
        }
        using (var logout = await PostAsync(client, "/api/v1/auth/logout", new { }, authenticated.Csrf))
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using (var afterLogout = await client.GetAsync(path))
            Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        Assert.Empty(await db.FileObjects.AsNoTracking().ToListAsync());
        Assert.Empty(await db.EvidenceVersions.AsNoTracking().ToListAsync());
    }
}
