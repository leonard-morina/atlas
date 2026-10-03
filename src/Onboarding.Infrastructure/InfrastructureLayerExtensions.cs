using Atlas.Onboarding.Application.Persistence;
using Atlas.Onboarding.Infrastructure.Documents;
using Atlas.Onboarding.Infrastructure.Persistence;
using Atlas.Persistence;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Atlas.Onboarding.Infrastructure;

public static class InfrastructureLayerExtensions
{
    /// <summary>The connection string name the AppHost provides: the Onboarding service's own database.</summary>
    public const string DatabaseConnectionName = "onboarding-db";

    /// <summary>The connection string name the AppHost provides: blob storage for documents.</summary>
    public const string DocumentsConnectionName = "documents";

    public static IHostApplicationBuilder AddInfrastructureLayer(this IHostApplicationBuilder builder)
    {
        // Aspire's EF Core integration: connection retries, a database health check and EF tracing.
        builder.AddSqlServerDbContext<OnboardingDbContext>(DatabaseConnectionName);
        builder.AddDatabaseTelemetry();

        // Aspire's Blob Storage integration: retries, a storage health check and tracing.
        builder.AddAzureBlobServiceClient(DocumentsConnectionName);

        builder.Services.AddScoped<IOnboardingApplicationRepository, OnboardingApplicationRepository>();
        builder.Services.AddSingleton<IDocumentStore, BlobDocumentStore>();

        return builder;
    }

    /// <summary>
    /// Development only, like migrations: creates the documents container. In production storage is provisioned
    /// with the infrastructure, not by the service.
    /// </summary>
    public static async Task CreateDocumentContainerAsync(this IHost host, CancellationToken cancellationToken = default) =>
        await host.Services.GetRequiredService<BlobServiceClient>()
            .GetBlobContainerClient(BlobDocumentStore.ContainerName)
            .CreateIfNotExistsAsync(cancellationToken: cancellationToken);
}
