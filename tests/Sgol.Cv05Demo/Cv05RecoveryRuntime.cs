using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.Continuity.Contracts;

namespace Sgol.Cv05Demo;

internal sealed partial class Cv05RecoveryRuntime(Cv05Infrastructure infrastructure, HostedSession direction)
{
    private const string BackupPrefix = "postgresql/v1/continuity/v1";
    private string replicaUri = null!;

    public async Task PrepareAsync(CancellationToken token)
    {
        var keys = await NativeProcess.RunAsync("docker",
            ["run", "--rm", "--platform", "linux/amd64", "--entrypoint", "/usr/bin/age-keygen",
                infrastructure.ImageId], infrastructure.RepositoryRoot, TimeSpan.FromSeconds(30), token);
        var combined = keys.Stdout + "\n" + keys.Stderr;
        var identity = Regex.Match(combined, "AGE-SECRET-KEY-[A-Z0-9]+", RegexOptions.CultureInvariant).Value;
        var recipient = Regex.Match(combined, "age1[023456789acdefghjklmnpqrstuvwxyz]+",
            RegexOptions.CultureInvariant).Value;
        if (keys.Exit != 0 || identity.Length == 0 || recipient.Length == 0)
        {
            var failure = new DemoFailureException("OCI", "NONE", "CV05_PRECONDITION_FAILED", keys.Exit);
            failure.Data["Stage"] = keys.Exit != 0 ? "AGE_KEYGEN_EXIT" :
                identity.Length == 0 ? "AGE_IDENTITY_MISSING" : "AGE_RECIPIENT_MISSING";
            throw failure;
        }

        var now = DateTimeOffset.UtcNow;
        var slot = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 5, 0, TimeSpan.Zero);
        if (slot > now) slot = slot.AddHours(-1);
        replicaUri = $"s3://{DemoContract.ManifestBucket}/objects/v1/{slot:yyyy/MM/dd}/" +
            $"objects-{slot:yyyyMMdd'T'HHmmss'Z'}.manifest.json";
        await infrastructure.WriteRuntimeEnvironmentAsync(recipient, identity, replicaUri, token);
        await RunWorkerJobAsync("REPLICATE_EVIDENCE_OBJECTS", slot, "REPLICA", token);
        using var store = infrastructure.CreateDestinationS3();
        using var replica = await store.GetObjectAsync(new GetObjectRequest
        {
            BucketName = DemoContract.ManifestBucket,
            Key = ParseObjectKey(replicaUri),
        }, token);
        if (replica.HttpStatusCode != HttpStatusCode.OK || replica.ContentLength <= 0)
            throw new DemoFailureException("REPLICA", "NONE", "CV05_STORE_FAILED");
    }

    public async Task<(Guid Id, string Etag)> RequestAsync(string reason, CancellationToken token)
    {
        using var response = await HostedAuthenticationClient.PostAsync(direction.Client,
            "/api/v1/continuity/reconciliations", new { reason }, direction.Csrf, token, Guid.CreateVersion7());
        if (response.StatusCode != HttpStatusCode.Created)
        {
            var failure = new DemoFailureException("RECONCILIATION", "NONE", "CV05_HTTP_CONTRACT_FAILED");
            await using var diagnosis = infrastructure.CreateContext();
            var rows = await diagnosis.RecoveryReconciliations.AsNoTracking().CountAsync(token);
            failure.Data["Stage"] = $"REQUEST_STATUS_{(int)response.StatusCode}_ROWS_{rows}";
            throw failure;
        }
        using var body = await HostedAuthenticationClient.ReadJsonAsync(response, token);
        var id = body.RootElement.GetProperty("data").GetProperty("reconciliationId").GetGuid();
        var etag = response.Headers.ETag?.ToString();
        if (id == Guid.Empty || etag is null)
            throw new DemoFailureException("RECONCILIATION", "NONE", "CV05_HTTP_CONTRACT_FAILED");
        return (id, etag);
    }

    public async Task CaptureAsync(Guid id, CancellationToken token)
    {
        var name = $"sgol-cv05-outbox-{Guid.CreateVersion7():N}";
        var started = await NativeProcess.RunAsync("docker",
            ["run", "--detach", "--rm", "--name", name, "--platform", "linux/amd64",
                "--network", infrastructure.NetworkName, "--env-file", infrastructure.RuntimeEnvironmentPath,
                infrastructure.ImageId, "dotnet", "Sgol.Worker.dll", "outbox"],
            infrastructure.RepositoryRoot, TimeSpan.FromSeconds(30), token);
        if (started.Exit != 0)
            throw new DemoFailureException("BACKUP", "NONE", "CV05_PRECONDITION_FAILED");
        infrastructure.RegisterTransientContainer(name);
        try
        {
            var deadline = DateTimeOffset.UtcNow.AddMinutes(15);
            while (DateTimeOffset.UtcNow < deadline)
            {
                await using var context = infrastructure.CreateContext();
                var status = await context.RecoveryReconciliationEvents.AsNoTracking()
                    .Where(item => item.ReconciliationId == id).OrderByDescending(item => item.Sequence)
                    .Select(item => item.Status).FirstAsync(token);
                if (status == RecoveryReconciliationStatuses.ReferenceCapturing) break;
                if (status == RecoveryReconciliationStatuses.Failed)
                    throw new DemoFailureException("BACKUP", "NONE", "CV05_SCENARIO_FAILED");
                await Task.Delay(250, token);
            }
            await using var verification = infrastructure.CreateContext();
            var terminal = await verification.RecoveryReconciliationEvents.AsNoTracking()
                .Where(item => item.ReconciliationId == id).OrderByDescending(item => item.Sequence)
                .Select(item => item.Status).FirstAsync(token);
            if (terminal != RecoveryReconciliationStatuses.ReferenceCapturing)
                throw new DemoFailureException("BACKUP", "NONE", "CV05_SCENARIO_FAILED");
        }
        finally
        {
            var stopped = await NativeProcess.RunAsync("docker", ["stop", name],
                infrastructure.RepositoryRoot, TimeSpan.FromSeconds(30), CancellationToken.None);
            if (stopped.Exit == 0) infrastructure.UnregisterTransientContainer(name);
        }
        await RunOperationsAsync("BACKUP", ["complete-functional-reference", "--reconciliation-id", id.ToString("D"),
            "--reference", ReferenceUri(id), "--backup-manifest", BackupUri(id),
            "--replica-manifest", replicaUri], [0], token);
        using var statusResponse = await direction.Client.GetAsync($"/api/v1/continuity/reconciliations/{id:D}", token);
        if (statusResponse.StatusCode != HttpStatusCode.OK)
            throw new DemoFailureException("BACKUP", "NONE", "CV05_HTTP_CONTRACT_FAILED");
        using var statusBody = await HostedAuthenticationClient.ReadJsonAsync(statusResponse, token);
        if (statusBody.RootElement.GetProperty("data").GetProperty("status").GetString()
            != RecoveryReconciliationStatuses.ReferenceReady)
            throw new DemoFailureException("BACKUP", "NONE", "CV05_SCENARIO_FAILED");
    }

    public async Task<string> RestoreAsync(Guid id, CancellationToken token)
    {
        await ResetRestoreDatabaseAsync(token);
        var restore = await RunOperationsAsync("RESTORE", ["verify-postgresql-backup", "--manifest", BackupUri(id)],
            [0], token);
        var line = restore.Stdout.Split('\n').Select(item => item.Trim())
            .LastOrDefault(item => item.StartsWith("{\"kind\":\"SGOL_TECHNICAL_RESTORE_EVIDENCE\"",
                StringComparison.Ordinal));
        if (line is null)
            throw new DemoFailureException("RESTORE", "NONE", "CV05_REPORT_FAILED");
        using (var parsed = JsonDocument.Parse(line))
        {
            if (parsed.RootElement.GetProperty("kind").GetString() != "SGOL_TECHNICAL_RESTORE_EVIDENCE")
                throw new DemoFailureException("RESTORE", "NONE", "CV05_REPORT_FAILED");
        }
        var path = Path.Combine(infrastructure.PrivateDirectory, $"restore-{id:N}.json");
        await File.WriteAllTextAsync(path, line, new UTF8Encoding(false), token);
        return path;
    }

    public async Task<(int Exit, JsonDocument Details)> ReconcileAsync(Guid id, string evidencePath,
        int[] expectedExits, CancellationToken token)
    {
        var exit = await ExecuteReconcileCommandAsync(id, evidencePath, expectedExits, token);
        using var response = await direction.Client.GetAsync($"/api/v1/continuity/reconciliations/{id:D}", token);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new DemoFailureException("RECONCILIATION", "NONE", "CV05_HTTP_CONTRACT_FAILED");
        var details = await HostedAuthenticationClient.ReadJsonAsync(response, token);
        return (exit, details);
    }

    public async Task<int> ExecuteReconcileCommandAsync(Guid id, string evidencePath, int[] expectedExits,
        CancellationToken token)
    {
        var result = await RunOperationsAsync("RECONCILIATION",
            ["reconcile-functional-restore", "--reconciliation-id", id.ToString("D"),
                "--reference-manifest", ReferenceUri(id), "--restore-evidence", ContainerPath(evidencePath)],
            expectedExits, token);
        return result.Exit;
    }

    public async Task CompleteReferenceAgainAsync(Guid id, CancellationToken token) =>
        _ = await RunOperationsAsync("BACKUP", ["complete-functional-reference", "--reconciliation-id", id.ToString("D"),
            "--reference", ReferenceUri(id), "--backup-manifest", BackupUri(id),
            "--replica-manifest", replicaUri], [0], token);

    public async Task CorruptReferenceAsync(Guid id, CancellationToken token)
    {
        using var store = infrastructure.CreateDestinationS3();
        using var bytes = new MemoryStream("{\"invalid\":true}"u8.ToArray());
        var response = await store.PutObjectAsync(new PutObjectRequest
        {
            BucketName = DemoContract.ManifestBucket,
            Key = ParseObjectKey(ReferenceUri(id)),
            InputStream = bytes,
            ContentType = "application/json",
        }, token);
        if (response.HttpStatusCode != HttpStatusCode.OK)
            throw new DemoFailureException("RECONCILIATION", "NONE", "CV05_STORE_FAILED");
    }

    public async Task MutateRestoreAsync(string table, CancellationToken token)
    {
        if (table is not ("evidence_version" or "audit_event"))
            throw new DemoFailureException("RESTORE", "NONE", "CV05_PRECONDITION_FAILED");
        await using var connection = new NpgsqlConnection(infrastructure.RestoreHostConnectionString);
        await connection.OpenAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await using (var role = new NpgsqlCommand("SET LOCAL session_replication_role = replica", connection, transaction))
            await role.ExecuteNonQueryAsync(token);
        await using (var remove = new NpgsqlCommand(
            $"DELETE FROM {table} WHERE id = (SELECT id FROM {table} ORDER BY id LIMIT 1)",
            connection, transaction))
        {
            if (await remove.ExecuteNonQueryAsync(token) != 1)
                throw new DemoFailureException("RESTORE", "NONE", "CV05_DATABASE_CONTRACT_FAILED");
        }
        await transaction.CommitAsync(token);
    }

    public static string ReferenceUri(Guid id) =>
        $"s3://{DemoContract.ManifestBucket}/{BackupPrefix}/{id:D}/reference.manifest.json";
    public static string BackupUri(Guid id) =>
        $"s3://{DemoContract.ManifestBucket}/{BackupPrefix}/{id:D}/postgresql.dump.age.manifest.json";
    private static string ParseObjectKey(string uri) => uri[(uri.IndexOf('/', "s3://".Length) + 1)..];
    private static string ContainerPath(string hostPath) => "/run/sgol/" + Path.GetFileName(hostPath);

    private async Task RunWorkerJobAsync(string job, DateTimeOffset slot, string phase, CancellationToken token) =>
        _ = await RunContainerAsync(phase,
            ["dotnet", "Sgol.Worker.dll", "run-job", "--job", job, "--scheduled-for",
                slot.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)],
            [0], token);

    private Task<NativeResult> RunOperationsAsync(string phase, string[] arguments, int[] expectedExits,
        CancellationToken token) => RunContainerAsync(phase,
            ["dotnet", "Sgol.Operations.dll", .. arguments], expectedExits, token);

    private async Task<NativeResult> RunContainerAsync(string phase, string[] command, int[] expectedExits,
        CancellationToken token)
    {
        var args = new List<string>
        {
            "run", "--rm", "--platform", "linux/amd64", "--network", infrastructure.NetworkName,
            "--env-file", infrastructure.RuntimeEnvironmentPath,
            "--mount", $"type=bind,source={infrastructure.PrivateDirectory},target=/run/sgol",
            infrastructure.ImageId,
        };
        args.AddRange(command);
        var result = await NativeProcess.RunAsync("docker", args, infrastructure.RepositoryRoot,
            TimeSpan.FromMinutes(15), token);
        if (!expectedExits.Contains(result.Exit))
            throw new DemoFailureException(phase, "NONE", "CV05_SCENARIO_FAILED", result.Exit);
        return result;
    }

    private async Task ResetRestoreDatabaseAsync(CancellationToken token)
    {
        using var pooledRestore = new NpgsqlConnection(infrastructure.RestoreHostConnectionString);
        NpgsqlConnection.ClearPool(pooledRestore);
        var builder = new NpgsqlConnectionStringBuilder(infrastructure.RestoreHostConnectionString);
        if (builder.Host is not ("127.0.0.1" or "localhost") ||
            !builder.Database!.StartsWith("sgol_cv05_restore_", StringComparison.Ordinal))
            throw new DemoFailureException("RESTORE", "NONE", "CV05_PRECONDITION_FAILED");
        var database = builder.Database;
        builder.Database = "postgres";
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(token);
        await using var close = new NpgsqlCommand(
            $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{database}' AND pid <> pg_backend_pid()",
            connection);
        await close.ExecuteNonQueryAsync(token);
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\"", connection);
        await drop.ExecuteNonQueryAsync(token);
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
        await create.ExecuteNonQueryAsync(token);
        NpgsqlConnection.ClearPool(pooledRestore);
    }
}
