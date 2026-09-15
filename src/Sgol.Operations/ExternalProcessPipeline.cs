using System.Diagnostics;
using System.Text;
using Npgsql;

namespace Sgol.Operations;

public interface IBackupProcessPipeline
{
    Task<BackupProcessResult> CreateEncryptedDumpAsync(
        BackupOptions options,
        string outputPath,
        CancellationToken cancellationToken);

    Task<BackupProcessResult> CreateEncryptedDumpFromSnapshotAsync(
        BackupOptions options,
        string outputPath,
        string postgreSqlSnapshotId,
        CancellationToken cancellationToken) =>
        throw new OperationsIntegrityException("REFERENCE_BACKUP_SNAPSHOT_UNSUPPORTED");

    Task<RestoreProcessResult> VerifyAndRestoreAsync(
        string agePath,
        string pgRestorePath,
        string encryptedPath,
        string identity,
        NpgsqlConnectionStringBuilder destination,
        CancellationToken cancellationToken);
}

public sealed record BackupProcessResult(
    string PostgreSqlVersion,
    string PgDumpVersion,
    string AgeVersion);

public sealed record RestoreProcessResult(string PgRestoreVersion, string AgeVersion);

public sealed class BackupProcessPipeline : IBackupProcessPipeline
{
    private const int MaximumDiagnosticCharacters = 4096;

    public async Task<BackupProcessResult> CreateEncryptedDumpAsync(
        BackupOptions options,
        string outputPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var connection = options.ParseConnection();
        var dump = CreateDumpProcess(options, connection, null);
        return await RunEncryptedDumpAsync(options, outputPath, dump, cancellationToken);
    }

    public async Task<BackupProcessResult> CreateEncryptedDumpFromSnapshotAsync(
        BackupOptions options,
        string outputPath,
        string postgreSqlSnapshotId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(postgreSqlSnapshotId) || postgreSqlSnapshotId.Length > 128 ||
            postgreSqlSnapshotId.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or ':')))
            throw new OperationsIntegrityException("REFERENCE_BACKUP_SNAPSHOT_INVALID");
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var dump = CreateDumpProcess(options, options.ParseConnection(), postgreSqlSnapshotId);
        return await RunEncryptedDumpAsync(options, outputPath, dump, cancellationToken);
    }

    private static ProcessStartInfo CreateDumpProcess(
        BackupOptions options,
        NpgsqlConnectionStringBuilder connection,
        string? postgreSqlSnapshotId)
    {
        var dump = CreatePostgresProcess(options.PgDumpPath, connection);
        dump.ArgumentList.Add("--format=custom");
        dump.ArgumentList.Add("--no-owner");
        dump.ArgumentList.Add("--no-privileges");
        dump.ArgumentList.Add("--compress=6");
        if (postgreSqlSnapshotId is not null) dump.ArgumentList.Add($"--snapshot={postgreSqlSnapshotId}");
        return dump;
    }

    private static async Task<BackupProcessResult> RunEncryptedDumpAsync(
        BackupOptions options,
        string outputPath,
        ProcessStartInfo dump,
        CancellationToken cancellationToken)
    {

        var age = BaseProcess(options.AgePath);
        age.ArgumentList.Add("--encrypt");
        age.ArgumentList.Add("--recipient");
        age.ArgumentList.Add(options.Recipient);

        using var dumpProcess = Process.Start(dump) ?? throw new OperationsIntegrityException("PG_DUMP_START_FAILED");
        using var ageProcess = Process.Start(age) ?? throw new OperationsIntegrityException("AGE_START_FAILED");
        var dumpError = ReadBoundedAsync(dumpProcess.StandardError, cancellationToken);
        var ageError = ReadBoundedAsync(ageProcess.StandardError, cancellationToken);

        try
        {
            await using var destination = new FileStream(
                outputPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var pipe = dumpProcess.StandardOutput.BaseStream.CopyToAsync(ageProcess.StandardInput.BaseStream, cancellationToken);
            var encrypted = ageProcess.StandardOutput.BaseStream.CopyToAsync(destination, cancellationToken);
            await pipe;
            ageProcess.StandardInput.Close();
            await Task.WhenAll(encrypted, dumpProcess.WaitForExitAsync(cancellationToken), ageProcess.WaitForExitAsync(cancellationToken));
            await destination.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Kill(dumpProcess);
            Kill(ageProcess);
            throw;
        }

        _ = await dumpError;
        _ = await ageError;
        if (dumpProcess.ExitCode != 0)
        {
            throw new OperationsIntegrityException("PG_DUMP_FAILED");
        }

        if (ageProcess.ExitCode != 0)
        {
            throw new OperationsIntegrityException("BACKUP_ENCRYPTION_FAILED");
        }

        var pgDumpVersion = await ReadVersionAsync(options.PgDumpPath, cancellationToken);
        var ageVersion = await ReadVersionAsync(options.AgePath, cancellationToken);
        return new BackupProcessResult("queried-separately", pgDumpVersion, ageVersion);
    }

    public async Task<RestoreProcessResult> VerifyAndRestoreAsync(
        string agePath,
        string pgRestorePath,
        string encryptedPath,
        string identity,
        NpgsqlConnectionStringBuilder destination,
        CancellationToken cancellationToken)
    {
        await VerifyArchiveAsync(agePath, pgRestorePath, encryptedPath, identity, cancellationToken);

        var database = destination.Database;
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new OperationsConfigurationException("OPERATIONS_CONFIGURATION_REQUIRED");
        }

        var age = BaseProcess(agePath);
        age.ArgumentList.Add("--decrypt");
        age.ArgumentList.Add("--identity");
        age.ArgumentList.Add(identity);
        age.ArgumentList.Add(encryptedPath);

        var restore = CreatePostgresProcess(pgRestorePath, destination);
        restore.ArgumentList.Add("--dbname");
        restore.ArgumentList.Add(database);
        restore.ArgumentList.Add("--exit-on-error");
        restore.ArgumentList.Add("--no-owner");
        restore.ArgumentList.Add("--no-privileges");

        using var ageProcess = Process.Start(age) ?? throw new OperationsIntegrityException("AGE_START_FAILED");
        using var restoreProcess = Process.Start(restore) ?? throw new OperationsIntegrityException("PG_RESTORE_START_FAILED");
        var ageError = ReadBoundedAsync(ageProcess.StandardError, cancellationToken);
        var restoreError = ReadBoundedAsync(restoreProcess.StandardError, cancellationToken);
        var restoreOutput = ReadBoundedAsync(restoreProcess.StandardOutput, cancellationToken);
        try
        {
            await ageProcess.StandardOutput.BaseStream.CopyToAsync(restoreProcess.StandardInput.BaseStream, cancellationToken);
            restoreProcess.StandardInput.Close();
            await Task.WhenAll(ageProcess.WaitForExitAsync(cancellationToken), restoreProcess.WaitForExitAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            Kill(ageProcess);
            Kill(restoreProcess);
            throw;
        }

        _ = await ageError;
        _ = await restoreError;
        _ = await restoreOutput;
        if (ageProcess.ExitCode != 0)
        {
            throw new OperationsIntegrityException("BACKUP_DECRYPTION_FAILED");
        }

        if (restoreProcess.ExitCode != 0)
        {
            throw new OperationsIntegrityException("PG_RESTORE_FAILED");
        }

        return new RestoreProcessResult(
            await ReadVersionAsync(pgRestorePath, cancellationToken),
            await ReadVersionAsync(agePath, cancellationToken));
    }

    private static async Task VerifyArchiveAsync(
        string agePath,
        string pgRestorePath,
        string encryptedPath,
        string identity,
        CancellationToken cancellationToken)
    {
        var age = BaseProcess(agePath);
        age.ArgumentList.Add("--decrypt");
        age.ArgumentList.Add("--identity");
        age.ArgumentList.Add(identity);
        age.ArgumentList.Add(encryptedPath);
        var list = BaseProcess(pgRestorePath);
        list.ArgumentList.Add("--list");

        using var ageProcess = Process.Start(age) ?? throw new OperationsIntegrityException("AGE_START_FAILED");
        using var listProcess = Process.Start(list) ?? throw new OperationsIntegrityException("PG_RESTORE_START_FAILED");
        var ageError = ReadBoundedAsync(ageProcess.StandardError, cancellationToken);
        var listError = ReadBoundedAsync(listProcess.StandardError, cancellationToken);
        var listOutput = ReadBoundedAsync(listProcess.StandardOutput, cancellationToken);
        try
        {
            await ageProcess.StandardOutput.BaseStream.CopyToAsync(listProcess.StandardInput.BaseStream, cancellationToken);
            listProcess.StandardInput.Close();
            await Task.WhenAll(ageProcess.WaitForExitAsync(cancellationToken), listProcess.WaitForExitAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            Kill(ageProcess);
            Kill(listProcess);
            throw;
        }

        _ = await ageError;
        _ = await listError;
        _ = await listOutput;
        if (ageProcess.ExitCode != 0)
        {
            throw new OperationsIntegrityException("BACKUP_DECRYPTION_FAILED");
        }

        if (listProcess.ExitCode != 0)
        {
            throw new OperationsIntegrityException("BACKUP_ARCHIVE_INVALID");
        }
    }

    private static ProcessStartInfo CreatePostgresProcess(string path, NpgsqlConnectionStringBuilder connection)
    {
        var process = BaseProcess(path);
        process.Environment["PGHOST"] = connection.Host;
        process.Environment["PGPORT"] = connection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        process.Environment["PGDATABASE"] = connection.Database;
        process.Environment["PGUSER"] = connection.Username;
        process.Environment["PGPASSWORD"] = connection.Password;
        process.Environment["PGCONNECT_TIMEOUT"] = "15";
        process.Environment["PGSSLMODE"] = connection.SslMode switch
        {
            SslMode.VerifyCA => "verify-ca",
            SslMode.VerifyFull => "verify-full",
            _ => connection.SslMode.ToString().ToLowerInvariant()
        };
        return process;
    }

    private static ProcessStartInfo BaseProcess(string path) => new()
    {
        FileName = path,
        UseShellExecute = false,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };

    private static async Task<string> ReadVersionAsync(string path, CancellationToken cancellationToken)
    {
        var start = BaseProcess(path);
        start.RedirectStandardInput = false;
        start.ArgumentList.Add("--version");
        using var process = Process.Start(start) ?? throw new OperationsIntegrityException("TOOL_VERSION_FAILED");
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode == 0 ? output.Trim() : "unavailable";
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var retained = new StringBuilder(MaximumDiagnosticCharacters);
        var buffer = new char[1024];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            var remaining = MaximumDiagnosticCharacters - retained.Length;
            if (remaining > 0)
            {
                retained.Append(buffer, 0, Math.Min(read, remaining));
            }
        }

        return retained.ToString();
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the observation and the kill attempt.
        }
    }
}
