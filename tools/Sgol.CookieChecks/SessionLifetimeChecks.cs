using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Authentication;
using Sgol.Web.Infrastructure.Persistence;

internal static class SessionLifetimeChecks
{
    // Reads persisted sessions in the dedicated security stack; never touches daily simulation.
    public static async Task RunAsync()
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Authentication:RequireMfa"] = "false" })
            .Build();
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(configuration.GetConnectionString("Sgol"));
        if (connection.Host is not ("localhost" or "127.0.0.1") || connection.Port != 5549 || connection.Database?.StartsWith("sgol_task_checks_security_") != true)
            throw new Exception("Session checks require the dedicated security database on port 5549.");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSgolHostedAuthentication();
        services.AddSgolPersistence(configuration);
        var clock = new CheckClock();
        services.AddSingleton<IClock>(clock);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(HostedAuthenticationDefaults.Scheme);
        if (options.ExpireTimeSpan != TimeSpan.FromDays(1) || HostedAuthenticationDefaults.SessionLifetime != TimeSpan.FromDays(1))
            throw new Exception("Cookie and absolute lifetime must both be 24 hours.");
        var db = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var authentication = scope.ServiceProvider.GetRequiredService<IHostedAuthenticationService>();
        var sessions = await db.PersonalSessions.AsNoTracking().Where(s => s.RevokedAt == null).OrderByDescending(s => s.IssuedAt).ToListAsync();
        foreach (var persisted in sessions)
        {
            var user = await db.AppUsers.AsNoTracking().SingleAsync(u => u.Id == persisted.UserId);
            var start = persisted.IssuedAt;
            var expires = persisted.ExpiresAt;
            clock.UtcNow = start;
            if (await authentication.ValidateSessionAsync(user.Id, user.SecurityStamp, start, expires, sessionId: persisted.Id) is null) continue;
            clock.UtcNow = start.AddHours(23);
            var snapshot = await authentication.GetSessionAsync(user.Id, user.SecurityStamp, start, expires, sessionId: persisted.Id);
            if (snapshot.IdleExpiresAt != expires || snapshot.AbsoluteExpiresAt != expires)
                throw new Exception("A session must survive 23 hours without extending beyond its original deadline.");
            if (await authentication.ValidateSessionAsync(user.Id, user.SecurityStamp, start, expires.AddSeconds(1), sessionId: persisted.Id) is not null)
                throw new Exception("Sessions longer than 24 hours must be rejected.");
            if (await authentication.ValidateSessionAsync(user.Id, "invalid-stamp", start, expires, sessionId: persisted.Id) is not null)
                throw new Exception("Revoked sessions must remain invalid.");
            clock.UtcNow = expires;
            if (await authentication.ValidateSessionAsync(user.Id, user.SecurityStamp, start, expires, sessionId: persisted.Id) is not null)
                throw new Exception("A session must expire exactly at 24 hours.");
            Console.WriteLine("Session lifetime passed: cookie 24h, valid after 23h idle, expiry at 24h, overlong and revoked sessions rejected.");
            return;
        }
        throw new Exception("No eligible persisted security-test session was found.");
    }

    private sealed class CheckClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }
}
