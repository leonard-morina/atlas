using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Atlas.Verification.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build the model without starting the service or Aspire. Adding a migration does not connect to
/// the database, so a placeholder connection string does. Removing one checks whether it was already applied, so it
/// needs a real one, passed after <c>--</c> (the Makefile passes the local development database's).
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VerificationDbContext>
{
    public VerificationDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<VerificationDbContext>()
            .UseSqlServer(args.FirstOrDefault() ?? "Server=localhost;Database=atlas_verification")
            .Options);
}
