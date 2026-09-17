using System.Globalization;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Continuity.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.JobInfrastructure;
using Sgol.Notifications.Contracts;
using Sgol.Operations;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Validation.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Continuity;
using Npgsql;
using Xunit;

namespace Sgol.OperationsIntegrationTests;

public sealed class FunctionalRecoveryAmd64GateTests
{
    private const string PostgreSqlImage = "postgres:18.6-alpine3.23";
    private const string SyntheticReplacementAuthority = ValidationAuthorityTypes.OriginalReplacement;
    private const string SyntheticJobCheckpoint = "{\"schemaVersion\":1,\"position\":\"complete\"}";
    private static readonly string[] FixtureActorCodes = ["DENIED", "DIR", "RESP"];
    private static readonly JsonSerializerOptions NetworkAbJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly Amd64Case[] ApprovedCases =
    [
        new("positive", RecoveryReconciliationStatuses.Approved, null),
        new("identity_missing", RecoveryReconciliationStatuses.Different, RecoveryDifferenceKinds.IdentityMissing),
        new("identity_additional", RecoveryReconciliationStatuses.Different, RecoveryDifferenceKinds.IdentityAdditional),
        new("link_missing", RecoveryReconciliationStatuses.Different, RecoveryDifferenceKinds.LinkMissing),
        new("link_altered", RecoveryReconciliationStatuses.Different, RecoveryDifferenceKinds.LinkChanged),
        new("version_changed", RecoveryReconciliationStatuses.Different, RecoveryDifferenceKinds.VersionChanged),
        new("count_changed", RecoveryReconciliationStatuses.Different, RecoveryDifferenceKinds.CountChanged),
        new("evidence_missing", RecoveryReconciliationStatuses.Different, RecoveryDifferenceKinds.EvidenceMissing),
        new("evidence_corrupt", RecoveryReconciliationStatuses.Different, RecoveryDifferenceKinds.EvidenceCorrupt),
        new("evidence_inaccessible", RecoveryReconciliationStatuses.Different, RecoveryDifferenceKinds.EvidenceInaccessible),
        new("audit_missing", RecoveryReconciliationStatuses.Different, RecoveryDifferenceKinds.AuditMissing),
        new("audit_altered", RecoveryReconciliationStatuses.Different, RecoveryDifferenceKinds.AuditAltered),
        new("reference_corrupt", RecoveryReconciliationStatuses.Failed, "REFERENCE_CORRUPT"),
        new("rpo_exceeded", RecoveryReconciliationStatuses.Failed, "RPO_EXCEEDED"),
        new("rto_exceeded", RecoveryReconciliationStatuses.Failed, "RTO_EXCEEDED"),
        new("primary_target", RecoveryReconciliationStatuses.Failed, "RESTORE_PRIMARY_TARGET_REJECTED"),
        new("replay_conflict", RecoveryReconciliationStatuses.Matched, "RECONCILIATION_IMMUTABLE_CONFLICT"),
        new("concurrency", RecoveryReconciliationStatuses.Matched, null)
    ];

    [Fact]
    [Trait("Category", "Hu035ReplicaDiagnostics")]
    public void ReplicaDiagnosticOperationsAreClosedAndNormalized()
    {
        string[] operations =
        [
            "LIST_SOURCE_OBJECTS",
            "HEAD_SOURCE_METADATA",
            "READ_SOURCE_HASH",
            "GET_SOURCE_STREAM",
            "CHECK_DESTINATION_METADATA",
            "WRITE_AND_VERIFY_DESTINATION",
            "PUT_DESTINATION",
            "GET_DESTINATION_VERIFY",
            "HEAD_DESTINATION_METADATA",
            "READ_DESTINATION_HASH"
        ];

        foreach (var operation in operations)
            Assert.Equal(operation, InvokeObjectReplica<string>("NormalizeOperation", operation));

        Assert.Equal("UNKNOWN", InvokeObjectReplica<string>("NormalizeOperation", "READ_OTHER_HASH"));
        Assert.Equal("UNKNOWN", InvokeObjectReplica<string>("NormalizeOperation", "LIST_SOURCE_OBJECTS\n"));
        Assert.Equal("REPLICATE_CLEAN", InvokeObjectReplica<string>("NormalizeStage", "REPLICATE_CLEAN"));
        Assert.Equal("UNKNOWN", InvokeObjectReplica<string>("NormalizeStage", "REPLICATE_CLEAN\n"));
    }

    [Fact]
    [Trait("Category", "Hu035ReplicaDiagnostics")]
    public void ReplicaDiagnosticPayloadIsSanitizedAndPreservesContractCode()
    {
        Assert.Equal("AmazonS3Exception",
            InvokeObjectReplica<string>("SanitizeDiagnosticToken", "AmazonS3Exception"));
        Assert.Equal("InternalError", InvokeObjectReplica<string>("SanitizeDiagnosticToken", "InternalError"));
        Assert.Equal("UNKNOWN", InvokeObjectReplica<string>("SanitizeDiagnosticToken", (object?)null));
        Assert.Equal("UNKNOWN", InvokeObjectReplica<string>("SanitizeDiagnosticToken", "Internal\nError"));
        Assert.Equal("UNKNOWN", InvokeObjectReplica<string>("SanitizeDiagnosticToken", "AccessKey=synthetic"));
        Assert.Equal("UNKNOWN", InvokeObjectReplica<string>("SanitizeDiagnosticToken", "http://storage.invalid/private"));
        Assert.Equal("UNKNOWN", InvokeObjectReplica<string>("SanitizeDiagnosticToken", new string('a', 65)));
        Assert.Equal("500", InvokeObjectReplica<string>("NormalizeHttpStatusCode", 500));
        Assert.Equal("NONE", InvokeObjectReplica<string>("NormalizeHttpStatusCode", 0));
        Assert.Equal("NONE", InvokeObjectReplica<string>("NormalizeHttpStatusCode", 600));

        var storageFailure = new AmazonS3Exception("endpoint=https://storage.invalid AccessKey=synthetic")
        {
            StatusCode = HttpStatusCode.InternalServerError,
            ErrorCode = "InternalError"
        };
        Assert.Equal("REPLICA_INFRASTRUCTURE_FAILED",
            InvokeObjectReplica<string>("ClassifyFailure", storageFailure));

        var failure = InvokeObjectReplica<JobExecutionException>(
            "CreateFailure",
            "REPLICA_INFRASTRUCTURE_FAILED",
            "REPLICATE_CLEAN",
            "READ_DESTINATION_HASH",
            storageFailure.GetType().Name,
            "500",
            storageFailure.ErrorCode);
        Assert.Equal("REPLICA_INFRASTRUCTURE_FAILED", failure.ErrorCode);
        Assert.Equal(5, failure.Data.Count);
        Assert.Equal("REPLICATE_CLEAN", failure.Data["SGOL_REPLICA_STAGE"]);
        Assert.Equal("READ_DESTINATION_HASH", failure.Data["SGOL_REPLICA_OPERATION"]);
        Assert.Equal("AmazonS3Exception", failure.Data["SGOL_REPLICA_EXCEPTION_TYPE"]);
        Assert.Equal("500", failure.Data["SGOL_REPLICA_HTTP_STATUS"]);
        Assert.Equal("InternalError", failure.Data["SGOL_REPLICA_S3_ERROR_CODE"]);
        Assert.Null(failure.InnerException);

        var unsafeFailure = InvokeObjectReplica<JobExecutionException>(
            "CreateFailure",
            "REPLICA_INFRASTRUCTURE_FAILED",
            "REPLICATE_CLEAN\nprivate",
            "AccessKey=synthetic",
            "endpoint=https://storage.invalid",
            "0",
            "SecretKey=synthetic");
        Assert.Equal("UNKNOWN", unsafeFailure.Data["SGOL_REPLICA_STAGE"]);
        Assert.Equal("UNKNOWN", unsafeFailure.Data["SGOL_REPLICA_OPERATION"]);
        Assert.Equal("UNKNOWN", unsafeFailure.Data["SGOL_REPLICA_EXCEPTION_TYPE"]);
        Assert.Equal("NONE", unsafeFailure.Data["SGOL_REPLICA_HTTP_STATUS"]);
        Assert.Equal("UNKNOWN", unsafeFailure.Data["SGOL_REPLICA_S3_ERROR_CODE"]);
        var serializedData = string.Join('|', unsafeFailure.Data.Values.Cast<object>());
        Assert.DoesNotContain("storage.invalid", serializedData, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic", serializedData, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Hu035ReplicaDiagnostics")]
    public void ReplicaPreparationFailureExposesOnlySanitizedDiagnosticsAndKeepsRetryBoundary()
    {
        var failure = InvokeObjectReplica<JobExecutionException>(
            "CreateFailure",
            "REPLICA_INFRASTRUCTURE_FAILED",
            "REPLICATE_CLEAN",
            "WRITE_AND_VERIFY_DESTINATION",
            "AmazonS3Exception",
            "500",
            "InternalError");

        var exposed = ReplicaPreparationFailure(failure);
        Assert.Equal(
            "HU035_REPLICA_PREPARATION_FAILED:REPLICA_INFRASTRUCTURE_FAILED:" +
            "STAGE=REPLICATE_CLEAN:OPERATION=WRITE_AND_VERIFY_DESTINATION:" +
            "TYPE=AmazonS3Exception:HTTP=500:S3CODE=InternalError",
            exposed.Message);
        Assert.Null(exposed.InnerException);
        Assert.True(ShouldRetryReplicaFailure(failure, 1, 3));
        Assert.False(ShouldRetryReplicaFailure(failure, 3, 3));
        Assert.False(ShouldRetryReplicaFailure(new JobExecutionException("DESTINATION_OBJECT_CORRUPT"), 1, 3));

        var unsafeFailure = new JobExecutionException("REPLICA_INFRASTRUCTURE_FAILED");
        unsafeFailure.Data["SGOL_REPLICA_STAGE"] = "REPLICATE_CLEAN\nprivate";
        unsafeFailure.Data["SGOL_REPLICA_OPERATION"] = "AccessKey=synthetic";
        unsafeFailure.Data["SGOL_REPLICA_EXCEPTION_TYPE"] = "endpoint=https://storage.invalid";
        unsafeFailure.Data["SGOL_REPLICA_HTTP_STATUS"] = "0";
        unsafeFailure.Data["SGOL_REPLICA_S3_ERROR_CODE"] = "SecretKey=synthetic";
        var sanitizedExposure = ReplicaPreparationFailure(unsafeFailure);
        Assert.Equal(
            "HU035_REPLICA_PREPARATION_FAILED:REPLICA_INFRASTRUCTURE_FAILED:" +
            "STAGE=UNKNOWN:OPERATION=UNKNOWN:TYPE=UNKNOWN:HTTP=NONE:S3CODE=UNKNOWN",
            sanitizedExposure.Message);
        Assert.DoesNotContain("storage.invalid", sanitizedExposure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic", sanitizedExposure.Message, StringComparison.Ordinal);
        Assert.Null(sanitizedExposure.InnerException);
    }

    [Fact]
    [Trait("Category", "Hu035Contract")]
    public void SyntheticEvidenceMatchesPersistenceContract()
    {
        SyntheticEvidenceFixture.AssertContract();
        Assert.Equal("application/pdf", SyntheticEvidenceFixture.ContentType);
        Assert.EndsWith(".pdf", SyntheticEvidenceFixture.OriginalName, StringComparison.Ordinal);

        var plan = new WorkPlan(Guid.CreateVersion7(), BranchScope.LorettaId, Guid.CreateVersion7());
        plan.ApplyPublication();
        var firstPublicationRowVersion = plan.RowVersion;
        plan.ApplyPublication();
        Assert.Equal(firstPublicationRowVersion + 1, plan.RowVersion);
        Assert.Equal(ValidationAuthorityTypes.OriginalReplacement, SyntheticReplacementAuthority);

        using var outbox = JsonDocument.Parse(SyntheticOutboxPayload(
            new DeterministicSeed(new string('a', 64))));
        Assert.Equal(3, outbox.RootElement.EnumerateObject().Count());
        Assert.Equal(1, outbox.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(JsonValueKind.String, outbox.RootElement.GetProperty("correlationId").ValueKind);
        Assert.Equal(JsonValueKind.Object, outbox.RootElement.GetProperty("data").ValueKind);
        using var checkpoint = JsonDocument.Parse(SyntheticJobCheckpoint);
        Assert.Equal(JsonValueKind.Object, checkpoint.RootElement.ValueKind);
    }

    [Fact]
    [Trait("Category", "Hu035PostgreSqlFixture")]
    public async Task SyntheticFixturePersistsUnderPostgreSqlConstraints()
    {
        const string database = "sgol_hu035_fixture";
        var password = $"hu035-{Guid.NewGuid():N}";
        await using var postgres = new ContainerBuilder(PostgreSqlImage)
            .WithEnvironment("POSTGRES_DB", database)
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithEnvironment("POSTGRES_PASSWORD", password)
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5432))
            .Build();
        await postgres.StartAsync();

        var connectionString = $"Host=127.0.0.1;Port={postgres.GetMappedPublicPort(5432)};" +
            $"Database={database};Username=postgres;Password={password};SSL Mode=Disable;Timeout=15";
        await using var context = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(connectionString).Options);
        await context.Database.MigrateAsync();
        await SeedFunctionalFixtureAsync(context, new DeterministicSeed(new string('a', 64)));

        Assert.Equal(2, await context.PlanVersions.CountAsync());
        Assert.Equal(2, await context.ValidationDecisionVersions.CountAsync());
        Assert.Single(await context.FileObjects.ToArrayAsync());
    }

    [Fact]
    [Trait("Category", "Hu035Amd64")]
    public async Task ExecuteApprovedSyntheticPhase()
    {
        Assert.Equal("true", Required("SGOL_SYNTHETIC_ONLY"));
        var phase = Required("SGOL_HU035_PHASE");
        if (phase == "verify")
        {
            await VerifyAndApproveAsync();
            return;
        }
        if (phase == "mutate")
        {
            await ApplySyntheticMutationAsync(Required("SGOL_HU035_CASE"));
            return;
        }
        Assert.Equal("prepare", phase);
        var outputPath = Required("SGOL_HU035_DESCRIPTOR_PATH");
        Assert.False(File.Exists(outputPath));
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var connectionString = Required("ConnectionStrings__Sgol");
        var now = DateTimeOffset.UtcNow;
        var clock = new AdvancingClock(now);
        var seed = new DeterministicSeed(Required("SGOL_REVISION"));
        var uuid = new DeterministicUuidGenerator(seed, "prepare");

        await using var context = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(connectionString).Options);
        await context.Database.MigrateAsync();
        var actors = await SeedFunctionalFixtureAsync(context, seed);
        var direction = actors.DirectionUserId;
        context.AuditEvents.Add(new AuditEvent
        {
            Id = uuid.NewUuid(),
            OccurredAt = now.AddMinutes(-10),
            ActorType = "USER",
            ActorUserId = direction,
            Action = "HU035_SYNTHETIC_BASELINE_CREATED",
            ResourceType = "SYNTHETIC_RECOVERY_FIXTURE",
            ResourceId = uuid.NewUuid(),
            BranchId = BranchScope.LorettaId,
            CorrelationId = uuid.NewUuid(),
            Outcome = "SUCCESS"
        });
        await context.SaveChangesAsync();

        var replicaSlot = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 5, 0, TimeSpan.Zero);
        if (replicaSlot > now) replicaSlot = replicaSlot.AddHours(-1);
        var staleReplicaSlot = replicaSlot.AddHours(-2);
        var replica = new ObjectReplica(configuration, TimeProvider.System, NullLogger<ObjectReplica>.Instance);
        await ExecuteReplicaWithBoundedRetryAsync(replica, staleReplicaSlot);
        await ExecuteReplicaWithBoundedRetryAsync(replica, replicaSlot);
        var replicaOptions = ReplicaOptions.FromConfiguration(configuration);
        var replicaUri = $"s3://{replicaOptions.ManifestBucket}/{replicaOptions.ManifestPrefix}/" +
            $"{replicaSlot:yyyy/MM/dd}/objects-{replicaSlot:yyyyMMdd'T'HHmmss'Z'}.manifest.json";
        var staleReplicaUri = $"s3://{replicaOptions.ManifestBucket}/{replicaOptions.ManifestPrefix}/" +
            $"{staleReplicaSlot:yyyy/MM/dd}/objects-{staleReplicaSlot:yyyyMMdd'T'HHmmss'Z'}.manifest.json";
        var backupOptions = BackupOptions.FromConfiguration(configuration);
        var descriptors = new List<object>();
        var service = new EfRecoveryReconciliationService(context, new AuditTransaction(context),
            new TestOutboxWriter(context, uuid, now), clock, uuid);
        await Assert.ThrowsAsync<RecoveryAccessDeniedException>(() => service.CreateAsync(new(
            actors.DeniedUserId, uuid.NewUuid(), uuid.NewUuid(), "Solicitud denegada sintética")));
        await Assert.ThrowsAsync<RecoveryAccessDeniedException>(() => service.GetAsync(new(
            actors.DeniedUserId, uuid.NewUuid(), seed.Id("anti-idor-absent"))));
        foreach (var testCase in ApprovedCases)
        {
            var selectedReplica = testCase.Name == "rpo_exceeded" ? staleReplicaUri : replicaUri;
            Environment.SetEnvironmentVariable("Continuity__ReplicaManifestUri", selectedReplica,
                EnvironmentVariableTarget.Process);
            configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var created = await service.CreateAsync(new(direction, uuid.NewUuid(), uuid.NewUuid(),
                $"Simulacro sintético CI HU-035: {testCase.Name}"));
            if (testCase.Name == "positive")
                await Assert.ThrowsAsync<RecoveryAccessDeniedException>(() => service.GetAsync(new(
                    actors.DeniedUserId, uuid.NewUuid(), created.ReconciliationId)));
            var operations = new FunctionalRecoveryOperations(configuration, new BackupProcessPipeline(),
                TimeProvider.System);
            var job = new CaptureRecoveryReferenceJob(operations, uuid, TimeProvider.System);
            var services = new ServiceCollection();
            services.AddScoped(_ => new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>()
                .UseNpgsql(connectionString).Options));
            services.AddSingleton(new ScheduledJobRegistry([job]));
            services.AddSingleton<IClock>(clock);
            services.AddSingleton<IUuidGenerator>(uuid);
            services.AddSingleton(new JobDatabaseOptions(connectionString));
            services.AddScoped(provider => new ScheduledJobRunner(provider.GetRequiredService<SgolDbContext>(),
                provider.GetRequiredService<ScheduledJobRegistry>(), clock, uuid,
                provider.GetRequiredService<JobDatabaseOptions>(), NullLogger<ScheduledJobRunner>.Instance));
            await using var provider = services.BuildServiceProvider();
            var handler = new RecoveryReferenceRequestedOutboxHandler(
                provider.GetRequiredService<IServiceScopeFactory>(), uuid, clock);
            var processor = new OutboxProcessor(context, new OutboxHandlerRegistry([handler]), clock,
                NullLogger<OutboxProcessor>.Instance);
            Assert.Equal(OutboxProcessResult.Processed, await processor.ProcessNextAsync());
            var latest = await context.RecoveryReconciliationEvents.AsNoTracking()
                .Where(item => item.ReconciliationId == created.ReconciliationId)
                .OrderByDescending(item => item.Sequence).FirstAsync();
            Assert.Equal(RecoveryReconciliationStatuses.ReferenceCapturing, latest.Status);
            var prefix = $"{backupOptions.Prefix}/continuity/v1/{created.ReconciliationId:D}";
            descriptors.Add(new
            {
                name = testCase.Name,
                reconciliationId = created.ReconciliationId,
                expectedStatus = testCase.ExpectedStatus,
                expectedCode = testCase.ExpectedCode,
                referenceManifestUri = $"s3://{backupOptions.Bucket}/{prefix}/reference.manifest.json",
                backupManifestUri = $"s3://{backupOptions.Bucket}/{prefix}/postgresql.dump.age.manifest.json",
                replicaManifestUri = selectedReplica
            });
        }

        var descriptor = new
        {
            schemaVersion = 1,
            kind = "SGOL_HU035_AMD64_DESCRIPTOR",
            directionUserId = direction,
            expectedMigration = "20260914210503_AddRecoveryReconciliation",
            cases = descriptors
        };
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(descriptor),
            new System.Text.UTF8Encoding(false));
    }

    private static async Task VerifyAndApproveAsync()
    {
        var descriptorPath = Required("SGOL_HU035_DESCRIPTOR_PATH");
        using var descriptor = JsonDocument.Parse(await File.ReadAllBytesAsync(descriptorPath));
        var directionUserId = descriptor.RootElement.GetProperty("directionUserId").GetGuid();
        var now = DateTimeOffset.UtcNow;
        var clock = new FixedClock(now);
        var uuid = new DeterministicUuidGenerator(new DeterministicSeed(Required("SGOL_REVISION")), "verify");
        await using var context = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(Required("ConnectionStrings__Sgol")).Options);
        var service = new EfRecoveryReconciliationService(context, new AuditTransaction(context),
            new TestOutboxWriter(context, uuid, now), clock, uuid);
        var results = new List<object>();
        object? manifestHashes = null;
        object? observedObjectives = null;
        foreach (var item in descriptor.RootElement.GetProperty("cases").EnumerateArray())
        {
            var name = item.GetProperty("name").GetString()!;
            var reconciliationId = item.GetProperty("reconciliationId").GetGuid();
            var expectedStatus = item.GetProperty("expectedStatus").GetString()!;
            var expectedCode = item.GetProperty("expectedCode").ValueKind == JsonValueKind.Null
                ? null : item.GetProperty("expectedCode").GetString();
            var details = await service.GetAsync(new(directionUserId, uuid.NewUuid(), reconciliationId));
            Assert.Equal(expectedStatus == RecoveryReconciliationStatuses.Approved
                ? RecoveryReconciliationStatuses.Matched : expectedStatus, details.Status);
            if (expectedCode is not null && details.Status == RecoveryReconciliationStatuses.Different)
                Assert.Contains(details.Differences, difference => difference.Kind == expectedCode);
            if (details.Status == RecoveryReconciliationStatuses.Failed)
            {
                var errorCode = await context.RecoveryReconciliationEvents.AsNoTracking()
                    .Where(entry => entry.ReconciliationId == reconciliationId)
                    .OrderByDescending(entry => entry.Sequence).Select(entry => entry.ErrorCode).FirstAsync();
                Assert.Equal(expectedCode, errorCode);
            }
            if (name == "positive")
            {
                Assert.Equal(0, details.DifferenceCount);
                Assert.False(details.DifferencesTruncated);
                Assert.InRange(details.ObservedRpoSeconds!.Value, 0, 3600);
                Assert.InRange(details.ObservedRtoSeconds!.Value, 0, 14400);
                var approved = await service.ApproveAsync(new(directionUserId, uuid.NewUuid(), uuid.NewUuid(),
                    reconciliationId, details.Sequence, "Aprobación sintética CI HU-035"));
                Assert.Equal(RecoveryReconciliationStatuses.Approved, approved.Status);
                var reportHash = await context.RecoveryReconciliationEvents.AsNoTracking()
                    .Where(entry => entry.ReconciliationId == reconciliationId &&
                        entry.EventType == RecoveryReconciliationEvents.Completed)
                    .Select(entry => entry.ReportManifestSha256).SingleAsync();
                var referenceUri = item.GetProperty("referenceManifestUri").GetString()!;
                var reportUri = referenceUri.Replace("reference.manifest.json", "report.manifest.json",
                    StringComparison.Ordinal);
                var reportBytes = await ReadS3Async(reportUri);
                var report = OperationManifestSerializer.Deserialize<FunctionalRecoveryReportManifest>(reportBytes);
                Assert.Equal(0, report.DatabaseRpoSeconds);
                Assert.InRange(report.ObservedRpoSeconds, 0, 3600);
                Assert.InRange(report.ObservedRtoSeconds, 0, 14400);
                Assert.Equal(reportHash, Convert.ToHexStringLower(SHA256.HashData(reportBytes)));
                observedObjectives = new
                {
                    databaseRpoSeconds = report.DatabaseRpoSeconds,
                    objectRpoSeconds = report.ObjectRpoSeconds,
                    observedRpoSeconds = report.ObservedRpoSeconds,
                    observedRtoSeconds = report.ObservedRtoSeconds
                };
                manifestHashes = new
                {
                    reference = await HashS3Async(referenceUri),
                    backup = await HashS3Async(item.GetProperty("backupManifestUri").GetString()!),
                    replica = await HashS3Async(item.GetProperty("replicaManifestUri").GetString()!),
                    report = reportHash
                };
            }
            else if (details.Status is RecoveryReconciliationStatuses.Different or RecoveryReconciliationStatuses.Failed)
            {
                await Assert.ThrowsAsync<RecoveryReconciliationNotApprovableException>(() => service.ApproveAsync(
                    new(directionUserId, uuid.NewUuid(), uuid.NewUuid(), reconciliationId, details.Sequence,
                        "No debe aprobarse")));
            }
            results.Add(new
            {
                name,
                status = name == "positive" ? RecoveryReconciliationStatuses.Approved : details.Status,
                code = expectedCode,
                differenceCount = details.DifferenceCount,
                rpoSeconds = details.ObservedRpoSeconds,
                rtoSeconds = details.ObservedRtoSeconds
            });
        }
        Assert.Equal(18, results.Count);
        Assert.NotNull(manifestHashes);
        Assert.NotNull(observedObjectives);
        Assert.Equal(FixtureActorCodes, await context.People.AsNoTracking()
            .Where(person => person.StableCode == "DENIED" || person.StableCode == "DIR" ||
                person.StableCode == "RESP" || person.StableCode == "HU035-ADDITIONAL")
            .OrderBy(person => person.StableCode).Select(person => person.StableCode).ToArrayAsync());
        Assert.Equal(SyntheticEvidenceFixture.Sha256, await context.FileObjects.AsNoTracking()
            .Where(file => file.ObjectKey == SyntheticEvidenceFixture.ObjectKey)
            .Select(file => file.Sha256).SingleAsync());
        var publicResult = new
        {
            schemaVersion = 1,
            kind = "SGOL_HU035_AMD64_RESULT",
            status = RecoveryReconciliationStatuses.Approved,
            tableCount = FunctionalSnapshotSchema.Tables.Length,
            expectedMigration = "20260914210503_AddRecoveryReconciliation",
            manifestHashes,
            observedObjectives,
            caseCount = results.Count,
            cases = results,
            isolatedRestore = true,
            sourceDataUnchanged = true,
            approved = true,
            syntheticOnly = true
        };
        await File.WriteAllTextAsync(Required("SGOL_HU035_PUBLIC_RESULT_PATH"),
            JsonSerializer.Serialize(publicResult), new System.Text.UTF8Encoding(false));
    }

    private static async Task ApplySyntheticMutationAsync(string testCase)
    {
        if (testCase is "evidence_missing" or "evidence_corrupt" or "restore_object")
        {
            await MutateReplicaObjectAsync(testCase);
            return;
        }
        if (testCase == "reference_corrupt")
        {
            var (bucket, key) = ParseS3(Required("SGOL_HU035_REFERENCE_MANIFEST_URI"));
            using var client = S3Client("Backup__Storage", "Backup__Storage__AccessKey",
                "Backup__Storage__SecretKey");
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                ContentBody = "{\"corrupt\":true}",
                ContentType = "application/json"
            });
            return;
        }
        if (testCase is "rto_exceeded" or "replay_conflict")
        {
            var path = Required("SGOL_HU035_RESTORE_EVIDENCE_PATH");
            var node = JsonNode.Parse(await File.ReadAllBytesAsync(path))?.AsObject()
                ?? throw new InvalidOperationException("Synthetic restore evidence is invalid.");
            if (testCase == "rto_exceeded")
            {
                var completed = node["completedAt"]?.GetValue<DateTimeOffset>()
                    ?? throw new InvalidOperationException("Synthetic restore evidence has no completedAt.");
                node["startedAt"] = completed.AddHours(-5);
            }
            else
            {
                node["durationMilliseconds"] = (node["durationMilliseconds"]?.GetValue<long>() ?? 0) + 1;
            }
            await File.WriteAllTextAsync(path, node.ToJsonString(), new UTF8Encoding(false));
            return;
        }

        var sql = testCase switch
        {
            "identity_missing" => "DELETE FROM person WHERE stable_code = 'RESP'",
            "identity_additional" => "INSERT INTO person(id,stable_code,display_name,created_at) VALUES " +
                "('70000000-0000-7000-8000-000000000001','HU035-ADDITIONAL','Additional synthetic identity'," +
                "'2026-09-14T18:00:00Z')",
            "link_missing" => "DELETE FROM assignment_version WHERE status = 'VIGENTE'",
            "link_altered" => "UPDATE assignment_version SET person_id = " +
                "(SELECT id FROM person WHERE stable_code = 'DENIED') WHERE status = 'VIGENTE'",
            "version_changed" => "UPDATE employment_version SET row_version = row_version + 1 " +
                "WHERE id = (SELECT id FROM employment_version ORDER BY id LIMIT 1)",
            "count_changed" => "DELETE FROM scheduled_job_run WHERE job_name = 'HU035_SYNTHETIC_JOB'",
            "audit_missing" => "DELETE FROM audit_event WHERE action = 'HU035_SYNTHETIC_BASELINE_CREATED'",
            "audit_altered" => "UPDATE audit_event SET outcome = 'FAILURE' " +
                "WHERE action = 'HU035_SYNTHETIC_BASELINE_CREATED'",
            "positive" or "rpo_exceeded" or "evidence_inaccessible" or "primary_target" or
                "replay_conflict" or "concurrency" => null,
            _ => throw new InvalidOperationException($"Unknown HU-035 synthetic case: {testCase}")
        };
        if (sql is null) return;
        await using var connection = new NpgsqlConnection(Required("Restore__PostgreSql__ConnectionString"));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"SET session_replication_role = replica; {sql}; " +
            "SET session_replication_role = origin", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteReplicaWithBoundedRetryAsync(
        IObjectReplica replica,
        DateTimeOffset scheduledFor,
        List<NetworkReplicaAttempt>? attempts = null)
    {
        const int maximumAttempts = 3;
        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            var startedAt = DateTimeOffset.UtcNow;
            try
            {
                await replica.ExecuteAsync(scheduledFor, CancellationToken.None);
                attempts?.Add(new(attempt, startedAt, DateTimeOffset.UtcNow, "SUCCEEDED", null));
                return;
            }
            catch (JobExecutionException exception)
                when (ShouldRetryReplicaFailure(exception, attempt, maximumAttempts))
            {
                attempts?.Add(new(attempt, startedAt, DateTimeOffset.UtcNow, "FAILED", ReplicaPreparationFailure(exception).Message));
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt));
            }
            catch (JobExecutionException exception)
            {
                attempts?.Add(new(attempt, startedAt, DateTimeOffset.UtcNow, "FAILED", ReplicaPreparationFailure(exception).Message));
                throw ReplicaPreparationFailure(exception);
            }
            catch (Exception exception)
            {
                attempts?.Add(new(attempt, startedAt, DateTimeOffset.UtcNow,
                    exception is OperationCanceledException ? "CANCELLED" : "FAILED", "UNEXPECTED_REPLICA_FAILURE"));
                throw;
            }
        }
    }

    // Separate opt-in entry point: no PostgreSQL, Worker or recovery matrix is initialized.
    [Fact]
    [Trait("Category", "Hu035ReplicaNetworkAb")]
    public async Task FirstReplicaOnPreparedNetworkHasExactCompleteManifest()
    {
        if (Required("SGOL_HU035_NETWORK_AB") != "true" ||
            !OperatingSystem.IsLinux() || System.Runtime.InteropServices.RuntimeInformation.OSArchitecture !=
            System.Runtime.InteropServices.Architecture.X64)
            throw new InvalidOperationException("NETWORK_AB_NATIVE_AMD64_REQUIRED");

        var variant = Required("SGOL_HU035_NETWORK_VARIANT");
        if (variant is not ("A" or "B")) throw new InvalidOperationException("NETWORK_AB_VARIANT_INVALID");
        var resultPath = Required("SGOL_HU035_NETWORK_RESULT");
        var slot = DateTimeOffset.ParseExact(Required("SGOL_HU035_NETWORK_SLOT"),
            "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var build = OperationBuildIdentity.FromConfiguration(configuration);
        var attempts = new List<NetworkReplicaAttempt>();
        var outcome = "REPLICA_FAILED";
        var manifestVerified = false;
        try
        {
            var replica = new ObjectReplica(configuration, TimeProvider.System, NullLogger<ObjectReplica>.Instance);
            await ExecuteReplicaWithBoundedRetryAsync(replica, slot, attempts);
            outcome = "MANIFEST_READ_FAILED";
            var options = ReplicaOptions.FromConfiguration(configuration);
            using var destination = options.Destination.CreateClient();
            var key = $"{options.ManifestPrefix}/{slot:yyyy/MM/dd}/objects-{slot:yyyyMMdd'T'HHmmss'Z'}.manifest.json";
            var stored = await new S3OperationStore(destination).ReadAsync(options.ManifestBucket, key, CancellationToken.None);
            outcome = "MANIFEST_INVALID";
            var manifest = OperationManifestSerializer.Deserialize<ObjectReplicaManifest>(stored.Content);
            ValidateNetworkManifest(manifest, slot, build);
            if (!stored.Content.AsSpan().SequenceEqual(OperationManifestSerializer.Serialize(manifest)))
                throw new InvalidOperationException("NETWORK_AB_MANIFEST_INVALID");
            manifestVerified = true;
            outcome = attempts.Count == 1 ? "PASSED_INITIAL" : "PASSED_AFTER_RETRY";
        }
        catch (OperationCanceledException) { outcome = "CANCELLED"; }
        catch (Exception) { /* Original exception and private data never enter public evidence. */ }
        finally
        {
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new
            {
                schemaVersion = 1, variant, revision = build.Revision, imageDigest = build.ImageDigest,
                scheduledFor = slot, outcome, manifestVerified, verifiedObjectCount = manifestVerified ? 1 : 0,
                attempts
            }, NetworkAbJsonOptions));
        }
        if (!manifestVerified) throw new InvalidOperationException($"NETWORK_AB_{outcome}");
    }

    private static void ValidateNetworkManifest(ObjectReplicaManifest manifest, DateTimeOffset slot, OperationBuildIdentity build)
    {
        if (manifest is not { SchemaVersion: 1, Kind: "SGOL_EVIDENCE_OBJECT_REPLICA", Status: "COMPLETE", ErrorClass: null } ||
            manifest.ScheduledFor != slot || manifest.Revision != build.Revision || manifest.ImageDigest != build.ImageDigest ||
            manifest.Objects is not { Count: 1 })
            throw new InvalidOperationException("NETWORK_AB_MANIFEST_INVALID");
        var entry = manifest.Objects[0];
        if (entry.BucketRole != "clean" || entry.Key != SyntheticEvidenceFixture.ObjectKey ||
            entry.Size != SyntheticEvidenceFixture.Content.LongLength || entry.SourceSha256 != SyntheticEvidenceFixture.Sha256 ||
            entry.DestinationSha256 != SyntheticEvidenceFixture.Sha256 || entry.Status != "VERIFIED")
            throw new InvalidOperationException("NETWORK_AB_MANIFEST_INVALID");
    }

    [Theory]
    [Trait("Category", "Hu035ReplicaNetworkAbPure")]
    [InlineData("valid")]
    [InlineData("slot")]
    [InlineData("schema")]
    [InlineData("kind")]
    [InlineData("status")]
    [InlineData("error")]
    [InlineData("revision")]
    [InlineData("image")]
    [InlineData("empty")]
    [InlineData("multiple")]
    [InlineData("role")]
    [InlineData("key")]
    [InlineData("size")]
    [InlineData("sourceHash")]
    [InlineData("destinationHash")]
    [InlineData("verified")]
    public void NetworkManifestRequiresExactSlotAndVerifiedSyntheticObject(string mutation)
    {
        var slot = new DateTimeOffset(2026, 9, 17, 12, 5, 0, TimeSpan.Zero);
        var build = new OperationBuildIdentity(new string('a', 40), "sha256:" + new string('b', 64));
        var entry = new ObjectReplicaManifestEntry("clean", SyntheticEvidenceFixture.ObjectKey,
            SyntheticEvidenceFixture.Content.LongLength, SyntheticEvidenceFixture.Sha256, SyntheticEvidenceFixture.Sha256, slot, "VERIFIED");
        var manifest = new ObjectReplicaManifest(1, "SGOL_EVIDENCE_OBJECT_REPLICA", slot, slot, build.Revision, build.ImageDigest, "COMPLETE", null, [entry]);
        manifest = mutation switch
        {
            "slot" => manifest with { ScheduledFor = slot.AddHours(1) },
            "schema" => manifest with { SchemaVersion = 2 },
            "kind" => manifest with { Kind = "OTHER" },
            "status" => manifest with { Status = "FAILED" },
            "error" => manifest with { ErrorClass = "FAILURE" },
            "revision" => manifest with { Revision = new string('c', 40) },
            "image" => manifest with { ImageDigest = "sha256:" + new string('c', 64) },
            "empty" => manifest with { Objects = [] },
            "multiple" => manifest with { Objects = [entry, entry] },
            "role" => manifest with { Objects = [entry with { BucketRole = "quarantine" }] },
            "key" => manifest with { Objects = [entry with { Key = "SENSITIVE_SENTINEL" }] },
            "size" => manifest with { Objects = [entry with { Size = 0 }] },
            "sourceHash" => manifest with { Objects = [entry with { SourceSha256 = "SENSITIVE_SENTINEL" }] },
            "destinationHash" => manifest with { Objects = [entry with { DestinationSha256 = "SENSITIVE_SENTINEL" }] },
            "verified" => manifest with { Objects = [entry with { Status = "COPIED" }] },
            _ => manifest
        };
        if (mutation == "valid") ValidateNetworkManifest(manifest, slot, build);
        else Assert.Equal("NETWORK_AB_MANIFEST_INVALID", Assert.Throws<InvalidOperationException>(() =>
            ValidateNetworkManifest(manifest, slot, build)).Message);
    }

    [Theory]
    [Trait("Category", "Hu035ReplicaNetworkAbPure")]
    [InlineData(0, "REPLICA_INFRASTRUCTURE_FAILED", 1, true)]
    [InlineData(1, "REPLICA_INFRASTRUCTURE_FAILED", 2, true)]
    [InlineData(3, "REPLICA_INFRASTRUCTURE_FAILED", 3, false)]
    [InlineData(3, "SOURCE_OBJECT_HASH_MISMATCH", 1, false)]
    public async Task NetworkAttemptsObserveTheExistingRetryBoundary(int failures, string code, int expected, bool succeeds)
    {
        var attempts = new List<NetworkReplicaAttempt>();
        var calls = 0;
        var replica = new NetworkReplicaStub(() => ++calls <= failures ? Task.FromException(new JobExecutionException(code)) : Task.CompletedTask);
        var action = () => ExecuteReplicaWithBoundedRetryAsync(replica, DateTimeOffset.UtcNow, attempts);
        if (succeeds) await action();
        else await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Equal(expected, calls);
        Assert.Equal(Enumerable.Range(1, expected), attempts.Select(item => item.Attempt));
        Assert.All(attempts, item => Assert.True(item.EndedAt >= item.StartedAt));
        Assert.All(attempts.Take(failures), item => Assert.Equal("FAILED", item.Outcome));
        if (succeeds) Assert.Equal("SUCCEEDED", attempts[^1].Outcome);
    }

    [Fact]
    [Trait("Category", "Hu035ReplicaNetworkAbPure")]
    public async Task NetworkAttemptCancellationIsNotRetriedOrConverted()
    {
        var cancellation = new OperationCanceledException();
        var attempts = new List<NetworkReplicaAttempt>();
        var error = await Assert.ThrowsAsync<OperationCanceledException>(() => ExecuteReplicaWithBoundedRetryAsync(
            new NetworkReplicaStub(() => Task.FromException(cancellation)), DateTimeOffset.UtcNow, attempts));
        Assert.Same(cancellation, error);
        Assert.Single(attempts);
        Assert.Equal("CANCELLED", attempts[0].Outcome);
    }

    [Fact]
    [Trait("Category", "Hu035ReplicaNetworkAbPure")]
    public async Task NetworkAttemptEvidenceContainsOnlySanitizedFailure()
    {
        var failure = new JobExecutionException("SOURCE_OBJECT_HASH_MISMATCH");
        const string sentinel = "https://private.invalid/SECRET_SENTINEL";
        foreach (var name in new[] { "SGOL_REPLICA_STAGE", "SGOL_REPLICA_OPERATION", "SGOL_REPLICA_EXCEPTION_TYPE",
            "SGOL_REPLICA_HTTP_STATUS", "SGOL_REPLICA_S3_ERROR_CODE" }) failure.Data[name] = sentinel;
        var attempts = new List<NetworkReplicaAttempt>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteReplicaWithBoundedRetryAsync(
            new NetworkReplicaStub(() => Task.FromException(failure)), DateTimeOffset.UtcNow, attempts));
        Assert.Single(attempts);
        var json = JsonSerializer.Serialize(attempts, NetworkAbJsonOptions);
        Assert.DoesNotContain("SECRET_SENTINEL", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private.invalid", json, StringComparison.Ordinal);
        Assert.Contains("STAGE=UNKNOWN:OPERATION=UNKNOWN:TYPE=UNKNOWN:HTTP=NONE:S3CODE=UNKNOWN", json, StringComparison.Ordinal);
    }

    private sealed record NetworkReplicaAttempt(int Attempt, DateTimeOffset StartedAt, DateTimeOffset EndedAt, string Outcome, string? Diagnostic);
    private sealed class NetworkReplicaStub(Func<Task> execute) : IObjectReplica
    {
        public Task ExecuteAsync(DateTimeOffset scheduledFor, CancellationToken cancellationToken) => execute();
    }

    private static bool ShouldRetryReplicaFailure(
        JobExecutionException exception,
        int attempt,
        int maximumAttempts) =>
        exception.ErrorCode == "REPLICA_INFRASTRUCTURE_FAILED" && attempt < maximumAttempts;

    private static InvalidOperationException ReplicaPreparationFailure(JobExecutionException exception)
    {
        var stage = NormalizeReplicaStage(exception.Data["SGOL_REPLICA_STAGE"] as string);
        var exceptionType = SanitizeReplicaToken(exception.Data["SGOL_REPLICA_EXCEPTION_TYPE"] as string);
        var httpStatus = NormalizeReplicaHttpStatus(exception.Data["SGOL_REPLICA_HTTP_STATUS"] as string);
        var operation = NormalizeReplicaOperation(exception.Data["SGOL_REPLICA_OPERATION"] as string);
        var s3ErrorCode = SanitizeReplicaToken(exception.Data["SGOL_REPLICA_S3_ERROR_CODE"] as string);
        return new InvalidOperationException(
            $"HU035_REPLICA_PREPARATION_FAILED:{exception.ErrorCode}:" +
            $"STAGE={stage}:OPERATION={operation}:TYPE={exceptionType}:HTTP={httpStatus}:S3CODE={s3ErrorCode}");
    }

    private static string NormalizeReplicaStage(string? value) => value switch
    {
        "CONFIGURATION" => "CONFIGURATION",
        "RECOVER_MANIFEST" => "RECOVER_MANIFEST",
        "LOAD_PREVIOUS_MANIFEST" => "LOAD_PREVIOUS_MANIFEST",
        "REPLICATE_QUARANTINE" => "REPLICATE_QUARANTINE",
        "REPLICATE_CLEAN" => "REPLICATE_CLEAN",
        "VERIFY_SOURCE_SET" => "VERIFY_SOURCE_SET",
        "PUBLISH_MANIFEST" => "PUBLISH_MANIFEST",
        _ => "UNKNOWN"
    };

    private static string NormalizeReplicaOperation(string? value) => value switch
    {
        "LIST_SOURCE_OBJECTS" => "LIST_SOURCE_OBJECTS",
        "HEAD_SOURCE_METADATA" => "HEAD_SOURCE_METADATA",
        "READ_SOURCE_HASH" => "READ_SOURCE_HASH",
        "GET_SOURCE_STREAM" => "GET_SOURCE_STREAM",
        "CHECK_DESTINATION_METADATA" => "CHECK_DESTINATION_METADATA",
        "WRITE_AND_VERIFY_DESTINATION" => "WRITE_AND_VERIFY_DESTINATION",
        "PUT_DESTINATION" => "PUT_DESTINATION",
        "GET_DESTINATION_VERIFY" => "GET_DESTINATION_VERIFY",
        "HEAD_DESTINATION_METADATA" => "HEAD_DESTINATION_METADATA",
        "READ_DESTINATION_HASH" => "READ_DESTINATION_HASH",
        _ => "UNKNOWN"
    };

    private static string NormalizeReplicaHttpStatus(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var numericStatus) &&
        numericStatus is >= 100 and <= 599
            ? numericStatus.ToString(CultureInfo.InvariantCulture)
            : "NONE";

    private static string SanitizeReplicaToken(string? value)
    {
        const int maximumLength = 64;
        return !string.IsNullOrEmpty(value) && value.Length <= maximumLength &&
            value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')
                ? value
                : "UNKNOWN";
    }

    [Theory]
    [Trait("Category", "Hu035ReplicaDiagnostics")]
    [InlineData("put", "PUT_DESTINATION", "REPLICA_INFRASTRUCTURE_FAILED", "PUT")]
    [InlineData("get", "GET_DESTINATION_VERIFY", "REPLICA_INFRASTRUCTURE_FAILED", "PUT,GET")]
    [InlineData("read", "GET_DESTINATION_VERIFY", "REPLICA_INFRASTRUCTURE_FAILED", "PUT,GET")]
    [InlineData("head", "HEAD_DESTINATION_METADATA", "REPLICA_INFRASTRUCTURE_FAILED", "PUT,GET,HEAD")]
    [InlineData("conflict409-head", "HEAD_DESTINATION_METADATA", "REPLICA_INFRASTRUCTURE_FAILED", "PUT,HEAD")]
    [InlineData("conflict412-head", "HEAD_DESTINATION_METADATA", "REPLICA_INFRASTRUCTURE_FAILED", "PUT,HEAD")]
    [InlineData("conflict409-mismatch", "UNKNOWN", "IMMUTABLE_OBJECT_CONFLICT", "PUT,HEAD")]
    [InlineData("conflict412-mismatch", "UNKNOWN", "IMMUTABLE_OBJECT_CONFLICT", "PUT,HEAD")]
    [InlineData("conflict409-404", "UNKNOWN", "IMMUTABLE_OBJECT_CONFLICT", "PUT,HEAD")]
    [InlineData("conflict412-404", "UNKNOWN", "IMMUTABLE_OBJECT_CONFLICT", "PUT,HEAD")]
    [InlineData("hash-mismatch", "UNKNOWN", "S3_OBJECT_VERIFICATION_FAILED", "PUT,GET")]
    [InlineData("metadata-mismatch", "UNKNOWN", "S3_WRITE_VERIFICATION_FAILED", "PUT,GET,HEAD")]
    [InlineData("head-404", "UNKNOWN", "S3_WRITE_VERIFICATION_FAILED", "PUT,GET,HEAD")]
    [InlineData("success", "UNKNOWN", null, "PUT,GET,HEAD")]
    [InlineData("conflict409-success", "UNKNOWN", null, "PUT,HEAD,GET,HEAD")]
    [InlineData("conflict412-success", "UNKNOWN", null, "PUT,HEAD,GET,HEAD")]
    public async Task RealReplicaWriteFailureReachesSanitizedGate(
        string scenario, string expectedOperation, string? expectedCode, string expectedCalls)
    {
        var result = await RunReplicaWriteScenarioAsync(scenario);
        Assert.Equal("SOURCE_LIST,SOURCE_HEAD,DESTINATION_LIST,SOURCE_GET_HASH,SOURCE_GET_STREAM," + expectedCalls,
            string.Join(',', result.Calls));
        if (expectedCode is null)
        {
            Assert.Null(result.Error);
            Assert.Equal("UNKNOWN", result.Context.GetType().GetProperty("Operation")!.GetValue(result.Context));
            return;
        }

        Assert.NotNull(result.Error);
        // ExecuteAsync uses this same capture/classification helper before failure-manifest publication.
        var failure = InvokeObjectReplica<JobExecutionException>("CaptureFailure", result.Error, "REPLICATE_CLEAN", result.Context);
        Assert.Equal(expectedCode, failure.ErrorCode);
        Assert.Equal(expectedOperation, failure.Data["SGOL_REPLICA_OPERATION"]);
        result.Context.GetType().GetProperty("Operation")!.SetValue(result.Context, "SOURCE_HEAD");
        Assert.Equal(expectedOperation, failure.Data["SGOL_REPLICA_OPERATION"]);
        Assert.Equal(5, failure.Data.Count);
        Assert.Null(failure.InnerException);
        var gate = ReplicaPreparationFailure(failure);
        Assert.Contains($"STAGE=REPLICATE_CLEAN:OPERATION={expectedOperation}:", gate.Message, StringComparison.Ordinal);
        Assert.Contains(expectedCode, gate.Message, StringComparison.Ordinal);
        Assert.Null(gate.InnerException);
        Assert.DoesNotContain("PRIVATE_SENTINEL", gate.Message, StringComparison.Ordinal);
        foreach (var value in failure.Data.Values)
            Assert.DoesNotContain("PRIVATE_SENTINEL", Assert.IsType<string>(value), StringComparison.Ordinal);
        if (result.Error is AmazonS3Exception)
            Assert.EndsWith("TYPE=AmazonS3Exception:HTTP=500:S3CODE=InternalError", gate.Message, StringComparison.Ordinal);
        if (scenario == "read")
            Assert.EndsWith("TYPE=IOException:HTTP=NONE:S3CODE=UNKNOWN", gate.Message, StringComparison.Ordinal);
        Assert.Equal(expectedCode == "REPLICA_INFRASTRUCTURE_FAILED", ShouldRetryReplicaFailure(failure, 1, 3));
    }

    [Theory]
    [Trait("Category", "Hu035ReplicaDiagnostics")]
    [InlineData("cancel-put")]
    [InlineData("cancel-get")]
    [InlineData("cancel-head")]
    [InlineData("cancel-read")]
    public async Task RealReplicaWriteCancellationIsNotClassifiedAsInfrastructure(string scenario)
    {
        var result = await RunReplicaWriteScenarioAsync(scenario);
        var cancellation = Assert.IsType<OperationCanceledException>(result.Error);
        var reflected = Assert.Throws<TargetInvocationException>(() =>
            InvokeObjectReplica<JobExecutionException>("CaptureFailure", cancellation, "REPLICATE_CLEAN", result.Context));
        Assert.Same(cancellation, reflected.InnerException);
    }

    [Theory]
    [Trait("Category", "Hu035ReplicaDiagnostics")]
    [InlineData("put")]
    [InlineData("get")]
    [InlineData("head")]
    [InlineData("success")]
    public async Task UninstrumentedStorePreservesBehaviorWithoutMarkers(string scenario)
    {
        var bytes = SyntheticEvidenceFixture.Content;
        using var cancellation = new CancellationTokenSource();
        await using var stream = new MemoryStream(bytes, writable: false);
        var calls = new List<string>();
        var injected = new AmazonS3Exception("PRIVATE_SENTINEL") { StatusCode = HttpStatusCode.InternalServerError };
        var client = DispatchProxy.Create<IAmazonS3, ReplicaWriteClientStub>();
        ((ReplicaWriteClientStub)client).Handler = (method, args) =>
        {
            Assert.Equal(cancellation.Token, (CancellationToken)args[1]!);
            switch (method.Name)
            {
                case nameof(IAmazonS3.PutObjectAsync):
                    calls.Add("PUT");
                    Assert.Same(stream, ((PutObjectRequest)args[0]!).InputStream);
                    return scenario == "put" ? FailLater<PutObjectResponse>(injected) : Task.FromResult(new PutObjectResponse());
                case nameof(IAmazonS3.GetObjectAsync):
                    calls.Add("GET");
                    return scenario == "get" ? FailLater<GetObjectResponse>(injected)
                        : Task.FromResult(new GetObjectResponse { ResponseStream = new MemoryStream(bytes, writable: false) });
                case nameof(IAmazonS3.GetObjectMetadataAsync):
                    calls.Add("HEAD");
                    return scenario == "head" ? FailLater<GetObjectMetadataResponse>(injected)
                        : Task.FromResult(ReplicaMetadata(bytes.LongLength));
                default: throw new InvalidOperationException("UNEXPECTED_TEST_CALL");
            }
        };
        var store = new S3OperationStore(client);
        var error = await Record.ExceptionAsync(() => store.PutStreamVerifiedAsync("synthetic", "synthetic", stream,
            bytes.LongLength, SyntheticEvidenceFixture.Sha256, SyntheticEvidenceFixture.ContentType, null, cancellation.Token));
        if (scenario == "success") Assert.Null(error);
        else Assert.Same(injected, error);
        Assert.Equal(scenario == "put" ? "PUT" : scenario == "get" ? "PUT,GET" : "PUT,GET,HEAD", string.Join(',', calls));
        Assert.Equal("UNKNOWN", typeof(S3OperationStore).GetProperty("ReplicaWriteOperation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store));
        Assert.False((bool)typeof(S3OperationStore).GetProperty("CaptureReplicaWriteOperation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store)!);
    }

    private static async Task<(Exception? Error, object Context, List<string> Calls)> RunReplicaWriteScenarioAsync(string scenario)
    {
        var bytes = SyntheticEvidenceFixture.Content;
        var calls = new List<string>();
        using var cancellation = new CancellationTokenSource();
        var injected = scenario.StartsWith("cancel-", StringComparison.Ordinal)
            ? (Exception)new OperationCanceledException("PRIVATE_SENTINEL", cancellation.Token)
            : new AmazonS3Exception("PRIVATE_SENTINEL endpoint bucket key metadata credential content")
            { StatusCode = HttpStatusCode.InternalServerError, ErrorCode = "InternalError" };
        injected.Data["PRIVATE_SENTINEL"] = "PRIVATE_SENTINEL";
        Stream? transferStream = null;
        var sourceGets = 0;
        var source = DispatchProxy.Create<IAmazonS3, ReplicaWriteClientStub>();
        ((ReplicaWriteClientStub)source).Handler = (method, args) =>
        {
            Assert.Equal(cancellation.Token, (CancellationToken)args[1]!);
            switch (method.Name)
            {
                case nameof(IAmazonS3.ListObjectsV2Async):
                    calls.Add("SOURCE_LIST");
                    return Task.FromResult(new ListObjectsV2Response { S3Objects = [new S3Object { Key = "synthetic", Size = bytes.LongLength }] });
                case nameof(IAmazonS3.GetObjectMetadataAsync):
                    calls.Add("SOURCE_HEAD");
                    return Task.FromResult(ReplicaMetadata(bytes.LongLength));
                case nameof(IAmazonS3.GetObjectAsync):
                    sourceGets++;
                    calls.Add(sourceGets == 1 ? "SOURCE_GET_HASH" : "SOURCE_GET_STREAM");
                    var response = new GetObjectResponse { ResponseStream = new MemoryStream(bytes, writable: false) };
                    if (sourceGets == 2) transferStream = response.ResponseStream;
                    return Task.FromResult(response);
                default: throw new InvalidOperationException("UNEXPECTED_TEST_CALL");
            }
        };
        var destination = DispatchProxy.Create<IAmazonS3, ReplicaWriteClientStub>();
        ((ReplicaWriteClientStub)destination).Handler = (method, args) =>
        {
            Assert.Equal(cancellation.Token, (CancellationToken)args[1]!);
            switch (method.Name)
            {
                case nameof(IAmazonS3.ListObjectsV2Async):
                    calls.Add("DESTINATION_LIST");
                    return Task.FromResult(new ListObjectsV2Response { S3Objects = [] });
                case nameof(IAmazonS3.PutObjectAsync):
                    calls.Add("PUT");
                    var put = Assert.IsType<PutObjectRequest>(args[0]);
                    Assert.Same(transferStream, put.InputStream);
                    Assert.Equal("*", put.IfNoneMatch);
                    Assert.False(put.AutoCloseStream);
                    Assert.Equal(bytes.LongLength, put.Headers.ContentLength);
                    Assert.Equal(SyntheticEvidenceFixture.ContentType, put.ContentType);
                    Assert.Equal(SyntheticEvidenceFixture.Sha256, put.Metadata["sha256"]);
                    Assert.Equal(SyntheticEvidenceFixture.Sha256, put.Metadata["sgol-sha256"]);
                    Assert.Equal(bytes.LongLength.ToString(CultureInfo.InvariantCulture), put.Metadata["sgol-size-bytes"]);
                    Assert.Equal(SyntheticEvidenceFixture.MetadataMediaType, put.Metadata["sgol-media-type"]);
                    if (scenario.StartsWith("conflict", StringComparison.Ordinal))
                        return FailLater<PutObjectResponse>(new AmazonS3Exception("PRIVATE_SENTINEL")
                        { StatusCode = scenario.StartsWith("conflict409", StringComparison.Ordinal) ? HttpStatusCode.Conflict : HttpStatusCode.PreconditionFailed });
                    return scenario is "put" or "cancel-put" ? FailLater<PutObjectResponse>(injected) : Task.FromResult(new PutObjectResponse());
                case nameof(IAmazonS3.GetObjectAsync):
                    calls.Add("GET");
                    if (scenario is "get" or "cancel-get") return FailLater<GetObjectResponse>(injected);
                    Stream verified = scenario is "read" or "cancel-read"
                        ? new ReplicaFailingReadStream(scenario == "read" ? new IOException("PRIVATE_SENTINEL") : injected)
                        : new MemoryStream(scenario == "hash-mismatch" ? "corrupt"u8.ToArray() : bytes, writable: false);
                    return Task.FromResult(new GetObjectResponse { ResponseStream = verified });
                case nameof(IAmazonS3.GetObjectMetadataAsync):
                    calls.Add("HEAD");
                    if (scenario.EndsWith("404", StringComparison.Ordinal))
                        return FailLater<GetObjectMetadataResponse>(new AmazonS3Exception("PRIVATE_SENTINEL") { StatusCode = HttpStatusCode.NotFound });
                    if (scenario is "head" or "cancel-head" || scenario.EndsWith("-head", StringComparison.Ordinal))
                        return FailLater<GetObjectMetadataResponse>(injected);
                    return Task.FromResult(ReplicaMetadata(scenario.EndsWith("mismatch", StringComparison.Ordinal) ? 0 : bytes.LongLength));
                default: throw new InvalidOperationException("UNEXPECTED_TEST_CALL");
            }
        };
        var context = Activator.CreateInstance(typeof(ObjectReplica).GetNestedType("ReplicaDiagnosticContext", BindingFlags.NonPublic)!, nonPublic: true)!;
        var method = typeof(ObjectReplica).GetMethod("ReplicateBucketAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        var task = (Task)method.Invoke(null, [source, destination, "synthetic-source", "synthetic-destination", "clean", false, 500,
            null, new List<ObjectReplicaManifestEntry>(), DateTimeOffset.UnixEpoch, context, cancellation.Token])!;
        return (await Record.ExceptionAsync(() => task), context, calls);
    }

    private static GetObjectMetadataResponse ReplicaMetadata(long size)
    {
        var response = new GetObjectMetadataResponse { ContentLength = size };
        response.Headers.ContentType = SyntheticEvidenceFixture.ContentType;
        response.Metadata["x-amz-meta-sha256"] = SyntheticEvidenceFixture.Sha256;
        response.Metadata["x-amz-meta-sgol-sha256"] = SyntheticEvidenceFixture.Sha256;
        response.Metadata["x-amz-meta-sgol-size-bytes"] = SyntheticEvidenceFixture.Content.LongLength.ToString(CultureInfo.InvariantCulture);
        response.Metadata["x-amz-meta-sgol-media-type"] = SyntheticEvidenceFixture.MetadataMediaType;
        return response;
    }

    private static async Task<T> FailLater<T>(Exception error)
    {
        await Task.Yield();
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        return default!;
    }

    public class ReplicaWriteClientStub : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args!);
    }

    private sealed class ReplicaFailingReadStream(Exception error) : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            new(FailLater<int>(error));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            FailLater<int>(error);
    }

    private static T InvokeObjectReplica<T>(string methodName, params object?[] arguments)
    {
        var method = typeof(ObjectReplica).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(candidate => candidate.Name == methodName && candidate.GetParameters().Length == arguments.Length);
        return Assert.IsType<T>(method.Invoke(null, arguments));
    }

    private static async Task MutateReplicaObjectAsync(string testCase)
    {
        const string key = SyntheticEvidenceFixture.ObjectKey;
        var sourceBucket = Required("Replica__Source__CleanBucket");
        var destinationBucket = Required("Replica__Destination__CleanBucket");
        using var source = S3Client("Replica__Source", "Replica__Source__AccessKey",
            "Replica__Source__SecretKey");
        using var destination = S3Client("Replica__Destination", "Replica__Destination__AccessKey",
            "Replica__Destination__SecretKey");
        if (testCase == "evidence_missing")
        {
            await destination.DeleteObjectAsync(destinationBucket, key);
            return;
        }
        if (testCase == "evidence_corrupt")
        {
            await destination.PutObjectAsync(new PutObjectRequest
            {
                BucketName = destinationBucket,
                Key = key,
                ContentBody = "corrupt synthetic content",
                ContentType = SyntheticEvidenceFixture.ContentType
            });
            return;
        }
        using var sourceObject = await source.GetObjectAsync(sourceBucket, key);
        await using var content = new MemoryStream();
        await sourceObject.ResponseStream.CopyToAsync(content);
        content.Position = 0;
        var request = new PutObjectRequest
        {
            BucketName = destinationBucket,
            Key = key,
            InputStream = content,
            AutoCloseStream = false,
            ContentType = SyntheticEvidenceFixture.ContentType
        };
        foreach (var metadataKey in sourceObject.Metadata.Keys)
            request.Metadata[metadataKey] = sourceObject.Metadata[metadataKey];
        await destination.PutObjectAsync(request);
    }

    private static AmazonS3Client S3Client(string section, string accessName, string secretName) => new(
        new BasicAWSCredentials(Required(accessName), Required(secretName)), new AmazonS3Config
        {
            ServiceURL = Required(section + "__Endpoint"),
            AuthenticationRegion = Required(section + "__Region"),
            ForcePathStyle = true,
            UseHttp = true,
            MaxErrorRetry = 0
        });

    private static async Task<string> HashS3Async(string uri)
        => Convert.ToHexStringLower(SHA256.HashData(await ReadS3Async(uri)));

    private static async Task<byte[]> ReadS3Async(string uri)
    {
        var (bucket, key) = ParseS3(uri);
        using var client = S3Client("Backup__Storage", "Backup__Storage__AccessKey",
            "Backup__Storage__SecretKey");
        using var response = await client.GetObjectAsync(bucket, key);
        await using var content = new MemoryStream();
        await response.ResponseStream.CopyToAsync(content);
        return content.ToArray();
    }

    private static (string Bucket, string Key) ParseS3(string uri)
    {
        var parsed = new Uri(uri);
        if (parsed.Scheme != "s3" || string.IsNullOrWhiteSpace(parsed.Host) || parsed.AbsolutePath.Length < 2)
            throw new InvalidOperationException("Synthetic S3 URI is invalid.");
        return (parsed.Host, parsed.AbsolutePath[1..]);
    }

    private static async Task<FixtureActors> SeedFunctionalFixtureAsync(SgolDbContext context, DeterministicSeed seed)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        var at = seed.Timestamp;
        var direction = AddActor(context, seed, "DIR", CanonicalRole.Direction, at, includeHistory: true);
        var denied = AddActor(context, seed, "DENIED", CanonicalRole.Administration, at, includeHistory: false);
        var responsible = AddActor(context, seed, "RESP", CanonicalRole.Subcoordination, at, includeHistory: true);
        context.IdentityCredentials.AddRange(
            Credential(direction.UserId, "hu035-direction"),
            Credential(denied.UserId, "hu035-denied"),
            Credential(responsible.UserId, "hu035-responsible"));
        context.DirectionBootstrapMarkers.Add(new DirectionBootstrapMarker
        {
            Singleton = true,
            CompletedAt = at.AddDays(-90)
        });

        var availability1 = new AvailabilityDayVersion(seed.Id("availability-1"), responsible.PersonId,
            BranchScope.LorettaId, DateOnly.FromDateTime(at.UtcDateTime), false, direction.UserId);
        var availability2 = availability1.CreateSuccessor(seed.Id("availability-2"), true, direction.UserId);
        context.AvailabilityDayVersions.AddRange(availability1, availability2);

        var releaseId = seed.Id("release");
        var release = new ConfigurationRelease(releaseId, BranchScope.LorettaId);
        release.ApplyPublished(Published(releaseId, at.AddDays(-10)), 1, direction.UserId, at.AddDays(-10));
        context.ConfigurationReleases.Add(release);

        var calendarId = seed.Id("calendar");
        var calendar = new CalendarDayVersion(calendarId, BranchScope.LorettaId,
            DateOnly.FromDateTime(at.UtcDateTime), CalendarContract.WorkingDay, true, releaseId,
            "Fixture sintético HU-035");
        calendar.ApplyPublished(Published(calendarId, at.AddDays(-10)));
        context.CalendarDayVersions.Add(calendar);

        var task = TaskDefinitionCatalog.Require("TAR-0008");
        var taskVersionId = seed.Id("task-version");
        var taskVersion = new TaskDefinitionVersion(taskVersionId, task.Id, 1, 1,
            JsonDocument.Parse("{}"), releaseId);
        taskVersion.ApplyPublished(Published(taskVersionId, at.AddDays(-10)), true);
        context.TaskDefinitionVersions.Add(taskVersion);

        var eligibilityPolicyId = seed.Id("eligibility-policy");
        var eligibilityPolicy = new EligibilityPolicyVersion(eligibilityPolicyId, task.Id, taskVersionId,
            releaseId, null, 1, CanonicalRole.Subcoordination, true, null);
        eligibilityPolicy.ApplyPublished(Published(eligibilityPolicyId, at.AddDays(-10)));
        context.EligibilityPolicyVersions.Add(eligibilityPolicy);

        var activationRuleId = seed.Id("activation-rule");
        var activationRule = new ActivationRuleVersion(activationRuleId, task.Id, taskVersionId, releaseId,
            null, 1, ActivationModes.Manual, JsonDocument.Parse("null").RootElement,
            ActivationOriginSchemas.ManualReference);
        activationRule.ApplyPublished(Published(activationRuleId, at.AddDays(-10)));
        context.ActivationRuleVersions.Add(activationRule);

        var evidencePolicyId = seed.Id("evidence-policy");
        var evidencePolicy = new EvidencePolicyVersion(evidencePolicyId, task.Id, taskVersionId, releaseId,
            null, 1);
        evidencePolicy.ApplyPublished(Published(evidencePolicyId, at.AddDays(-10)));
        context.EvidencePolicyVersions.Add(evidencePolicy);
        var requirementDefinitions = EvidencePolicyCatalog.Require(task.TaskCode).Take(2).ToArray();
        var requirements = requirementDefinitions.Select((definition, index) =>
            new EvidenceRequirementVersion(seed.Id($"requirement-{index}"), evidencePolicyId, task.Id, definition))
            .ToArray();
        context.EvidenceRequirementVersions.AddRange(requirements);

        var validationDefinition = ValidationPolicyCatalog.Require(task.TaskCode);
        var validationPolicyId = seed.Id("validation-policy");
        var validationPolicy = new ValidationPolicyVersion(validationPolicyId, task.Id, taskVersionId, releaseId,
            null, 1, true, validationDefinition.ExecutorRole, ValidationPolicyValues.ImmediateSuperior,
            validationDefinition.ValidatorRole, ValidationPolicyValues.AllowedResults);
        validationPolicy.ApplyPublished(Published(validationPolicyId, at.AddDays(-10)));
        context.ValidationPolicyVersions.Add(validationPolicy);

        var range = WeekContract.Calculate(2026, 37);
        var period = new WeekPeriod(seed.Id("week"), BranchScope.LorettaId, 2026, 37,
            range.StartsOn, range.EndsOn, WeekContract.Elapsed);
        context.WeekPeriods.Add(period);
        var plan = new WorkPlan(seed.Id("plan"), BranchScope.LorettaId, period.Id);
        plan.ApplyPublication();
        context.WorkPlans.Add(plan);

        var generation = new GenerationRequest(seed.Id("generation"), seed.Id("generation-key"),
            seed.Hash("generation-request"), activationRuleId, BranchScope.LorettaId, period.Id,
            ActivationOriginSchemas.ManualReference, "HU035-SYNTHETIC", direction.UserId, at.AddDays(-5));
        context.GenerationRequests.Add(generation);
        await context.SaveChangesAsync();

        var obligation = new WorkObligation(seed.Id("obligation"), taskVersionId, BranchScope.LorettaId,
            period.Id, generation.Id, "HU035-SYNTHETIC", evidencePolicyId, validationPolicyId);
        context.WorkObligations.Add(obligation);
        await context.SaveChangesAsync();
        generation.LinkObligation(obligation.Id);
        await context.SaveChangesAsync();

        var evaluation = new EligibilityEvaluation(seed.Id("eligibility-evaluation"),
            seed.Id("eligibility-request"), obligation.Id, at.AddDays(-4), range.StartsOn,
            EligibilityDateSources.ManualRequest, eligibilityPolicyId, JsonDocument.Parse("{\"schemaVersion\":1}"),
            EligibilityResults.EligibleCandidates);
        context.EligibilityEvaluations.Add(evaluation);
        context.EligibilityCandidates.Add(new EligibilityCandidate(evaluation.Id, responsible.PersonId,
            "HU035-RESP", true, JsonDocument.Parse("[]")));

        var assignment1 = new AssignmentVersion(seed.Id("assignment-1"), obligation.Id, denied.PersonId,
            AssignmentVersionStatuses.Current, AssignmentTypes.Automatic, JsonDocument.Parse("{}"), at.AddDays(-4));
        assignment1.Supersede();
        var assignment2 = new AssignmentVersion(seed.Id("assignment-2"), obligation.Id, responsible.PersonId,
            AssignmentVersionStatuses.Current, AssignmentTypes.Correction, JsonDocument.Parse("{}"), at.AddDays(-3),
            "Corrección sintética", direction.UserId, assignment1.Id);
        context.AssignmentVersions.AddRange(assignment1, assignment2);
        context.InternalNotices.AddRange(
            new InternalNotice(seed.Id("notice-1"), denied.UserId, assignment1.Id, at.AddDays(-4)),
            new InternalNotice(seed.Id("notice-2"), responsible.UserId, assignment2.Id, at.AddDays(-3)));

        var planVersion1 = new PlanVersion(seed.Id("plan-version-1"), plan.Id, 1,
            CanonicalRole.Direction, direction.UserId, at.AddDays(-3), null, plan.RowVersion);
        planVersion1.Supersede();
        plan.ApplyPublication();
        var planVersion2 = new PlanVersion(seed.Id("plan-version-2"), plan.Id, 2,
            CanonicalRole.Direction, direction.UserId, at.AddDays(-2), planVersion1.Id, plan.RowVersion);
        Assert.NotEqual(planVersion1.PlanRowVersion, planVersion2.PlanRowVersion);
        context.PlanVersions.AddRange(planVersion1, planVersion2);
        context.PlanVersionObligations.Add(new PlanVersionObligation(planVersion2.Id, obligation.Id, assignment2.Id));

        SyntheticEvidenceFixture.AssertContract();
        var fileContent = SyntheticEvidenceFixture.Content;
        var fileHash = SyntheticEvidenceFixture.Sha256;
        var fileItem = new EvidenceItem(seed.Id("evidence-file-item"), obligation.Id, evidencePolicyId,
            requirements[0].Id, requirements[0].RequirementCode, requirements[0].Kind, at.AddDays(-2));
        var file = new FileObject(seed.Id("file"), BranchScope.LorettaId, obligation.Id, evidencePolicyId,
            requirements[0].Id, requirements[0].RequirementCode, requirements[0].Kind, null,
            SyntheticEvidenceFixture.ObjectKey, SyntheticEvidenceFixture.OriginalName,
            SyntheticEvidenceFixture.ContentType, fileContent.LongLength, fileHash,
            responsible.UserId, at.AddDays(-2));
        file.ConfirmUpload(at.AddDays(-2).AddMinutes(1));
        file.MarkClean(SyntheticEvidenceFixture.ContentType, "synthetic", at.AddDays(-2).AddMinutes(2));
        file.Link(fileItem.Id, at.AddDays(-2).AddMinutes(3));
        var fileVersion = new EvidenceVersion(seed.Id("evidence-file-version"), fileItem.Id, 1, file.Id,
            responsible.UserId, at.AddDays(-2).AddMinutes(3));
        context.FileObjects.Add(file);
        context.EvidenceItems.Add(fileItem);
        context.EvidenceVersions.Add(fileVersion);

        var structuredItem = new EvidenceItem(seed.Id("evidence-structured-item"), obligation.Id,
            evidencePolicyId, requirements[1].Id, requirements[1].RequirementCode, requirements[1].Kind,
            at.AddDays(-2));
        var structured1 = new EvidenceVersion(seed.Id("evidence-structured-version-1"), structuredItem.Id, 1,
            JsonDocument.Parse(SyntheticEvidenceFixture.FirstSequencePayload), responsible.UserId,
            at.AddDays(-2));
        structured1.Supersede();
        var structured2 = new EvidenceVersion(seed.Id("evidence-structured-version-2"), structuredItem.Id, 2,
            JsonDocument.Parse(SyntheticEvidenceFixture.SecondSequencePayload), responsible.UserId,
            at.AddDays(-1), "Sustitución sintética", structured1.Id);
        context.EvidenceItems.Add(structuredItem);
        context.EvidenceVersions.AddRange(structured1, structured2);
        await context.SaveChangesAsync();

        var reviewEvaluation = EvidenceReviewEvaluator.Evaluate(new EvidenceReviewEvaluationInput(
            obligation.Id, evidencePolicyId, task.TaskCode,
            requirements.Select(requirement => new EvidenceReviewExpectedRequirement(requirement.RequirementCode,
                requirement.Kind, requirement.ConditionCode, requirement.Ordinal)).ToArray(),
            [
                new EvidenceReviewRequirementInput(requirements[0].Id, requirements[0].RequirementCode,
                    requirements[0].Kind, requirements[0].ConditionCode, requirements[0].Ordinal, true,
                    fileItem.Id, [new EvidenceReviewVersionInput(fileVersion.Id, EvidenceVersionStatuses.Current,
                        file.Id, null, new EvidenceReviewFileInput(file.Id, requirements[0].Id, fileItem.Id,
                            file.ScanStatus, file.BucketClass))]),
                new EvidenceReviewRequirementInput(requirements[1].Id, requirements[1].RequirementCode,
                    requirements[1].Kind, requirements[1].ConditionCode, requirements[1].Ordinal, true,
                    structuredItem.Id, [new EvidenceReviewVersionInput(structured2.Id,
                        EvidenceVersionStatuses.Current, null, structured2.StructuredPayload, null)])
            ]));
        Assert.Equal(EvidenceReviewResults.Complete, reviewEvaluation.Result);
        var review = new EvidenceReviewSnapshot(seed.Id("review"), obligation.Id, evidencePolicyId,
            reviewEvaluation, responsible.UserId, at.AddDays(-1), seed.Id("review-correlation"));
        context.EvidenceReviewSnapshots.Add(review);
        context.ExecutionResults.Add(new ExecutionResult(seed.Id("execution"), obligation.Id, review.Id,
            responsible.UserId, at.AddDays(-1)));
        obligation.Conclude(responsible.UserId, at.AddDays(-1), obligation.RowVersion);
        await context.SaveChangesAsync();

        var validation = new ValidationRequirement(seed.Id("validation"), obligation.Id, validationPolicyId,
            at.AddDays(-1));
        validation.Resolve(at.AddHours(-12), validation.RowVersion);
        var decision1 = new ValidationDecisionVersion(seed.Id("decision-1"), validation.Id, 1,
            ValidationResults.Incomplete, "Fundamento sintético inicial", ValidationAuthorityTypes.Ordinary,
            denied.UserId, denied.PersonId, CanonicalRole.Administration, assignment2.Id, responsible.PersonId,
            at.AddHours(-12), null, null, review.Id);
        decision1.Supersede();
        validation.Advance(at.AddHours(-6), validation.RowVersion);
        var decision2 = new ValidationDecisionVersion(seed.Id("decision-2"), validation.Id, 2,
            ValidationResults.Fulfilled, "Fundamento sintético final", SyntheticReplacementAuthority,
            denied.UserId, denied.PersonId, CanonicalRole.Administration, assignment2.Id, responsible.PersonId,
            at.AddHours(-6), "Corrección sintética", decision1.Id, review.Id);
        context.ValidationRequirements.Add(validation);
        context.ValidationDecisionVersions.AddRange(decision1, decision2);

        context.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Scope = "HU035_SYNTHETIC", Key = seed.Id("idempotency"), RequestHash = seed.Hash("idempotency"),
            Status = "COMPLETED", ResourceType = "WORK_OBLIGATION", ResourceId = obligation.Id,
            ResponseCode = 201, CreatedAt = at.AddDays(-5), ExpiresAt = at.AddDays(5), ProtocolVersion = 1,
            ResponsePayload = JsonDocument.Parse("{\"schemaVersion\":1}"),
            ResponseLocation = $"/api/v1/obligations/{obligation.Id:D}", ResponseEtag = "\"1\""
        });
        context.OutboxEvents.Add(new OutboxEvent
        {
            Id = seed.Id("fixture-outbox"), EventType = "HU035_SYNTHETIC_EVENT", AggregateId = obligation.Id,
            Payload = SyntheticOutboxPayload(seed), CreatedAt = at.AddDays(-1), AvailableAt = at.AddDays(-1),
            ProcessedAt = at.AddDays(-1).AddMinutes(1), AttemptCount = 1
        });
        context.ScheduledJobRuns.Add(new ScheduledJobRun
        {
            Id = seed.Id("job-run"), JobName = "HU035_SYNTHETIC_JOB", ScheduledFor = at.AddDays(-1),
            StartedAt = at.AddDays(-1), EndedAt = at.AddDays(-1).AddMinutes(1),
            Status = ScheduledJobStatuses.Succeeded, Checkpoint = SyntheticJobCheckpoint
        });
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return new(direction.UserId, denied.UserId);
    }

    private static IdentityCredential Credential(Guid userId, string name) => new()
    {
        UserId = userId,
        UserName = name,
        NormalizedUserName = name.ToUpperInvariant(),
        PasswordHash = "synthetic-non-secret-hash"
    };

    private static string SyntheticOutboxPayload(DeterministicSeed seed) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        correlationId = seed.Id("fixture-outbox-correlation"),
        data = new { kind = "HU035_SYNTHETIC" }
    });

    private static VersionRecord Published(Guid id, DateTimeOffset at) => new(id, VersionStatuses.Current,
        at, null, "Fixture sintético HU-035", null, 2);

    private static SeededActor AddActor(SgolDbContext context, DeterministicSeed seed, string code, string role, DateTimeOffset now,
        bool includeHistory)
    {
        var personId = seed.Id($"person-{code}");
        var userId = seed.Id($"user-{code}");
        context.People.Add(new Person
        {
            Id = personId,
            StableCode = code,
            DisplayName = $"Persona sintética {code}",
            CreatedAt = now.AddDays(-60)
        });
        var employment = new EmploymentVersion(seed.Id($"employment-{code}-1"), personId, BranchScope.LorettaId,
            EmploymentStatus.Active, now.AddDays(-60));
        if (includeHistory)
        {
            context.EmploymentVersions.Add(employment);
            employment = employment.CreateSuccessor(seed.Id($"employment-{code}-2"), EmploymentStatus.Active,
                "Dirección sintética", "Diurno", now.AddDays(-30));
        }
        context.EmploymentVersions.Add(employment);
        context.AppUsers.Add(new AppUser
        {
            Id = userId,
            PersonId = personId,
            Status = AccountStatus.Active,
            MustChangePassword = false,
            MfaEnrolledAt = now.AddDays(-30),
            SecurityStamp = $"synthetic-{userId:N}"
        });
        if (includeHistory)
        {
            context.RoleAssignmentVersions.Add(new RoleAssignmentVersion
            {
                Id = seed.Id($"role-{code}-1"), UserId = userId, BranchId = BranchScope.LorettaId,
                RoleCode = CanonicalRole.Administration, Status = RoleAssignmentStatus.Superseded,
                ValidFrom = now.AddDays(-60), ValidTo = now.AddDays(-30), RowVersion = 2
            });
        }
        context.RoleAssignmentVersions.Add(new RoleAssignmentVersion
        {
            Id = seed.Id($"role-{code}-2"), UserId = userId, BranchId = BranchScope.LorettaId,
            RoleCode = role, Status = RoleAssignmentStatus.Active, ValidFrom = now.AddDays(-30),
            SupersedesId = includeHistory ? seed.Id($"role-{code}-1") : null, RowVersion = includeHistory ? 2 : 1
        });
        return new(personId, userId);
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"Missing HU-035 AMD64 setting: {name}");

    private sealed class FixedClock(DateTimeOffset value) : IClock
    {
        public DateTimeOffset UtcNow { get; } = value;
    }

    private sealed class AdvancingClock(DateTimeOffset start) : IClock
    {
        private int seconds;

        public DateTimeOffset UtcNow => start.AddSeconds(Interlocked.Increment(ref seconds));
    }

    private sealed record SeededActor(Guid PersonId, Guid UserId);

    private sealed record FixtureActors(Guid DirectionUserId, Guid DeniedUserId);

    private sealed record Amd64Case(string Name, string ExpectedStatus, string? ExpectedCode);

    private sealed class DeterministicUuidGenerator(DeterministicSeed seed, string scope) : IUuidGenerator
    {
        private int sequence;

        public Guid NewUuid() => seed.Id($"runtime-{scope}-{Interlocked.Increment(ref sequence)}");
    }

    private sealed class DeterministicSeed(string revision)
    {
        private readonly byte[] prefix = Encoding.UTF8.GetBytes($"HU-035|{revision}|");

        public DateTimeOffset Timestamp { get; } = new DateTimeOffset(2026, 9, 14, 18, 0, 0,
            TimeSpan.Zero).AddSeconds(Convert.ToInt32(revision[..2], 16));

        public Guid Id(string name)
        {
            var bytes = SHA256.HashData(prefix.Concat(Encoding.UTF8.GetBytes(name)).ToArray())[..16];
            bytes[6] = (byte)((bytes[6] & 0x0f) | 0x70);
            bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
            return new Guid(bytes, bigEndian: true);
        }

        public string Hash(string name) => Convert.ToHexStringLower(
            SHA256.HashData(prefix.Concat(Encoding.UTF8.GetBytes(name)).ToArray()));
    }

    private sealed class TestOutboxWriter(SgolDbContext context, IUuidGenerator uuid, DateTimeOffset now) : IOutboxWriter
    {
        public OutboxEvent Enqueue(string eventType, Guid? aggregateId, JsonElement data, Guid correlationId,
            DateTimeOffset? availableAt = null)
        {
            var item = new OutboxEvent
            {
                Id = uuid.NewUuid(), EventType = eventType, AggregateId = aggregateId,
                Payload = JsonSerializer.Serialize(new { schemaVersion = 1, correlationId, data }),
                CreatedAt = now, AvailableAt = availableAt ?? now
            };
            context.OutboxEvents.Add(item);
            return item;
        }
    }
}
