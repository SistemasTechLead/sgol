namespace Sgol.Organization.Contracts;

public static class BranchScope
{
    public static readonly Guid LorettaId = Guid.Parse("019d2d67-2c00-7000-8000-000000000001");

    public const string LorettaCode = "LOR-001";
    public const string LorettaName = "Loretta";
    public const string ActiveStatus = "ACTIVA";
    public const string GlobalQueryScope = "TODAS";
    public const string TimeZone = "America/Mexico_City";

    public static string RequireLoretta(string branchCode)
    {
        if (!string.Equals(branchCode, LorettaCode, StringComparison.Ordinal))
        {
            throw new UnsupportedBranchCodeException(branchCode);
        }

        return LorettaCode;
    }

    public static string ResolveQueryScope(string scope)
    {
        if (string.Equals(scope, GlobalQueryScope, StringComparison.Ordinal) ||
            string.Equals(scope, LorettaCode, StringComparison.Ordinal))
        {
            return LorettaCode;
        }

        throw new UnsupportedBranchCodeException(scope);
    }
}

public sealed class UnsupportedBranchCodeException : ArgumentException
{
    public UnsupportedBranchCodeException(string? branchCode)
        : base("Only the canonical Loretta branch code is supported in the MVP.", nameof(branchCode))
    {
    }
}
