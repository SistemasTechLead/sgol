using Microsoft.EntityFrameworkCore;

namespace Sgol.Web.Infrastructure.Persistence;

public sealed class SgolDbContext(DbContextOptions<SgolDbContext> options) : DbContext(options);
