using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class PostgreSqlPersistenceTests : IAsyncLifetime
{
    private const string InitialMigrationId = "20260827000000_InitializePersistence";
    private const string AuditMigrationId = "20260831192942_AddAuditEvent";
    private const string BootstrapMigrationId = "20260901190333_AddDirectionBootstrap";
    private const string BranchScopeMigrationId = "20260901223021_EnforceLorettaBranchScope";
    private const string PersonAdministrationMigrationId = "20260901233438_AddPersonAdministration";
    private const string RoleAdministrationMigrationId = "20260902190915_AddRoleAdministration";
    private const string AvailabilityAdministrationMigrationId = "20260902200533_AddAvailabilityAdministration";
    private const string ConfigurationReleaseMigrationId = "20260902231629_AddConfigurationReleases";
    private const string CalendarAdministrationMigrationId = "20260903001027_AddCalendarAdministration";
    private const string WeekPeriodsMigrationId = "20260903170116_AddWeekPeriods";
    private const string TaskDefinitionsMigrationId = "20260903175354_AddTaskDefinitions";
    private const string EligibilityPoliciesMigrationId = "20260903191642_AddEligibilityPolicies";
    private const string ActivationPoliciesMigrationId = "20260903201854_AddActivationPolicies";
    private const string GenerationRequestsMigrationId = "20260903212356_AddGenerationRequests";
    private const string WorkObligationsMigrationId = "20260903220652_AddWorkObligations";
    private const string EligibilityEvaluationsMigrationId = "20260903225522_AddEligibilityEvaluations";
    private const string AssignmentVersionsMigrationId = "20260904155837_AddAssignmentVersions";
    private const string WorkPlansMigrationId = "20260904190309_AddWorkPlans";
    private const string PlanPublicationsMigrationId = "20260904200228_AddPlanPublications";
    private const string JobInfrastructureMigrationId = "20260904211401_AddJobInfrastructure";
    private const string RecurringGenerationMigrationId = "20260904223610_AllowSystemRecurringGenerationRequests";
    private const string EvidencePoliciesMigrationId = "20260905203518_AddEvidencePolicies";
    private const string EvidenceContributionMigrationId = "20260907203912_AddVersionedEvidenceContribution";
    private const string StructuredEvidenceMigrationId = "20260908005832_EnableStructuredEvidence";
    private const string EvidenceReviewMigrationId = "20260908193819_AddEvidenceReviewSnapshots";
    private const string ObligationConclusionMigrationId = "20260908222252_AddObligationConclusions";
    private const string InternalNoticesMigrationId = "20260909190648_AddInternalNotices";
    private const string ValidationPoliciesMigrationId = "20260910191232_AddValidationPolicies";
    private const string ValidationDecisionsMigrationId = "20260910210908_AddValidationDecisions";
    private const string IdempotencyReplayMigrationId = "20260912120000_ExtendIdempotencyReplay";
    private const string PortableDataProtectionMigrationId = "20260912213000_AddPortableDataProtectionKeyRing";
    private const string RecoveryReconciliationMigrationId = "20260914210503_AddRecoveryReconciliation";
    private const string HostedAuthenticationMigrationId = "20260918001719_AddHostedAuthentication";
    private readonly PostgreSqlContainer _postgres = CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Migrations_CreateOnlyTheApprovedTables()
    {
        await using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();

        await context.Database.MigrateAsync();
        await context.Database.MigrateAsync();
        await context.Database.OpenConnectionAsync();

        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal(
            [
                InitialMigrationId,
                AuditMigrationId,
                BootstrapMigrationId,
                BranchScopeMigrationId,
                PersonAdministrationMigrationId,
                RoleAdministrationMigrationId,
                AvailabilityAdministrationMigrationId,
                ConfigurationReleaseMigrationId,
                CalendarAdministrationMigrationId,
                WeekPeriodsMigrationId,
                TaskDefinitionsMigrationId,
                EligibilityPoliciesMigrationId,
                ActivationPoliciesMigrationId,
                GenerationRequestsMigrationId,
                WorkObligationsMigrationId,
                EligibilityEvaluationsMigrationId,
                AssignmentVersionsMigrationId,
                WorkPlansMigrationId,
                PlanPublicationsMigrationId,
                JobInfrastructureMigrationId,
                RecurringGenerationMigrationId,
                EvidencePoliciesMigrationId,
                EvidenceContributionMigrationId,
                StructuredEvidenceMigrationId,
                EvidenceReviewMigrationId,
                ObligationConclusionMigrationId,
                InternalNoticesMigrationId,
                ValidationPoliciesMigrationId,
                ValidationDecisionsMigrationId,
                IdempotencyReplayMigrationId,
                PortableDataProtectionMigrationId,
                RecoveryReconciliationMigrationId,
                HostedAuthenticationMigrationId,
            ],
            appliedMigrations);

        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            "SELECT table_name FROM information_schema.tables " +
            "WHERE table_schema = 'public' ORDER BY table_name";

        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        Assert.Equal(
            [
                "__EFMigrationsHistory",
                "activation_rule_version",
                "app_user",
                "assignment_version",
                "audit_event",
                "authentication_challenge",
                "availability_day_version",
                "branch",
                "calendar_day_version",
                "configuration_release",
                "data_protection_key",
                "direction_bootstrap",
                "eligibility_candidate",
                "eligibility_evaluation",
                "eligibility_policy_version",
                "employment_version",
                "evidence_item",
                "evidence_policy_version",
                "evidence_requirement_catalog",
                "evidence_requirement_version",
                "evidence_review_snapshot",
                "evidence_version",
                "execution_result",
                "file_object",
                "generation_request",
                "idempotency_record",
                "identity_credential",
                "internal_notice",
                "mfa_recovery_code",
                "mfa_totp_credential",
                "outbox_event",
                "person",
                "plan_version",
                "plan_version_obligation",
                "recovery_reconciliation",
                "recovery_reconciliation_difference",
                "recovery_reconciliation_event",
                "role_assignment_version",
                "scheduled_job_run",
                "task_definition",
                "task_definition_version",
                "validation_decision_version",
                "validation_policy_version",
                "validation_requirement",
                "week_period",
                "work_obligation",
                "work_plan",
            ],
            tables);

        await reader.DisposeAsync();
        command.CommandText = """
            SELECT conname
            FROM pg_constraint
            WHERE conname IN (
                'CK_app_user_access_failed_count',
                'CK_app_user_authentication_row_version',
                'CK_app_user_lockout_level',
                'CK_authentication_challenge_failed_attempt_count',
                'CK_authentication_challenge_interval',
                'CK_authentication_challenge_purpose',
                'CK_authentication_challenge_row_version',
                'CK_authentication_challenge_status',
                'CK_mfa_recovery_code_row_version',
                'CK_mfa_totp_credential_protection_version',
                'CK_mfa_totp_credential_row_version')
            ORDER BY conname
            """;
        var authenticationConstraints = new List<string>();
        await using (var constraintReader = await command.ExecuteReaderAsync())
        {
            while (await constraintReader.ReadAsync())
            {
                authenticationConstraints.Add(constraintReader.GetString(0));
            }
        }
        Assert.Equal(11, authenticationConstraints.Count);

        command.CommandText = """
            SELECT indexdef
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND indexname = 'IX_mfa_totp_credential_user_id'
            """;
        var activeTotpIndex = (string?)await command.ExecuteScalarAsync();
        Assert.NotNull(activeTotpIndex);
        Assert.Contains("UNIQUE", activeTotpIndex, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("revoked_at IS NULL", activeTotpIndex, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PortableDataProtectionKeyRingIsEncryptedAndReadableAfterRedeploy()
    {
        await using (var context = CreateContext())
        {
            await context.Database.EnsureDeletedAsync();
            await context.Database.MigrateAsync();
        }

        const string certificatePassword = "synthetic-certificate-password";
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=SGOL Synthetic Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Sgol"] = _postgres.GetConnectionString(),
            ["DataProtection:ApplicationName"] = "SGOL-TECH-OPS-TEST",
            ["DataProtection:WrappingCertificate"] = Convert.ToBase64String(
                certificate.Export(X509ContentType.Pfx, certificatePassword)),
            ["DataProtection:WrappingCertificatePassword"] = certificatePassword
        }).Build();

        string protectedValue;
        await using (var first = Services(configuration))
        {
            protectedValue = first.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("synthetic-purpose")
                .Protect("synthetic-protected-value");
        }

        await using (var second = Services(configuration))
        {
            Assert.Equal(
                "synthetic-protected-value",
                second.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("synthetic-purpose")
                    .Unprotect(protectedValue));
        }

        using var wrongRsa = RSA.Create(2048);
        var wrongRequest = new CertificateRequest(
            "CN=SGOL Wrong Synthetic Test", wrongRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var wrongCertificate = wrongRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
        var wrongConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Sgol"] = _postgres.GetConnectionString(),
            ["DataProtection:ApplicationName"] = "SGOL-TECH-OPS-TEST",
            ["DataProtection:WrappingCertificate"] = Convert.ToBase64String(
                wrongCertificate.Export(X509ContentType.Pfx, certificatePassword)),
            ["DataProtection:WrappingCertificatePassword"] = certificatePassword
        }).Build();
        await using (var wrong = Services(wrongConfiguration))
        {
            Assert.Throws<CryptographicException>(() =>
                wrong.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("synthetic-purpose")
                    .Unprotect(protectedValue));
        }

        await using var verification = CreateContext();
        await verification.Database.OpenConnectionAsync();
        await using var command = verification.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT count(*), string_agg(xml, '') FROM data_protection_key";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetInt64(0) > 0);
        Assert.DoesNotContain("synthetic-protected-value", reader.GetString(1), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidationDecisionMigrationInstallsClosedChecksAndVersioningGuards()
    {
        await using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        await context.Database.MigrateAsync();

        var checks = await ScalarAsync<long>(context, """
            SELECT count(*)
            FROM pg_constraint
            WHERE contype = 'c'
              AND conname IN (
                'CK_validation_requirement_row_version',
                'CK_validation_requirement_state',
                'CK_validation_requirement_status',
                'CK_validation_decision_authority',
                'CK_validation_decision_foundation',
                'CK_validation_decision_reason',
                'CK_validation_decision_result',
                'CK_validation_decision_status',
                'CK_validation_decision_version')
            """);
        var triggers = await ScalarAsync<long>(context, """
            SELECT count(*)
            FROM pg_trigger
            WHERE NOT tgisinternal
              AND tgname IN (
                'trg_validation_requirement_guard',
                'trg_validation_decision_guard',
                'trg_validation_decision_consistent',
                'trg_validation_requirement_consistent',
                'trg_validation_decision_requirement_consistent')
            """);
        var currentIndex = await ScalarAsync<long>(context, """
            SELECT count(*)
            FROM pg_class index_relation
            JOIN pg_index index_metadata ON index_metadata.indexrelid = index_relation.oid
            JOIN pg_class table_relation ON table_relation.oid = index_metadata.indrelid
            JOIN pg_namespace schema_relation ON schema_relation.oid = table_relation.relnamespace
            WHERE schema_relation.nspname = 'public'
              AND table_relation.relname = 'validation_decision_version'
              AND index_relation.relname = 'UX_validation_decision_version_current_requirement'
              AND index_metadata.indisunique
              AND index_metadata.indpred IS NOT NULL
            """);

        Assert.Equal(9, checks);
        Assert.Equal(5, triggers);
        Assert.Equal(1, currentIndex);
    }

    [Fact]
    public async Task InternalNoticeMigrationInstallsClosedCatalogDeduplicationAndGuards()
    {
        await using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        await context.Database.MigrateAsync();

        var constraints = await ScalarAsync<long>(context, """
            SELECT count(*)
            FROM pg_constraint
            WHERE conrelid = 'internal_notice'::regclass
              AND conname IN (
                'CK_internal_notice_non_empty_ids',
                'CK_internal_notice_read_at',
                'CK_internal_notice_resource_type',
                'CK_internal_notice_type',
                'FK_internal_notice_app_user_recipient_user_id',
                'FK_internal_notice_assignment_version_resource_id',
                'PK_internal_notice')
            """);
        var triggers = await ScalarAsync<long>(context, """
            SELECT count(*)
            FROM pg_trigger
            WHERE NOT tgisinternal
              AND tgname IN (
                'tr_assignment_version_notice',
                'tr_internal_notice_no_delete',
                'tr_internal_notice_validate')
            """);
        var uniqueIndex = await ScalarAsync<long>(context, """
            SELECT count(*)
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND tablename = 'internal_notice'
              AND indexname = 'UX_internal_notice_producer'
              AND indexdef LIKE 'CREATE UNIQUE INDEX%'
            """);

        Assert.Equal(7, constraints);
        Assert.Equal(3, triggers);
        Assert.Equal(1, uniqueIndex);
    }

    [Fact]
    public async Task EvidenceMigrationInstallsImmutableHistoryAndCleanLinkGuards()
    {
        await using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        await context.Database.MigrateAsync();

        var triggerNames = new List<string>();
        await using (var command = context.Database.GetDbConnection().CreateCommand())
        {
            await context.Database.OpenConnectionAsync();
            command.CommandText = "SELECT tgname FROM pg_trigger WHERE NOT tgisinternal AND tgname IN ('trg_evidence_item_snapshot', 'trg_evidence_item_guard', 'trg_evidence_version_guard', 'trg_file_object_snapshot', 'trg_file_object_guard') ORDER BY tgname";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) triggerNames.Add(reader.GetString(0));
        }

        Assert.Contains("trg_evidence_item_snapshot", triggerNames);
        Assert.Contains("trg_evidence_item_guard", triggerNames);
        Assert.Contains("trg_evidence_version_guard", triggerNames);
        Assert.Contains("trg_file_object_snapshot", triggerNames);
        Assert.Contains("trg_file_object_guard", triggerNames);

        var indexes = new List<string>();
        await using (var command = context.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "SELECT indexname FROM pg_indexes WHERE schemaname = 'public' AND indexname IN ('UX_evidence_item_obligation_requirement','UX_evidence_version_current_item','UX_outbox_event_evidence_inspection_pending') ORDER BY indexname";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) indexes.Add(reader.GetString(0));
        }

        Assert.Equal(3, indexes.Count);

        await using (var command = context.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "SELECT i.indisunique FROM pg_class c JOIN pg_index i ON i.indexrelid = c.oid WHERE c.relname = 'IX_file_object_linked_evidence_item_id'";
            var isUnique = (bool?)await command.ExecuteScalarAsync();
            Assert.False(isUnique);
        }

        await using (var command = context.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "SELECT count(*), bool_and(confdeltype = 'r') FROM pg_constraint WHERE contype = 'f' AND conrelid::regclass::text IN ('file_object','evidence_item','evidence_version')";
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(13, reader.GetInt64(0));
            Assert.True(reader.GetBoolean(1));
        }

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task EvidenceReviewMigrationInstallsImmutableSnapshotAuthority()
    {
        await using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        await context.Database.MigrateAsync();
        await context.Database.OpenConnectionAsync();

        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT
              EXISTS (SELECT 1 FROM pg_trigger WHERE NOT tgisinternal AND tgname = 'evidence_review_snapshot_guard'),
              EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'sgol_evidence_review_snapshot_guard'),
              (SELECT count(*) FROM pg_indexes WHERE schemaname = 'public' AND indexname IN
                ('UX_evidence_review_snapshot_obligation_fingerprint','UX_evidence_review_snapshot_obligation_input')),
              (SELECT count(*) FROM pg_constraint WHERE contype = 'f'
                AND conrelid = 'evidence_review_snapshot'::regclass AND confdeltype = 'r'),
              (SELECT count(*) FROM pg_constraint WHERE contype = 'c'
                AND conrelid = 'evidence_review_snapshot'::regclass);
            """;

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
        Assert.Equal(2, reader.GetInt64(2));
        Assert.Equal(3, reader.GetInt64(3));
        Assert.Equal(7, reader.GetInt64(4));
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task AuditTransaction_CommitsCriticalWriteAndAuditEventTogether()
    {
        await using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var auditTransaction = scope.ServiceProvider.GetRequiredService<AuditTransaction>();
        await PrepareDatabaseAsync(context);

        var auditId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();
        var resourceId = Guid.CreateVersion7();
        var branchId = Guid.CreateVersion7();
        var correlationId = Guid.CreateVersion7();
        var probeId = Guid.CreateVersion7();
        using var beforeData = JsonDocument.Parse("""{"schemaVersion":1,"value":"before"}""");
        using var afterData = JsonDocument.Parse("""{"schemaVersion":1,"value":"after"}""");
        var auditEvent = new AuditEvent
        {
            Id = auditId,
            OccurredAt = DateTimeOffset.UtcNow,
            ActorUserId = actorId,
            ActorType = "USER",
            Action = "TECHNICAL_TEST_WRITE",
            ResourceType = "TECHNICAL_TEST_PROBE",
            ResourceId = resourceId,
            BranchId = branchId,
            CorrelationId = correlationId,
            RequestId = "technical-test-request",
            BeforeData = beforeData,
            AfterData = afterData,
            Reason = "Verify transactional audit core",
            Outcome = "SUCCESS",
            SourceIpHash = new string('a', 64),
        };

        await auditTransaction.ExecuteAsync(
            auditEvent,
            cancellationToken => context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO audit_write_probe (id, payload) VALUES ({probeId}, {"committed"})",
                cancellationToken));

        Assert.True(await ProbeExistsAsync(context, probeId));
        var stored = await context.AuditEvents.AsNoTracking().SingleAsync(item => item.Id == auditId);
        Assert.Equal(actorId, stored.ActorUserId);
        Assert.Equal(resourceId, stored.ResourceId);
        Assert.Equal(branchId, stored.BranchId);
        Assert.Equal(correlationId, stored.CorrelationId);
        Assert.Equal("after", stored.AfterData?.RootElement.GetProperty("value").GetString());
        Assert.Equal(new string('a', 64), stored.SourceIpHash);
    }

    [Fact]
    public async Task AuditTransaction_WhenCriticalWriteFails_RollsBackWithoutPartialEffects()
    {
        await using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var auditTransaction = scope.ServiceProvider.GetRequiredService<AuditTransaction>();
        await PrepareDatabaseAsync(context);

        var auditId = Guid.CreateVersion7();
        var probeId = Guid.CreateVersion7();

        await Assert.ThrowsAsync<InvalidOperationException>(() => auditTransaction.ExecuteAsync(
            CreateAuditEvent(auditId),
            async cancellationToken =>
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO audit_write_probe (id, payload) VALUES ({probeId}, {"rolled-back"})",
                    cancellationToken);
                throw new InvalidOperationException("Injected failure before audit insertion.");
            }));

        Assert.False(await ProbeExistsAsync(context, probeId));
        Assert.False(await context.AuditEvents.AsNoTracking().AnyAsync(item => item.Id == auditId));
    }

    [Fact]
    public async Task AuditTransaction_WhenAuditInsertFails_RollsBackCriticalWrite()
    {
        await using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var auditTransaction = scope.ServiceProvider.GetRequiredService<AuditTransaction>();
        await PrepareDatabaseAsync(context);

        var duplicateAuditId = Guid.CreateVersion7();
        var probeId = Guid.CreateVersion7();
        context.AuditEvents.Add(CreateAuditEvent(duplicateAuditId));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<DbUpdateException>(() => auditTransaction.ExecuteAsync(
            CreateAuditEvent(duplicateAuditId),
            cancellationToken => context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO audit_write_probe (id, payload) VALUES ({probeId}, {"rolled-back"})",
                cancellationToken)));

        Assert.False(await ProbeExistsAsync(context, probeId));
        Assert.Equal(
            1,
            await context.AuditEvents.AsNoTracking().CountAsync(item => item.Id == duplicateAuditId));
    }

    [Fact]
    public async Task AuditEvent_UpdateAndDeleteAreRejectedByPostgreSql_WithoutEffect()
    {
        await using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        await PrepareDatabaseAsync(context);

        var auditId = Guid.CreateVersion7();
        context.AuditEvents.Add(CreateAuditEvent(auditId));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var updateException = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE audit_event SET outcome = {"TAMPERED"} WHERE id = {auditId}"));
        Assert.Equal("55000", updateException.SqlState);

        var deleteException = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM audit_event WHERE id = {auditId}"));
        Assert.Equal("55000", deleteException.SqlState);

        var preserved = await context.AuditEvents.AsNoTracking().SingleAsync(item => item.Id == auditId);
        Assert.Equal("SUCCESS", preserved.Outcome);
    }

    [Fact]
    public async Task StructuredEvidenceMigrationEnforcesClosedPayloadAndExclusiveSource()
    {
        await using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();

        await context.Database.MigrateAsync();
        await context.Database.OpenConnectionAsync();

        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT
              sgol_evidence_text_valid('"REC-01"'::jsonb, 120, true),
              sgol_evidence_timestamp_valid('"2026-09-07T20:00:00Z"'::jsonb),
              sgol_evidence_exact_keys(
                '{"schemaVersion":1,"formCode":"F-ENT-001","formReference":"REC-01","completedAt":"2026-09-07T20:00:00Z","hasDifference":true,"hasDamage":false}'::jsonb,
                ARRAY['schemaVersion','formCode','formReference','completedAt','hasDifference','hasDamage']),
              sgol_evidence_structured_payload_valid(
                'F_ENT_001', 'FORMULARIO_REFERENCIADO',
                '{"schemaVersion":1,"formCode":"F-ENT-001","formReference":"REC-01","completedAt":"2026-09-07T20:00:00Z","hasDifference":true,"hasDamage":false}'::jsonb),
              sgol_evidence_structured_payload_valid(
                'F_ENT_001', 'FORMULARIO_REFERENCIADO',
                '{"schemaVersion":1,"formCode":"F-ENT-001","formReference":"REC-01","completedAt":"2026-09-07T20:00:00Z","hasDifference":true,"hasDamage":false,"extra":true}'::jsonb),
              (SELECT is_nullable = 'YES' FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'evidence_version' AND column_name = 'file_object_id'),
              EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_evidence_version_source'),
              (SELECT count(*) = 18 AND bool_and(sgol_evidence_structured_payload_valid(code, kind, payload))
                FROM (VALUES
                  ('CALCULO_AVANCE','REGISTRO_DIGITAL','{"schemaVersion":1,"expectedTarget":100,"actualSales":90,"sourceReference":"VEN-01"}'::jsonb),
                  ('ACCION_O_CONFORMIDAD','REGISTRO_DIGITAL','{"schemaVersion":1,"outcome":"CONFORMIDAD","actionDescription":null,"responsiblePersonId":null,"startsAt":null}'::jsonb),
                  ('LIBERACION','REGISTRO_DIGITAL','{"schemaVersion":1,"releasedAt":"2026-09-07T20:00:00Z","releaseReference":"LIB-01"}'::jsonb),
                  ('MERCANCIA','DATO_ESTRUCTURADO','{"schemaVersion":1,"merchandiseReference":"MER-01"}'::jsonb),
                  ('FECHA_HORA','DATO_ESTRUCTURADO','{"schemaVersion":1,"occurredAt":"2026-09-07T20:00:00Z"}'::jsonb),
                  ('RETORNO_EXHIBICION','REGISTRO_DIGITAL','{"schemaVersion":1,"returnedAt":"2026-09-07T20:00:00Z","returnReference":"RET-01"}'::jsonb),
                  ('SECUENCIA','REGISTRO_DIGITAL','{"schemaVersion":1,"sequenceSummary":"Secuencia sintetica"}'::jsonb),
                  ('DECISION','REGISTRO_DIGITAL','{"schemaVersion":1,"decisionSummary":"Decision sintetica","decidedAt":"2026-09-07T20:00:00Z"}'::jsonb),
                  ('FUNDAMENTO','DATO_ESTRUCTURADO','{"schemaVersion":1,"foundationSummary":"Fundamento sintetico"}'::jsonb),
                  ('AVISO_INTERNO','REGISTRO_DIGITAL','{"schemaVersion":1,"noticeReference":"AVI-01","notifiedAt":"2026-09-07T20:00:00Z"}'::jsonb),
                  ('EVALUACION','REGISTRO_DIGITAL','{"schemaVersion":1,"assessmentSummary":"Evaluacion sintetica","assessedAt":"2026-09-07T20:00:00Z"}'::jsonb),
                  ('REPARACION_O_CAMBIO','REGISTRO_DIGITAL','{"schemaVersion":1,"solutionType":"REPARACION","solutionReference":"SOL-01","completedAt":"2026-09-07T20:00:00Z"}'::jsonb),
                  ('ENTREGA','REGISTRO_DIGITAL','{"schemaVersion":1,"deliveryReference":"ENT-01","deliveredAt":"2026-09-07T20:00:00Z"}'::jsonb),
                  ('CHECKLIST_COMPLETO','CHECKLIST_ESTRUCTURADO','{"schemaVersion":1,"productCorrect":true,"zoneAndFamilyCorrect":true,"stableFormation":true,"labelsVisible":true,"alignmentConsistent":true,"occupancyJustified":true,"clean":true,"intact":true,"signageCorrect":true,"matchesPlanogramOrList":true}'::jsonb),
                  ('FORM_ADM_02','FORMULARIO_REFERENCIADO','{"schemaVersion":1,"formCode":"FORM-ADM-02","formReference":"ADM-01","completedAt":"2026-09-07T20:00:00Z"}'::jsonb),
                  ('F_ENT_001','FORMULARIO_REFERENCIADO','{"schemaVersion":1,"formCode":"F-ENT-001","formReference":"REC-01","completedAt":"2026-09-07T20:00:00Z","hasDifference":true,"hasDamage":false}'::jsonb),
                  ('ANOTACION_F_ENT_001','FORMULARIO_REFERENCIADO','{"schemaVersion":1,"formCode":"F-ENT-001","formReference":"REC-01","annotationReference":"ANO-01","recordedAt":"2026-09-07T20:00:00Z"}'::jsonb),
                  ('CONSTANCIA_AVISO_INTERNO','REGISTRO_DIGITAL','{"schemaVersion":1,"noticeReference":"AVI-01","notifiedAt":"2026-09-07T20:00:00Z"}'::jsonb)
                ) AS approved(code, kind, payload));
            """;

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
        Assert.True(reader.GetBoolean(3));
        Assert.False(reader.GetBoolean(4));
        Assert.True(reader.GetBoolean(5));
        Assert.True(reader.GetBoolean(6));
        Assert.True(reader.GetBoolean(7));
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("IntegrationTests");
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Sgol"] = _postgres.GetConnectionString(),
                    }));
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureServices(services =>
                    services.AddDataProtection().UseEphemeralDataProtectionProvider());
            });

    private static async Task PrepareDatabaseAsync(SgolDbContext context)
    {
        await context.Database.MigrateAsync();
        await context.Database.OpenConnectionAsync();
        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TEMP TABLE audit_write_probe
            (
                id uuid PRIMARY KEY,
                payload text NOT NULL
            )
            """);
    }

    private static AuditEvent CreateAuditEvent(Guid id) => new()
    {
        Id = id,
        OccurredAt = DateTimeOffset.UtcNow,
        ActorType = "SYSTEM",
        Action = "TECHNICAL_TEST_WRITE",
        ResourceType = "TECHNICAL_TEST_PROBE",
        CorrelationId = Guid.CreateVersion7(),
        Outcome = "SUCCESS",
    };

    private static async Task<bool> ProbeExistsAsync(SgolDbContext context, Guid id)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM audit_write_probe WHERE id = @id)";
        command.Parameters.Add(new NpgsqlParameter<Guid>("id", id));
        if (command.Connection?.State != System.Data.ConnectionState.Open)
        {
            await context.Database.OpenConnectionAsync();
        }

        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task<T> ScalarAsync<T>(SgolDbContext context, string sql)
    {
        if (context.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
            await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return (T)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Scalar query returned null."));
    }

    private SgolDbContext CreateContext() => new(
        new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), options => options.CommandTimeout(15))
            .Options);

    private static ServiceProvider Services(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSgolPersistence(configuration);
        return services.BuildServiceProvider();
    }

    internal static PostgreSqlContainer CreateContainerForTests()
    {
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

        return new PostgreSqlBuilder("postgres:18.6-alpine3.23")
            .WithDatabase("sgol_integration")
            .WithUsername("sgol_integration")
            .WithPassword(password)
            .Build();
    }
}
