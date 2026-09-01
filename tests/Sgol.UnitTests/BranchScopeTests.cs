using Sgol.Organization.Contracts;
using Xunit;

namespace Sgol.UnitTests;

public sealed class BranchScopeTests
{
    [Fact]
    public void LorettaCode_IsTheOnlyRecognizedBranch()
    {
        Assert.Equal(BranchScope.LorettaCode, BranchScope.RequireLoretta("LOR-001"));
    }

    [Fact]
    public void GlobalQueryScope_ReturnsLorettaWithoutBecomingABranch()
    {
        Assert.Equal(
            BranchScope.LorettaCode,
            BranchScope.ResolveQueryScope(BranchScope.GlobalQueryScope));
        Assert.Throws<UnsupportedBranchCodeException>(
            () => BranchScope.RequireLoretta(BranchScope.GlobalQueryScope));
    }

    [Theory]
    [InlineData("TODAS")]
    [InlineData("LOR-002")]
    [InlineData("lor-001")]
    [InlineData("")]
    public void AnyOtherCode_IsRejected(string branchCode)
    {
        Assert.Throws<UnsupportedBranchCodeException>(() => BranchScope.RequireLoretta(branchCode));
    }

    [Theory]
    [InlineData("LOR-002")]
    [InlineData("lor-001")]
    [InlineData("")]
    public void UnsupportedQueryScope_IsRejected(string branchCode)
    {
        Assert.Throws<UnsupportedBranchCodeException>(() => BranchScope.ResolveQueryScope(branchCode));
    }
}
