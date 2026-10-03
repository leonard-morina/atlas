using Atlas.Verification.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Verification.Infrastructure.Persistence;

/// <summary>The Verification service's own database. No other service reads or writes it.</summary>
public sealed class VerificationDbContext(DbContextOptions<VerificationDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationVerification> Verifications => Set<ApplicationVerification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VerificationDbContext).Assembly);

        // MassTransit's outbox and inbox tables live in this database so they share its transactions.
        modelBuilder.AddTransactionalOutboxEntities();
    }
}
