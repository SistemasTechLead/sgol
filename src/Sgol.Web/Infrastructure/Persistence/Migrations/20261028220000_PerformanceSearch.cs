using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Sgol.Web.Infrastructure.Persistence.Migrations;
[DbContext(typeof(SgolDbContext))]
[Migration("20261028220000_PerformanceSearch")]
public sealed class PerformanceSearch : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE OR REPLACE FUNCTION sgol_search(value text) RETURNS text
        LANGUAGE sql IMMUTABLE PARALLEL SAFE AS $f$
        SELECT lower(regexp_replace(normalize(coalesce(value, ''), NFD), U&'[\0300-\036f]', '', 'g'))
        $f$;
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("DROP FUNCTION IF EXISTS sgol_search(text)");
}
