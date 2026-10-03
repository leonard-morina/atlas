using Atlas.Documents;
using Atlas.Messaging;
using Atlas.Onboarding.Application.Persistence;
using Atlas.Onboarding.Infrastructure.Documents;
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

        // Blob storage for the submitted images (shared building block: container, hash, integrity check).
        builder.AddDocumentStorage();

        // RabbitMQ with the outbox in this service's database.
        builder.AddMessaging<OnboardingDbContext>();

        builder.Services.AddScoped<IOnboardingApplicationRepository, OnboardingApplicationRepository>();
        builder.Services.AddSingleton<IDocumentStore, BlobDocumentStore>();

        return builder;
    }
}
