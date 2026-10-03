using System.Net;
using Atlas.Verification.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace Atlas.Verification.Infrastructure.Providers;

internal static class ProviderClientExtensions
{
    public static IServiceCollection AddProviderClients(this IServiceCollection services)
    {
        services.AddHttpClient<IIdentityVerification, IdNowClient>(ProviderOptions.IdNow)
            .ConfigureProvider(ProviderOptions.IdNow);

        services.AddHttpClient<ISanctionsScreening, WorldCheckClient>(ProviderOptions.WorldCheck)
            .ConfigureProvider(ProviderOptions.WorldCheck);

        return services;
    }

    /// <summary>
    /// Replaces the default resilience (several retries on any failure) for calls with side effects at the
    /// provider: a retried IDNow call can be a second, billed identification, a retried World-Check call a second
    /// case. Retried only when the request cannot have been processed: the connection failed, or the provider
    /// answered 503. A timeout is not retried here, because the provider may have done the work; the message is
    /// retried later instead.
    /// </summary>
    private static IHttpClientBuilder ConfigureProvider(this IHttpClientBuilder client, string provider)
    {
        client.ConfigureHttpClient((services, http) =>
            http.BaseAddress = services.GetRequiredService<IOptionsMonitor<ProviderOptions>>().Get(provider).BaseUrl);

        // Experimental API, but the documented way to opt one client out of the defaults ServiceDefaults gives every
        // HttpClient. The package version is pinned centrally, so it cannot change underneath us.
#pragma warning disable EXTEXP0001
        client.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

        client.AddResilienceHandler(provider, (pipeline, context) =>
        {
            var options = context.ServiceProvider.GetRequiredService<IOptionsMonitor<ProviderOptions>>().Get(provider);

            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = attempt => ValueTask.FromResult(
                    attempt.Outcome.Exception is HttpRequestException { HttpRequestError: HttpRequestError.ConnectionError }
                    || attempt.Outcome.Result?.StatusCode == HttpStatusCode.ServiceUnavailable),
            });

            pipeline.AddTimeout(options.Timeout);
        });

        return client;
    }
}
