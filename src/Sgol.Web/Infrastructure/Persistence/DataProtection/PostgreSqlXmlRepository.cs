using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Npgsql;

namespace Sgol.Web.Infrastructure.Persistence.DataProtection;

internal sealed class PostgreSqlXmlRepository(string connectionString) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(
            "SELECT xml FROM data_protection_key ORDER BY created_at, name", connection);
        using var reader = command.ExecuteReader();
        var elements = new List<XElement>();
        while (reader.Read())
        {
            elements.Add(XElement.Parse(reader.GetString(0), LoadOptions.PreserveWhitespace));
        }

        return elements;
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrWhiteSpace(friendlyName);
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(
            "INSERT INTO data_protection_key (name, xml, created_at) VALUES ($1, $2, CURRENT_TIMESTAMP)",
            connection);
        command.Parameters.AddWithValue(friendlyName);
        command.Parameters.AddWithValue(element.ToString(SaveOptions.DisableFormatting));
        command.ExecuteNonQuery();
    }
}
