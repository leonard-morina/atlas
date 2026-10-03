using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Persistence;

public static class MigrationExtensions
{
    /// <summary>
    /// Development only: a service brings its own database up to date on startup. In production each service
    /// ships an EF migration bundle that the deployment runs once, before its replicas start, so replicas never
    /// race to migrate the same database.
    /// </summary>
    public static async Task MigrateDatabaseAsync<TContext>(this IHost host, CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        await using var scope = host.Services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TContext>>();

        logger.LogInformation("Applying migrations for {DbContext}", typeof(TContext).Name);
        await scope.ServiceProvider.GetRequiredService<TContext>().Database.MigrateAsync(cancellationToken);
    }
}
