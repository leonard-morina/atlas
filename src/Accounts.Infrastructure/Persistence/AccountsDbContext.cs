using Atlas.Accounts.Domain.Openings;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Accounts.Infrastructure.Persistence;

/// <summary>The Accounts service's own database. No other service reads or writes it.</summary>
public sealed class AccountsDbContext(DbContextOptions<AccountsDbContext> options) : DbContext(options)
{
    public DbSet<AccountOpening> Openings => Set<AccountOpening>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccountsDbContext).Assembly);

        // MassTransit's outbox and inbox tables live in this database so they share its transactions.
        modelBuilder.AddTransactionalOutboxEntities();
    }
}
