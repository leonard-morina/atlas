using Atlas.Accounts.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Atlas.Accounts.Infrastructure.CoreBanking;

internal static class CoreBankingClientExtensions
{
    public static IServiceCollection AddCoreBankingClient(this IServiceCollection services)
    {
        var client = services.AddHttpClient<ICoreBanking, CoreBankingClient>(http => http.Timeout = Timeout.InfiniteTimeSpan);

        client.ConfigureHttpClient((provider, http) =>
            http.BaseAddress = provider.GetRequiredService<IOptions<CoreBankingOptions>>().Value.BaseUrl);

        // No resilience handler at all, not even a timeout: the defaults ServiceDefaults gives every HttpClient retry,
        // and a retried OpenAccount opens a second account (CBS §3). Each call has its own deadline in the client;
        // retrying is the processor's decision, made on what the failure means. Experimental API, pinned centrally.
#pragma warning disable EXTEXP0001
        client.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

        return services;
    }
}
