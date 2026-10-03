using Atlas.Onboarding.Application.Persistence;
using Atlas.Onboarding.Infrastructure.Persistence;
using Atlas.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Atlas.Onboarding.Infrastructure;

public static class InfrastructureLayerExtensions
{
    /// <summary>The connection string name the AppHost provides: the Onboarding service's own database.</summary>
    public const string DatabaseConnectionName = "onboarding-db";

    public static IHostApplicationBuilder AddInfrastructureLayer(this IHostApplicationBuilder builder)
    {
        // Aspire's EF Core integration: connection retries, a database health check and EF tracing.
        builder.AddSqlServerDbContext<OnboardingDbContext>(DatabaseConnectionName);
        builder.AddDatabaseTelemetry();

        builder.Services.AddScoped<IOnboardingApplicationRepository, OnboardingApplicationRepository>();

        return builder;
    }
}
