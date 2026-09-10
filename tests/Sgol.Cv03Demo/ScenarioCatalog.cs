namespace Sgol.Cv03Demo;

internal sealed record DemoScenario(string Id, string Name, string Coverage, string Rule);

internal static class ScenarioCatalog
{
    public static IReadOnlyList<DemoScenario> All { get; } =
    [
        new("S01", "ResponsibleReadsOnlyAllowedWork", "HU-023; CA-023; CP-023-P", "RN-002,RN-025,RN-027"),
        new("S02", "ForeignTaskConvergesWithoutDisclosure", "HU-023; CA-023; CP-023-N", "DEC-044,DEC-055,DEC-056"),
        new("S03", "ObligationKeepsFrozenEvidencePolicy", "HU-024; CA-024; CP-024-N", "RN-006,RN-008,RN-017,DEC-021"),
        new("S04", "ContributesStructuredEvidenceThroughRealService", "HU-025,TECH-EVID-002; CA-025; CP-025-P", "RN-018,RN-023,RN-027"),
        new("S05", "CleanBinaryTravelsThroughPrivateInfrastructure", "HU-025,TECH-EVID-001; CA-025; CP-025-P", "Adendas-17-18"),
        new("S06", "InvalidOrInfectedBinaryNeverBecomesEvidence", "HU-025,TECH-EVID-001; CA-025; CP-025-N", "DENEGACION-CERRADA"),
        new("S07", "ReplacementPreservesCompleteHistory", "HU-025; CA-025; CP-025-P", "RN-018,RN-023,RN-027,DEC-067,DEC-068"),
        new("S08", "SubstitutedEvidenceDoesNotSatisfyRequirement", "HU-025,HU-026; CA-026; CP-026-N", "RN-017,RN-018,RN-019"),
        new("S09", "Tar0092DifferenceMakesPhotoApplicable", "TECH-EVID-002,HU-026; CA-026; CP-026-P", "F_ENT_001,DIFERENCIA_O_DANO"),
        new("S10", "Tar0092NoDifferenceMakesPhotoNotApplicable", "TECH-EVID-002,HU-026; CA-026; CP-026-P", "F_ENT_001,DIFERENCIA_O_DANO"),
        new("S11", "AllApplicableRequirementsEvaluateComplete", "HU-026; CA-026; CP-026-P", "RN-017,RN-018,RN-019,DEC-022"),
        new("S12", "MissingRequirementEvaluatesIncompleteExactly", "HU-026; CA-026; CP-026-N", "RN-017,RN-018,RN-019,DEC-022"),
        new("S13", "ConclusionWithMissingEvidenceHasNoEffect", "HU-022; CA-022; CP-022-N", "RN-016,RN-017,RN-018,RN-019"),
        new("S14", "CompleteConclusionPersistsExactSnapshotAndResult", "HU-022; CA-022; CP-022-P", "RN-016,RN-017,RN-018,RN-019"),
        new("S15", "ConclusionReplayReturnsSameOutcome", "HU-022; CA-022; CP-022-P", "IDEMPOTENCIA-HU-022"),
        new("S16", "ConcurrentConclusionsHaveSingleWinner", "HU-022; CA-022; CP-022-N", "SERIALIZACION-HU-022"),
        new("S17", "OwnInboxShowsFutureAvailableAndOverdueTasks", "HU-030; CA-030; CP-030-P", "RN-019,RN-025,RN-029"),
        new("S18", "OverdueFlagDoesNotChangeExecutionStatus", "HU-023,HU-030; CA-030; CP-030-P", "RN-019,RN-025"),
        new("S19", "AssignmentProducesOneInternalNotice", "HU-030; CA-030; CP-030-P", "RN-029"),
        new("S20", "NoticeReadIsIdempotentAndAtomicallyAudited", "HU-030; CA-030; CP-030-P", "RN-029"),
        new("S21", "QueriesCreateNoSnapshotsResultsAuditOrWrites", "HU-023,HU-026,HU-030; CA-023,CA-026,CA-030; CP-023-P,CP-026-P,CP-030-P", "READ-ONLY"),
        new("S22", "NoExternalNotificationIsProduced", "HU-030; CA-030; CP-030-N", "RN-029"),
        new("S23", "NoValidationSupervisionOrIndicatorsAreCreated", "HU-022,HU-025,HU-026,HU-030; CA-022,CA-025,CA-026,CA-030; CP-022-N,CP-025-N,CP-026-N,CP-030-N", "LIMITES-CV-03"),
        new("S24", "SecondCleanRunIsReproducibleWithoutResidue", "HU-022..HU-026,HU-030; CA-022..CA-026,CA-030; CP-022-P,CP-023-P,CP-024-P,CP-025-P,CP-026-P,CP-030-P", "UNICIDAD,IDEMPOTENCIA,LIMPIEZA"),
    ];

    public static DemoScenario Require(string id) => All.Single(item => item.Id == id);
}
