using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Authentication;
using Sgol.Web.Infrastructure.Persistence;

internal static class SessionLifetimeChecks
{
    // Reads existing local identities without logging in or modifying their credentials.
    public static async Task RunAsync()
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Authentication:RequireMfa"] = "false" })
            .Build();
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(configuration.GetConnectionString("Sgol"));
        if (connection.Host != "localhost" || connection.Database != "sgol_local")
            throw new Exception("Session checks require the local sgol_local database.");
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
        var users = await db.AppUsers.AsNoTracking().Where(user => !user.MustChangePassword).ToListAsync();
        var start = clock.UtcNow;
        foreach (var user in users)
        {
            var expires = start.AddDays(1);
            if (await authentication.ValidateSessionAsync(user.Id, user.SecurityStamp, start, expires) is null) continue;
            clock.UtcNow = start.AddHours(23);
            var snapshot = await authentication.GetSessionAsync(user.Id, user.SecurityStamp, start, expires);
            if (snapshot.IdleExpiresAt != expires || snapshot.AbsoluteExpiresAt != expires)
                throw new Exception("A session must survive 23 hours without extending beyond its original deadline.");
            if (await authentication.ValidateSessionAsync(user.Id, user.SecurityStamp, start, expires.AddSeconds(1)) is not null)
                throw new Exception("Sessions longer than 24 hours must be rejected.");
            if (await authentication.ValidateSessionAsync(user.Id, "invalid-stamp", start, expires) is not null)
                throw new Exception("Revoked sessions must remain invalid.");
            clock.UtcNow = expires;
            if (await authentication.ValidateSessionAsync(user.Id, user.SecurityStamp, start, expires) is not null)
                throw new Exception("A session must expire exactly at 24 hours.");
            Console.WriteLine("Session lifetime passed: cookie 24h, valid after 23h idle, expiry at 24h, overlong and revoked sessions rejected.");
            return;
        }
        throw new Exception("No eligible local identity was found for read-only session checks.");
    }

    private sealed class CheckClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }
}
