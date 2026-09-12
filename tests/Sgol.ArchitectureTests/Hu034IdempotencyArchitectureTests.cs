using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class Hu034IdempotencyArchitectureTests
{
    [Fact]
    public void HttpConsumersUseTheSingleCanonicalHeaderParser()
    {
        var root = FindRepositoryRoot();
        var endpoints = Path.Combine(root, "src", "Sgol.Web", "Interface", "Endpoints");
        var consumers = Directory.EnumerateFiles(endpoints, "*ApiEndpoints.cs")
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return source.Contains("IdempotencyKeyHeader.Parse", StringComparison.Ordinal) ||
                    source.Contains("Idempotency-Key", StringComparison.Ordinal);
            })
            .ToArray();

        Assert.Equal(17, consumers.Length);
        foreach (var path in consumers.Where(path => !Path.GetFileName(path).Equals("InboxApiEndpoints.cs", StringComparison.Ordinal)))
        {
            var source = File.ReadAllText(path);
            Assert.Contains("IdempotencyKeyHeader.Parse", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Guid.TryParse(context.Request.Headers[\"Idempotency-Key\"]", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MigrationIsAdditiveForwardOnlyAndReplacesOnlyTheGenerationKeyIndex()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Sgol.Web",
            "Infrastructure",
            "Persistence",
            "Migrations",
            "20260912120000_ExtendIdempotencyReplay.cs"));

        Assert.Contains("protocol_version", migration, StringComparison.Ordinal);
        Assert.Contains("response_payload", migration, StringComparison.Ordinal);
        Assert.Contains("response_etag", migration, StringComparison.Ordinal);
        Assert.Contains("response_location", migration, StringComparison.Ordinal);
        Assert.Contains("UX_generation_request_idempotency_key", migration, StringComparison.Ordinal);
        Assert.Contains("Npgsql:NullsDistinct", migration, StringComparison.Ordinal);
        Assert.Contains("throw new NotSupportedException", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("DropTable", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteData", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryApprovedOperationUsesTheVersionedProtocolAndNoProducerBuildsRawRecords()
    {
        var root = FindRepositoryRoot();
        var persistence = Path.Combine(root, "src", "Sgol.Web", "Infrastructure", "Persistence");
        var sources = Directory.EnumerateFiles(persistence, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToArray();
        var combined = string.Join('\n', sources);
        var operations = new[]
        {
            "PERSON_CREATE", "PERSON_EMPLOYMENT_PATCH", "PERSON_DEACTIVATE", "PERSON_REACTIVATE",
            "AVAILABILITY_PUT", "ACCOUNT_CREATE", "ACCOUNT_DEACTIVATE", "ACCOUNT_REACTIVATE",
            "ROLE_ASSIGNMENT_CHANGE", "CONFIGURATION_RELEASE_CREATE", "CONFIGURATION_RELEASE_PUBLISH",
            "TASK_DEFINITION_VERSION_CREATE", "TASK_DEFINITION_VERSION_PUBLISH", "TASK_DEFINITION_DEACTIVATE_NEW",
            "ACTIVATION_POLICY_PUT", "ELIGIBILITY_POLICY_PUT", "EVIDENCE_POLICY_PUT", "VALIDATION_POLICY_PUT",
            "GENERATION_REQUEST_CREATE", "ASSIGNMENT_CORRECTION_CREATE", "WORK_PLAN_ENSURE",
            "PLAN_PUBLICATION_CREATE", "FILE_UPLOAD_INTENT_CREATE", "FILE_UPLOAD_COMPLETE",
            "EVIDENCE_CONTRIBUTE", "EVIDENCE_REPLACE", "OBLIGATION_CONCLUDE",
            "VALIDATION_DECISION_CREATE", "VALIDATION_DECISION_REPLACE",
            "RECURRING_OCCURRENCE_PROCESS", "ELIGIBILITY_EVALUATION_CREATE", "AUTOMATIC_ASSIGNMENT_CREATE",
        };

        foreach (var operation in operations)
        {
            Assert.Contains(operation, combined, StringComparison.Ordinal);
        }

        foreach (var source in sources.Where(source =>
                     !source.Contains("internal static class IdempotencyProtocol", StringComparison.Ordinal) &&
                     !source.Contains("ApplyConfiguration(new IdempotencyRecordConfiguration", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain("new IdempotencyRecord", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Status = \"COMPLETED\"", source, StringComparison.Ordinal);
        }

        Assert.Contains("protocolVersion = CurrentVersion", combined, StringComparison.Ordinal);
        var exceptionHandler = File.ReadAllText(Path.Combine(
            root, "src", "Sgol.Web", "Infrastructure", "Http", "SafeExceptionHandler.cs"));
        Assert.Contains("IDEMPOTENCY_CONFLICT_AUDIT_FAILED", exceptionHandler, StringComparison.Ordinal);
        Assert.Contains("IDEMPOTENCY_REPLAY_NO_DISPONIBLE", exceptionHandler, StringComparison.Ordinal);
        Assert.Contains("idempotency_requests_total", combined, StringComparison.Ordinal);
        Assert.Contains("idempotency_retries_total", combined, StringComparison.Ordinal);
        Assert.Contains("idempotency_replay_unavailable_total", combined, StringComparison.Ordinal);
        Assert.Contains("idempotency_request_duration_seconds", combined, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SGOL.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
