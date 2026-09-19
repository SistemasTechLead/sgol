using Sgol.Operations;
using Xunit;

namespace Sgol.OperationsIntegrationTests;

[Trait("Category", "Hu035External")]
public sealed class FunctionalRecoveryExternalTests
{
    [Fact]
    public async Task CompletedReferenceAndReconciliationReplayWithoutNewEffects()
    {
        Assert.Equal("true", Required("SGOL_SYNTHETIC_ONLY"));
        var id = Required("SGOL_HU035_RECONCILIATION_ID");
        var reference = Required("SGOL_HU035_REFERENCE_MANIFEST_URI");
        var backup = Required("SGOL_HU035_BACKUP_MANIFEST_URI");
        var replica = Required("SGOL_HU035_REPLICA_MANIFEST_URI");
        var evidence = Required("SGOL_HU035_RESTORE_EVIDENCE_PATH");

        var complete = new[] { "complete-functional-reference", "--reconciliation-id", id,
            "--reference", reference, "--backup-manifest", backup, "--replica-manifest", replica };
        Assert.Equal(0, await OperationsProgram.RunAsync(complete));
        Assert.Equal(0, await OperationsProgram.RunAsync(complete));

        var reconcile = new[] { "reconcile-functional-restore", "--reconciliation-id", id,
            "--reference-manifest", reference, "--restore-evidence", evidence };
        Assert.Equal(0, await OperationsProgram.RunAsync(reconcile));
        Assert.Equal(0, await OperationsProgram.RunAsync(reconcile));
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"Missing HU-035 synthetic setting: {name}");
}
