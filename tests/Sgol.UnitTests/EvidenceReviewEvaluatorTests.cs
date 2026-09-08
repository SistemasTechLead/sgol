using System.Text.Json;
using Sgol.Evidence.Contracts;
using Xunit;

namespace Sgol.UnitTests;

public sealed class EvidenceReviewEvaluatorTests
{
    private const string Always = "SIEMPRE";
    private const string Conditional = "DIFERENCIA_O_DANO";

    [Fact]
    public void EveryApplicableRequirementSatisfiedIsCompleteAndDeterministic()
    {
        var input = Input("TAR-0018",
            Structured("CHECKLIST_COMPLETO", "CHECKLIST_ESTRUCTURADO", 1, Checklist(true)),
            File("FOTOGRAFIA_FINAL", "FOTOGRAFIA", 2),
            File("PLANOGRAMA_O_LISTA", "DOCUMENTO_REFERENCIADO", 3));

        var first = EvidenceReviewEvaluator.Evaluate(input);
        var second = EvidenceReviewEvaluator.Evaluate(input);

        Assert.Equal(EvidenceReviewResults.Complete, first.Result);
        Assert.Empty(first.MissingRequirements);
        Assert.Equal(first.CanonicalInput, second.CanonicalInput);
        Assert.Equal(first.InputFingerprint, second.InputFingerprint);
        Assert.Equal(first.EvidenceVersionIds.OrderBy(id => id.ToString("D"), StringComparer.Ordinal), first.EvidenceVersionIds);
    }

    [Fact]
    public void MissingRequirementsAreCompleteAndOrderedByFrozenPolicy()
    {
        var input = Input("TAR-0018",
            Missing("CHECKLIST_COMPLETO", "CHECKLIST_ESTRUCTURADO", 1),
            File("FOTOGRAFIA_FINAL", "FOTOGRAFIA", 2),
            Missing("PLANOGRAMA_O_LISTA", "DOCUMENTO_REFERENCIADO", 3));

        var review = EvidenceReviewEvaluator.Evaluate(input);

        Assert.Equal(EvidenceReviewResults.Incomplete, review.Result);
        Assert.Equal(["CHECKLIST_COMPLETO", "PLANOGRAMA_O_LISTA"],
            review.MissingRequirements.Select(requirement => requirement.RequirementCode));
        Assert.All(review.MissingRequirements,
            requirement => Assert.Equal(EvidenceReviewMissingReasons.CurrentEvidenceAbsent, requirement.MissingReason));
    }

    [Fact]
    public void InvalidChecklistIsPresentButDoesNotSatisfy()
    {
        var input = Input("TAR-0018",
            Structured("CHECKLIST_COMPLETO", "CHECKLIST_ESTRUCTURADO", 1, Checklist(false)),
            File("FOTOGRAFIA_FINAL", "FOTOGRAFIA", 2),
            File("PLANOGRAMA_O_LISTA", "DOCUMENTO_REFERENCIADO", 3));

        var review = EvidenceReviewEvaluator.Evaluate(input);

        var missing = Assert.Single(review.MissingRequirements);
        Assert.Equal("CHECKLIST_COMPLETO", missing.RequirementCode);
        Assert.Equal(EvidenceReviewMissingReasons.CurrentEvidenceDoesNotSatisfy, missing.MissingReason);
    }

    [Fact]
    public void CurrentFEnt001AloneResolvesConditionalApplicability()
    {
        var falseCondition = Input("TAR-0092",
            File("DOCUMENTO_RECEPCION", "DOCUMENTO_REFERENCIADO", 1),
            Structured("F_ENT_001", "FORMULARIO_REFERENCIADO", 2, Form(false, false)),
            Missing("FOTO_DIFERENCIA_DANO", "FOTOGRAFIA", 3, Conditional));
        var trueCondition = Input("TAR-0092",
            File("DOCUMENTO_RECEPCION", "DOCUMENTO_REFERENCIADO", 1),
            Structured("F_ENT_001", "FORMULARIO_REFERENCIADO", 2, Form(true, false)),
            Missing("FOTO_DIFERENCIA_DANO", "FOTOGRAFIA", 3, Conditional));

        var notApplicable = EvidenceReviewEvaluator.Evaluate(falseCondition);
        var applicable = EvidenceReviewEvaluator.Evaluate(trueCondition);

        Assert.Equal(EvidenceReviewResults.Complete, notApplicable.Result);
        Assert.Equal(EvidenceReviewApplicability.NotApplicable, notApplicable.Requirements[2].Applicability);
        Assert.Equal(EvidenceReviewResults.Incomplete, applicable.Result);
        Assert.Equal("FOTO_DIFERENCIA_DANO", Assert.Single(applicable.MissingRequirements).RequirementCode);
    }

    [Fact]
    public void MissingFEnt001LeavesConditionUnresolvedAndFailsClosed()
    {
        var review = EvidenceReviewEvaluator.Evaluate(Input("TAR-0092",
            File("DOCUMENTO_RECEPCION", "DOCUMENTO_REFERENCIADO", 1),
            Missing("F_ENT_001", "FORMULARIO_REFERENCIADO", 2),
            Missing("FOTO_DIFERENCIA_DANO", "FOTOGRAFIA", 3, Conditional)));

        Assert.Equal(EvidenceReviewResults.Incomplete, review.Result);
        Assert.Equal(EvidenceReviewApplicability.Unresolved, review.Requirements[2].Applicability);
        Assert.Equal("F_ENT_001", Assert.Single(review.MissingRequirements).RequirementCode);
    }

    [Fact]
    public void SupersededVersionAloneDoesNotSatisfyAndReplacementChangesFingerprint()
    {
        var superseded = Structured("CHECKLIST_COMPLETO", "CHECKLIST_ESTRUCTURADO", 1, Checklist(true),
            EvidenceVersionStatuses.Superseded);
        var missingReview = EvidenceReviewEvaluator.Evaluate(Input("TAR-0018", superseded,
            File("FOTOGRAFIA_FINAL", "FOTOGRAFIA", 2), File("PLANOGRAMA_O_LISTA", "DOCUMENTO_REFERENCIADO", 3)));
        var current = superseded with
        {
            Versions = [superseded.Versions[0] with { EvidenceVersionId = Guid.CreateVersion7(), Status = EvidenceVersionStatuses.Current }],
        };
        var currentReview = EvidenceReviewEvaluator.Evaluate(Input("TAR-0018", current,
            File("FOTOGRAFIA_FINAL", "FOTOGRAFIA", 2), File("PLANOGRAMA_O_LISTA", "DOCUMENTO_REFERENCIADO", 3)));

        Assert.Equal(EvidenceReviewResults.Incomplete, missingReview.Result);
        Assert.Equal(EvidenceReviewResults.Complete, currentReview.Result);
        Assert.NotEqual(missingReview.InputFingerprint, currentReview.InputFingerprint);
    }

    [Theory]
    [InlineData(10, 100, "ACCION", true)]
    [InlineData(95, 100, "CONFORMIDAD", true)]
    [InlineData(10, 100, "CONFORMIDAD", false)]
    public void ActionOutcomeMustMatchCurrentCalculation(int actual, int expected, string outcome, bool satisfied)
    {
        var review = EvidenceReviewEvaluator.Evaluate(Input("TAR-0005",
            Structured("CALCULO_AVANCE", "REGISTRO_DIGITAL", 1, Calculation(actual, expected)),
            Structured("ACCION_O_CONFORMIDAD", "REGISTRO_DIGITAL", 2, Action(outcome))));

        Assert.Equal(satisfied, review.Requirements[1].Satisfied);
    }

    [Fact]
    public void InconsistentFrozenPolicyFailsClosed()
    {
        var valid = Input("TAR-0018", File("FOTOGRAFIA_FINAL", "FOTOGRAFIA", 2));
        var input = valid with { ExpectedRequirements = [] };

        Assert.Throws<EvidenceReviewUnavailableException>(() => EvidenceReviewEvaluator.Evaluate(input));
    }

    [Fact]
    public void CanonicalFingerprintMatchesKnownUtf8Vector()
    {
        var requirement = new EvidenceReviewRequirementInput(
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            "FOTOGRAFIA_FINAL", "FOTOGRAFIA", Always, 1, true, null, []);
        var input = new EvidenceReviewEvaluationInput(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            "TAR-0018",
            [new("FOTOGRAFIA_FINAL", "FOTOGRAFIA", Always, 1)],
            [requirement]);
        const string canonical = """{"schemaVersion":1,"obligationId":"00000000-0000-0000-0000-000000000001","evidencePolicyVersionId":"00000000-0000-0000-0000-000000000002","taskCode":"TAR-0018","requirements":[{"requirementVersionId":"00000000-0000-0000-0000-000000000003","requirementCode":"FOTOGRAFIA_FINAL","kind":"FOTOGRAFIA","conditionCode":"SIEMPRE","ordinal":1,"applicability":"APLICABLE","satisfied":false,"evidenceItemId":null,"evidenceVersionId":null,"conditionEvidenceVersionId":null,"missingReason":"EVIDENCIA_VIGENTE_AUSENTE"}]}""";

        var review = EvidenceReviewEvaluator.Evaluate(input);

        Assert.Equal(canonical, review.CanonicalInput);
        Assert.Equal("eaa9b9c97d980ed0ca0a5c993f5137922e9adc1b5434cc090299105ee9cb77ef",
            review.InputFingerprint);
    }

    private static EvidenceReviewEvaluationInput Input(string taskCode, params EvidenceReviewRequirementInput[] requirements)
    {
        var ordered = requirements.OrderBy(requirement => requirement.Ordinal).ToArray();
        return new(Guid.CreateVersion7(), Guid.CreateVersion7(), taskCode,
            ordered.Select(requirement => new EvidenceReviewExpectedRequirement(
                requirement.RequirementCode, requirement.Kind, requirement.ConditionCode, requirement.Ordinal)).ToArray(),
            requirements);
    }

    private static EvidenceReviewRequirementInput Missing(string code, string kind, short ordinal, string condition = Always) =>
        new(Guid.CreateVersion7(), code, kind, condition, ordinal, true, null, []);

    private static EvidenceReviewRequirementInput File(string code, string kind, short ordinal, string condition = Always)
    {
        var requirementId = Guid.CreateVersion7();
        var itemId = Guid.CreateVersion7();
        var fileId = Guid.CreateVersion7();
        return new(requirementId, code, kind, condition, ordinal, true, itemId,
            [new(Guid.CreateVersion7(), EvidenceVersionStatuses.Current, fileId, null,
                new(fileId, requirementId, itemId, EvidenceFileStatuses.Clean, EvidenceBucketClasses.Clean))]);
    }

    private static EvidenceReviewRequirementInput Structured(
        string code,
        string kind,
        short ordinal,
        string payload,
        string status = EvidenceVersionStatuses.Current)
    {
        var itemId = Guid.CreateVersion7();
        return new(Guid.CreateVersion7(), code, kind, Always, ordinal, true, itemId,
            [new(Guid.CreateVersion7(), status, null, JsonDocument.Parse(payload), null)]);
    }

    private static string Checklist(bool finalValue) =>
        $$"""{"schemaVersion":1,"productCorrect":true,"zoneAndFamilyCorrect":true,"stableFormation":true,"labelsVisible":true,"alignmentConsistent":true,"occupancyJustified":true,"clean":true,"intact":true,"signageCorrect":true,"matchesPlanogramOrList":{{finalValue.ToString().ToLowerInvariant()}}}""";

    private static string Form(bool difference, bool damage) =>
        $$"""{"schemaVersion":1,"formCode":"F-ENT-001","formReference":"FENT-01","completedAt":"2026-09-08T18:00:00Z","hasDifference":{{difference.ToString().ToLowerInvariant()}},"hasDamage":{{damage.ToString().ToLowerInvariant()}}}""";

    private static string Calculation(int actual, int expected) =>
        $$"""{"schemaVersion":1,"expectedTarget":{{expected}},"actualSales":{{actual}},"sourceReference":"CALC-01"}""";

    private static string Action(string outcome) => outcome == "CONFORMIDAD"
        ? """{"schemaVersion":1,"outcome":"CONFORMIDAD","actionDescription":null,"responsiblePersonId":null,"startsAt":null}"""
        : $$"""{"schemaVersion":1,"outcome":"ACCION","actionDescription":"Corregir avance","responsiblePersonId":"{{Guid.CreateVersion7():D}}","startsAt":"2026-09-08T19:00:00Z"}""";
}
