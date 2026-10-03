using Atlas.Onboarding.Domain.Applications;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Onboarding.Infrastructure.Persistence;

/// <summary>The Onboarding service's own database. No other service reads or writes it.</summary>
public sealed class OnboardingDbContext(DbContextOptions<OnboardingDbContext> options) : DbContext(options)
{
    public DbSet<OnboardingApplication> Applications => Set<OnboardingApplication>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OnboardingDbContext).Assembly);

        // MassTransit's outbox and inbox tables live in this database so they share its transactions.
        modelBuilder.AddTransactionalOutboxEntities();
    }
}
