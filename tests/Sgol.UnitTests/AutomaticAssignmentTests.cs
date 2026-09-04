using Sgol.Assignment.Contracts;
using Xunit;

namespace Sgol.UnitTests;

public sealed class AutomaticAssignmentTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 4, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LowerActiveLoadWinsIncludingZeroAgainstPositiveLoad()
    {
        var zero = Candidate("EMP-020", 0, Now.AddHours(-1));
        var positive = Candidate("EMP-010", 1, null);

        var result = AutomaticAssignmentRanker.Rank([positive, zero]);

        Assert.Equal(zero.PersonId, result.Winner.PersonId);
        Assert.Equal(AutomaticAssignmentDecisiveRules.ActiveLoad, result.DecisiveRule);
    }

    [Fact]
    public void NeverAutomaticallyAssignedWinsAnActiveLoadTie()
    {
        var neverAssigned = Candidate("EMP-020", 2, null);
        var assigned = Candidate("EMP-010", 2, Now.AddDays(-30));

        var result = AutomaticAssignmentRanker.Rank([assigned, neverAssigned]);

        Assert.Equal(neverAssigned.PersonId, result.Winner.PersonId);
        Assert.Equal(AutomaticAssignmentDecisiveRules.LastAutoAssignmentAt, result.DecisiveRule);
    }

    [Fact]
    public void OldestAutomaticAssignmentWinsAnActiveLoadTie()
    {
        var oldest = Candidate("EMP-020", 2, Now.AddDays(-10));
        var newest = Candidate("EMP-010", 2, Now.AddDays(-2));

        var result = AutomaticAssignmentRanker.Rank([newest, oldest]);

        Assert.Equal(oldest.PersonId, result.Winner.PersonId);
        Assert.Equal(AutomaticAssignmentDecisiveRules.LastAutoAssignmentAt, result.DecisiveRule);
    }

    [Fact]
    public void StableCodeOrdinallyBreaksTheRemainingTieAndIsDeterministic()
    {
        var firstCode = Candidate("EMP-002", 1, Now.AddDays(-1));
        var secondCode = Candidate("EMP-010", 1, Now.AddDays(-1));

        var forward = AutomaticAssignmentRanker.Rank([secondCode, firstCode]);
        var reverse = AutomaticAssignmentRanker.Rank([firstCode, secondCode]);

        Assert.Equal(firstCode.PersonId, forward.Winner.PersonId);
        Assert.Equal(firstCode.PersonId, reverse.Winner.PersonId);
        Assert.Equal(AutomaticAssignmentDecisiveRules.StableCode, forward.DecisiveRule);
        Assert.Equal(
            forward.Candidates.Select(candidate => candidate.PersonId),
            reverse.Candidates.Select(candidate => candidate.PersonId));
    }

    [Fact]
    public void ASingleEligibleCandidateIsAssignedDirectly()
    {
        var only = Candidate("EMP-001", 7, Now.AddMinutes(-1));

        var result = AutomaticAssignmentRanker.Rank([only]);

        Assert.Equal(only.PersonId, result.Winner.PersonId);
        Assert.Equal(1, result.Winner.Rank);
        Assert.Equal(AutomaticAssignmentDecisiveRules.OnlyEligibleCandidate, result.DecisiveRule);
    }

    [Fact]
    public void EmptyDuplicateOrInvalidCandidatesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => AutomaticAssignmentRanker.Rank([]));
        var candidate = Candidate("EMP-001", 0, null);
        Assert.Throws<ArgumentException>(() => AutomaticAssignmentRanker.Rank([candidate, candidate]));
        Assert.Throws<ArgumentException>(() => AutomaticAssignmentRanker.Rank([
            candidate with { PersonId = Guid.CreateVersion7(), ActiveLoad = -1 },
        ]));
    }

    private static AutomaticAssignmentCandidate Candidate(
        string stableCode,
        int activeLoad,
        DateTimeOffset? lastAutoAssignmentAt) =>
        new(Guid.CreateVersion7(), stableCode, activeLoad, lastAutoAssignmentAt);
}
