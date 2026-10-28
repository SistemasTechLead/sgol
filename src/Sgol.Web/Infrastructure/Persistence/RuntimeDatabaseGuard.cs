using Npgsql;

namespace Sgol.Web.Infrastructure.Persistence;

public sealed class RuntimeDatabaseGuard(IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(configuration.GetConnectionString("Sgol"));
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls
                OR EXISTS(SELECT FROM pg_auth_members WHERE member=r.oid)
                OR has_schema_privilege(current_user,'public','CREATE')
                OR EXISTS(SELECT FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                    WHERE n.nspname='public' AND c.relowner=r.oid)
            FROM pg_roles r WHERE rolname=current_user
            """, connection);
        if (await command.ExecuteScalarAsync(cancellationToken) is not false)
            throw new InvalidOperationException("La cuenta runtime tiene privilegios de administración o propiedad. Ejecuta la preparación de seguridad con las cuentas separadas.");
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
