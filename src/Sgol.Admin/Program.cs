using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Hosting;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

const string PasswordEnvironmentVariable = "SGOL_BOOTSTRAP_INITIAL_PASSWORD";

var initialPassword = Environment.GetEnvironmentVariable(PasswordEnvironmentVariable);
Environment.SetEnvironmentVariable(PasswordEnvironmentVariable, null);

if (string.IsNullOrEmpty(initialPassword))
{
    Console.Error.WriteLine($"Required ephemeral injection {PasswordEnvironmentVariable} is missing.");
    return 2;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "O";
    options.UseUtcTimestamp = true;
});
builder.Services.AddSgolPersistence(builder.Configuration);

using var host = builder.Build();
await using var scope = host.Services.CreateAsyncScope();
var bootstrap = scope.ServiceProvider.GetRequiredService<DirectionBootstrapService>();

try
{
    await bootstrap.ExecuteAsync(new DirectionBootstrapInput
    {
        PersonStableCode = RequireEnvironmentVariable("SGOL_BOOTSTRAP_PERSON_CODE"),
        PersonDisplayName = RequireEnvironmentVariable("SGOL_BOOTSTRAP_PERSON_DISPLAY_NAME"),
        UserName = RequireEnvironmentVariable("SGOL_BOOTSTRAP_USER_NAME"),
        InitialPassword = initialPassword,
    });
    Console.WriteLine("DIRECCION bootstrap completed; the bootstrap path is now permanently disabled.");
    return 0;
}
catch (DirectionBootstrapAlreadyCompletedException)
{
    Console.Error.WriteLine("DIRECCION bootstrap is permanently disabled.");
    return 3;
}
catch (Exception)
{
    Console.Error.WriteLine("DIRECCION bootstrap failed without committing changes.");
    return 1;
}

static string RequireEnvironmentVariable(string name) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"Required input {name} is missing.");
