namespace Sgol.Cv05Demo;

internal sealed record DemoScenario(string Id, string Name, string Coverage);

internal static class ScenarioCatalog
{
    public static IReadOnlyList<DemoScenario> All { get; } =
    [
        new("S01", "OperationalIndicatorsReconcileAuthorizedScope", "HU-029;CP-029-P"),
        new("S02", "OverdueAndSupersededNeverInflateCounts", "HU-029;CP-029-N"),
        new("S03", "IndicatorHierarchyCursorAndReadOnlySnapshot", "HU-029"),
        new("S04", "DirectionSeesEntireBranchAndUnassigned", "HU-032;CP-032-P"),
        new("S05", "DirectionRoleAndFiveIndicatorBoundary", "HU-032;CP-032-N"),
        new("S06", "AuditTraceReconstructsPersistedChain", "HU-033;CP-033-P"),
        new("S07", "AuditScopeCursorMinimizationAndNoReadEffect", "HU-033"),
        new("S08", "AuditDeletionIsRejectedAndAttributed", "HU-033;CP-033-N"),
        new("S09", "IdenticalMutationReplaysOriginalOutcome", "HU-034;CP-034-P"),
        new("S10", "ChangedPayloadConflictsWithoutDuplicate", "HU-034;CP-034-N"),
        new("S11", "UnauthorizedReplayAndStaleIfMatchDoNotReexecute", "HU-034"),
        new("S12", "ConcurrentIdempotentRequestsHaveOneWinner", "HU-034;F07-ADENDA-31"),
        new("S13", "RecoveryReferenceRestoreAndMatch", "HU-035;CP-035-P"),
        new("S14", "DirectionApprovesMatchedOnly", "HU-035"),
        new("S15", "MissingEvidenceProducesVisibleDifference", "HU-035;CP-035-N"),
        new("S16", "MissingOrAlteredAuditBlocksRecovery", "HU-035;CP-035-N"),
        new("S17", "RecoveryManifestCorruptionFailsClosed", "HU-035"),
        new("S18", "RecoveryAuthorizationAuditAndNoEffect", "HU-035"),
        new("S19", "RecoveryConcurrencyAndStageReplayStayUnique", "HU-035"),
        new("S20", "HostedAuthenticationFailuresHaveNoBusinessEffect", "TECH-AUTH-001"),
        new("S21", "SecondRunHasSameFunctionalFingerprint", "TECH-E2E-CV-05"),
        new("S22", "CleanupLeavesNoOwnedResource", "TECH-E2E-CV-05"),
    ];

    public static DemoScenario Require(string id) => All.Single(item => item.Id == id);
}
