using System.Text.RegularExpressions;

namespace Sgol.Web.Infrastructure.Persistence.Versioning;

public static partial class VersioningPostgreSql
{
    public const string RequiredExtensionSql = "CREATE EXTENSION IF NOT EXISTS btree_gist";

    public static string AddNoOverlapConstraintSql(
        string tableName,
        params string[] objectAndScopeColumnNames)
    {
        ValidateIdentifier(tableName, nameof(tableName));
        if (objectAndScopeColumnNames.Length == 0)
        {
            throw new ArgumentException(
                "At least one object or scope column is required.",
                nameof(objectAndScopeColumnNames));
        }

        foreach (var columnName in objectAndScopeColumnNames)
        {
            ValidateIdentifier(columnName, nameof(objectAndScopeColumnNames));
        }

        var equalityTerms = string.Join(
            ", ",
            objectAndScopeColumnNames.Select(column => $"\"{column}\" WITH ="));
        return $"""
            ALTER TABLE "{tableName}"
            ADD CONSTRAINT "EX_{tableName}_validity"
            EXCLUDE USING gist (
                {equalityTerms},
                tstzrange("effective_from", "effective_to", '[)') WITH &&
            )
            WHERE ("status" IN ('VIGENTE', 'SUSTITUIDA'))
            """;
    }

    private static void ValidateIdentifier(string identifier, string parameterName)
    {
        if (identifier is null || !PostgreSqlIdentifierRegex().IsMatch(identifier))
        {
            throw new ArgumentException("A lowercase PostgreSQL identifier is required.", parameterName);
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9_]{0,47}$", RegexOptions.CultureInvariant)]
    private static partial Regex PostgreSqlIdentifierRegex();
}
