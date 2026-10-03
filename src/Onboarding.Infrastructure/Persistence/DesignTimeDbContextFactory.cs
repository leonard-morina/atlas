using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Atlas.Onboarding.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without starting the API or Aspire. Creating a migration
/// does not connect to the database, so the connection string here is only a placeholder.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OnboardingDbContext>
{
    public OnboardingDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<OnboardingDbContext>()
            .UseSqlServer("Server=localhost;Database=atlas_onboarding")
            .Options);
}
