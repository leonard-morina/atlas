using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Atlas.Persistence;

public static class DatabaseTelemetryExtensions
{
    /// <summary>
    /// EF Core's metrics (queries, SaveChanges, active DbContexts, compiled query cache hits and misses) next to
    /// the SQL tracing the Aspire EF integration already provides. Exported with the rest of the service's
    /// metrics; a climbing cache-miss count or DbContext count shows a problem before users notice it.
    /// </summary>
    public static IHostApplicationBuilder AddDatabaseTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics.AddMeter("Microsoft.EntityFrameworkCore"));

        return builder;
    }
}
