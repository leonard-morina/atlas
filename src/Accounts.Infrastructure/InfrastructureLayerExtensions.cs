using Atlas.Accounts.Application.Abstractions;
using Atlas.Accounts.Infrastructure.CoreBanking;
using Atlas.Accounts.Infrastructure.Persistence;
using Atlas.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Atlas.Accounts.Infrastructure;

public static class InfrastructureLayerExtensions
{
    /// <summary>The connection string name the AppHost provides: the Accounts service's own database.</summary>
    public const string DatabaseConnectionName = "accounts-db";

    public static IHostApplicationBuilder AddInfrastructureLayer(this IHostApplicationBuilder builder)
    {
        // Aspire's EF Core integration: connection retries, a database health check and EF tracing.
        builder.AddSqlServerDbContext<AccountsDbContext>(DatabaseConnectionName);
        builder.AddDatabaseTelemetry();

        builder.Services.AddOptions<CoreBankingOptions>()
            .BindConfiguration(CoreBankingOptions.Section)
            .ValidateOnStart();
        builder.Services.AddCoreBankingClient();

        builder.Services.AddScoped<IAccountOpeningRepository, AccountOpeningRepository>();

        return builder;
    }
}
