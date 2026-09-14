using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SgolDbContext))]
[Migration("20260912213000_AddPortableDataProtectionKeyRing")]
public sealed class AddPortableDataProtectionKeyRing : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "data_protection_key",
            columns: table => new
            {
                name = table.Column<string>(type: "text", nullable: false),
                xml = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_data_protection_key", item => item.name));

        migrationBuilder.AddCheckConstraint(
            name: "CK_data_protection_key_name",
            table: "data_protection_key",
            sql: "length(name) BETWEEN 1 AND 256");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Portable Data Protection key-ring persistence is forward-only.");
}
