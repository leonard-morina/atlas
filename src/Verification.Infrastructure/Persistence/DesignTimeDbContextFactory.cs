using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Atlas.Verification.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without starting the worker or Aspire. Creating a migration
/// does not connect to the database, so the connection string here is only a placeholder.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VerificationDbContext>
{
    public VerificationDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<VerificationDbContext>()
            .UseSqlServer("Server=localhost;Database=atlas_verification")
            .Options);
}
