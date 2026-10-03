using Atlas.Documents;
using Atlas.Persistence;
using Atlas.Verification.Application.Abstractions;
using Atlas.Verification.Infrastructure.Documents;
using Atlas.Verification.Infrastructure.Persistence;
using Atlas.Verification.Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Atlas.Verification.Infrastructure;

public static class InfrastructureLayerExtensions
{
    /// <summary>The connection string name the AppHost provides: the Verification service's own database.</summary>
    public const string DatabaseConnectionName = "verification-db";

    public static IHostApplicationBuilder AddInfrastructureLayer(this IHostApplicationBuilder builder)
    {
        // Aspire's EF Core integration: connection retries, a database health check and EF tracing.
        builder.AddSqlServerDbContext<VerificationDbContext>(DatabaseConnectionName);
        builder.AddDatabaseTelemetry();

        // Onboarding's document storage, read-only (shared building block: container, hash, integrity check).
        builder.AddDocumentStorage();

        builder.Services.AddOptions<ProviderOptions>(ProviderOptions.IdNow)
            .BindConfiguration($"Providers:{ProviderOptions.IdNow}")
            .ValidateOnStart();
        builder.Services.AddOptions<ProviderOptions>(ProviderOptions.WorldCheck)
            .BindConfiguration($"Providers:{ProviderOptions.WorldCheck}")
            .ValidateOnStart();
        builder.Services.AddProviderClients();

        builder.Services.AddScoped<IVerificationRepository, VerificationRepository>();
        builder.Services.AddSingleton<IDocumentReader, BlobDocumentReader>();

        return builder;
    }
}
