namespace Sgol.Cv04Demo;

internal sealed record DemoScenario(string Id, string Name, string Coverage);

internal static class ScenarioCatalog
{
    public static IReadOnlyList<DemoScenario> All { get; } =
    [
        new("S01", "EightTarPoliciesResolveCanonicalImmediateSuperior", "HU-027;CA-027;RN-020"),
        new("S02", "TextualPositionPeerAndLowerRoleGrantNoAuthority", "HU-027;CA-027;CP-027-N;RN-003"),
        new("S03", "OrdinarySuperiorIssuesFulfilledDecision", "HU-028;CA-028;RN-020..RN-022"),
        new("S04", "OrdinarySuperiorIssuesIncompleteDecision", "HU-028;CA-028;RN-022"),
        new("S05", "OrdinarySuperiorIssuesNotFulfilledDecision", "HU-028;CA-028;CPE-002;RN-022;RN-024"),
        new("S06", "MotivatedReplacementPreservesBothVersions", "HU-028;CP-028-P;RN-023;RN-027"),
        new("S07", "SecondDirectDecisionHasNoEffect", "HU-028;CP-028-N"),
        new("S08", "UnknownResultHasNoEffect", "HU-028;CP-028-N"),
        new("S09", "NonDirectionSelfValidationHasNoEffect", "HU-028;CP-028-N;RN-021"),
        new("S10", "DirectionSelfValidationIsExceptionalAndAudited", "HU-028;RN-021;NFR-004"),
        new("S11", "AdministrationSeesOnlySubcoordinationAndFloor", "HU-031;CA-031;CP-031-P/N;RN-025"),
        new("S12", "SubcoordinationSeesOnlyFloor", "HU-031;CA-031;RN-025"),
        new("S13", "FloorObtainsNoSupervision", "HU-031;CP-031-N"),
        new("S14", "FiltersNeverExpandHierarchicalScope", "HU-031;RN-025;NFR-002"),
        new("S15", "DetailEvidenceDecisionAndHistoryConvergeAntiIdor", "HU-028;HU-031;RN-027"),
        new("S16", "PendingValidationsAreExactAndReadOnly", "HU-031;CA-031;RN-020;RN-025"),
        new("S17", "AuditIsTransactionalAndRejectionsHaveNoFalseSuccess", "HU-027;HU-028;RN-028;NFR-004"),
        new("S18", "ConcurrentInitialDecisionsHaveOneWinner", "HU-028;RN-022;RN-027"),
        new("S19", "ConcurrentReplacementsHaveOneWinner", "HU-028;RN-023;RN-027"),
        new("S20", "MissingOrTamperedCookieBlocksWithoutEffect", "TECH-AUTH-001;ADR-012"),
        new("S21", "CsrfFailureBlocksEveryMutationWithoutEffect", "TECH-AUTH-001;ADR-012"),
        new("S22", "IncompleteMfaAndInvalidatedSessionBlockWithoutEffect", "TECH-AUTH-001;ADR-004"),
        new("S23", "SecondRunReproducesSameFunctionalFingerprint", "TECH-E2E-CV-04"),
        new("S24", "CleanupLeavesNoOwnedResource", "TECH-E2E-CV-04"),
    ];

    public static DemoScenario Require(string id) => All.Single(item => item.Id == id);
}
