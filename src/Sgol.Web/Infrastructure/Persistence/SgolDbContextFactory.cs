using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sgol.Web.Infrastructure.Persistence;

public sealed class SgolDbContextFactory : IDesignTimeDbContextFactory<SgolDbContext>
{
    public SgolDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=sgol_design;Username=sgol_design",
                npgsql => npgsql.MigrationsAssembly(typeof(SgolDbContext).Assembly.FullName))
            .Options;

        return new SgolDbContext(options);
    }
}
