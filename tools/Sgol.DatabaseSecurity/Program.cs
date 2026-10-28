using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Sgol.Web.Infrastructure.Persistence;

var connection = Environment.GetEnvironmentVariable("SGOL_MAINTENANCE_CONNECTION")
    ?? throw new InvalidOperationException("Falta conexión de mantenimiento (solo en este proceso).");
var settings = new NpgsqlConnectionStringBuilder(connection);
var isolated = settings.Database?.StartsWith("sgol_task_checks_security_", StringComparison.Ordinal) == true;
if (settings.Host is not ("localhost" or "127.0.0.1") ||
    !(isolated || settings.Database == "sgol_local" && args.Contains("--activate-local")))
    throw new InvalidOperationException("Destino no autorizado.");
var prefix = isolated ? settings.Database! : "sgol";
var migrator = prefix + "_migrator";
var runtime = prefix + "_runtime";
string Q(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
await using var admin = new NpgsqlConnection(connection);
await admin.OpenAsync();
if (args.Contains("--rotate-maintenance-only"))
{
    var newPassword = Environment.GetEnvironmentVariable("SGOL_NEW_MAINTENANCE_PASSWORD") ?? throw new Exception("Falta secreto nuevo.");
    await using var format = new NpgsqlCommand("SELECT format('ALTER ROLE %I PASSWORD %L', current_user, @password)", admin);
    format.Parameters.AddWithValue("password", newPassword);
    var sql = (string)(await format.ExecuteScalarAsync())!;
    await using (var change = new NpgsqlCommand(sql, admin)) await change.ExecuteNonQueryAsync();
    var verifySettings = new NpgsqlConnectionStringBuilder(connection) { Password = newPassword, Pooling = false };
    await using var verify = new NpgsqlConnection(verifySettings.ConnectionString);
    await verify.OpenAsync();
    Console.WriteLine("Contraseña de mantenimiento rotada y conexión nueva verificada; runtime no usa esta cuenta.");
    return;
}
if (args.Contains("--cleanup-expired-sessions"))
{
    await using var cleanup = new NpgsqlCommand("DELETE FROM personal_session WHERE expires_at < CURRENT_TIMESTAMP - interval '90 days'", admin);
    var count = await cleanup.ExecuteNonQueryAsync();
    Console.WriteLine($"Sesiones con expiración anterior a 90 días eliminadas: {count}; auditoría conservada.");
    return;
}
var migrationPassword = Environment.GetEnvironmentVariable("SGOL_MIGRATION_PASSWORD") ?? throw new Exception("Falta secreto migrador.");
var runtimePassword = Environment.GetEnvironmentVariable("SGOL_RUNTIME_PASSWORD") ?? throw new Exception("Falta secreto runtime.");
await using (var transaction = await admin.BeginTransactionAsync())
{
    // Passwords are SQL parameters to format(), never command arguments or console output.
    foreach (var (role, password) in new[] { (migrator, migrationPassword), (runtime, runtimePassword) })
    {
        await using var exists = new NpgsqlCommand("SELECT EXISTS(SELECT FROM pg_roles WHERE rolname=@role)", admin, transaction);
        exists.Parameters.AddWithValue("role", role);
        var present = (bool)(await exists.ExecuteScalarAsync())!;
        await using var format = new NpgsqlCommand("SELECT format('" + (present ? "ALTER" : "CREATE") +
            " ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %L', @role, @password)", admin, transaction);
        format.Parameters.AddWithValue("role", role); format.Parameters.AddWithValue("password", password);
        var sql = (string)(await format.ExecuteScalarAsync())!;
        await using var create = new NpgsqlCommand(sql, admin, transaction);
        await create.ExecuteNonQueryAsync();
    }
    // Only transfer application relations/functions, never cluster/shared objects or extensions.
    var statements = new List<string>();
    await using (var objects = new NpgsqlCommand("""
        SELECT format('ALTER %s %I.%I OWNER TO %I',
          CASE c.relkind WHEN 'S' THEN 'SEQUENCE' WHEN 'v' THEN 'VIEW' WHEN 'm' THEN 'MATERIALIZED VIEW' ELSE 'TABLE' END,
          n.nspname,c.relname,@owner)
        FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
        WHERE n.nspname='public' AND c.relkind IN ('r','p','S','v','m')
          AND NOT EXISTS(SELECT FROM pg_depend d WHERE d.objid=c.oid AND d.deptype='e')
          AND NOT (c.relkind='S' AND EXISTS(SELECT FROM pg_depend d WHERE d.objid=c.oid
            AND d.classid='pg_class'::regclass AND d.deptype IN ('a','i')))
        UNION ALL
        SELECT format('ALTER FUNCTION %s OWNER TO %I',p.oid::regprocedure,@owner)
        FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='public'
          AND NOT EXISTS(SELECT FROM pg_depend d WHERE d.objid=p.oid AND d.deptype='e')
        """, admin, transaction))
    {
        objects.Parameters.AddWithValue("owner", migrator);
        await using var reader = await objects.ExecuteReaderAsync();
        while (await reader.ReadAsync()) statements.Add(reader.GetString(0));
    }
    statements.Insert(0, $"ALTER DATABASE {Q(settings.Database!)} OWNER TO {Q(migrator)}");
    statements.Add($"ALTER SCHEMA public OWNER TO {Q(migrator)}");
    statements.Add("REVOKE CREATE ON SCHEMA public FROM PUBLIC");
    statements.Add($"REVOKE ALL ON DATABASE {Q(settings.Database!)} FROM PUBLIC");
    statements.Add($"GRANT CONNECT ON DATABASE {Q(settings.Database!)} TO {Q(runtime)}");
    statements.Add($"GRANT USAGE ON SCHEMA public TO {Q(runtime)}");
    // Grant future objects before migration; audited/history restrictions are tightened below.
    statements.Add($"ALTER DEFAULT PRIVILEGES FOR ROLE {Q(migrator)} IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {Q(runtime)}");
    statements.Add($"ALTER DEFAULT PRIVILEGES FOR ROLE {Q(migrator)} IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO {Q(runtime)}");
    foreach (var sql in statements) { await using var command = new NpgsqlCommand(sql, admin, transaction); await command.ExecuteNonQueryAsync(); }
    await transaction.CommitAsync();
}
var migration = new NpgsqlConnectionStringBuilder(connection) { Username = migrator, Password = migrationPassword };
var options = new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(migration.ConnectionString).Options;
await using (var db = new SgolDbContext(options)) await db.Database.MigrateAsync();
await using (var command = new NpgsqlCommand($"""
    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {Q(runtime)};
    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {Q(runtime)};
    REVOKE ALL ON TABLE "__EFMigrationsHistory" FROM {Q(runtime)};
    REVOKE UPDATE, DELETE, TRUNCATE ON audit_event FROM {Q(runtime)};
    REVOKE UPDATE, DELETE, TRUNCATE ON data_protection_key FROM {Q(runtime)};
    REVOKE DELETE, TRUNCATE ON personal_session FROM {Q(runtime)};
    """, admin)) await command.ExecuteNonQueryAsync();
// No role memberships, schema creation or ownership are permitted for runtime.
await using (var check = new NpgsqlCommand("""
    SELECT r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls
      OR EXISTS(SELECT FROM pg_auth_members WHERE member=r.oid)
      OR has_schema_privilege(r.oid, 'public', 'CREATE')
    FROM pg_roles r WHERE rolname=@role
    """, admin))
{
    check.Parameters.AddWithValue("role", runtime);
    if ((bool)(await check.ExecuteScalarAsync())!) throw new Exception("Runtime conserva privilegios no permitidos.");
}
Console.WriteLine($"Preparación verificada: {settings.Database}; migrador/propietario y runtime separados; mantenimiento no se entrega a Web.");
