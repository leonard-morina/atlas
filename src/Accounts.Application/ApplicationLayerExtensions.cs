using Atlas.Accounts.Application.ProcessOpenings;
using Atlas.Accounts.Application.RequestAccountOpening;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Atlas.Accounts.Application;

/// <summary>
/// Two entry points, the message consumer and the processor, each calling its handler directly (as in Verification):
/// MassTransit's pipeline already wraps the consumer, and the processor is its own loop.
/// </summary>
public static class ApplicationLayerExtensions
{
    public static IHostApplicationBuilder AddApplicationLayer(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<AccountOpeningOptions>()
            .BindConfiguration(AccountOpeningOptions.Section)
            .Validate(
                options => options.MarketTimeZones.Count > 0
                           && options.MarketTimeZones.Values.All(zone => TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _)),
                "Every market needs a known time zone.")
            .Validate(options => options.CallsInFlightPerMarket is > 0 and < 5, "The share must leave the branches room.")
            .ValidateOnStart();

        builder.Services.AddScoped<RequestAccountOpeningHandler>();
        builder.Services.AddScoped<CallCoreBankingHandler>();
        builder.Services.AddScoped<RecoverLostCallsHandler>();
        builder.Services.AddSingleton<OpeningSignal>();
        builder.Services.AddHostedService<AccountOpeningProcessor>();
        builder.Services.TryAddSingleton(TimeProvider.System);

        return builder;
    }
}
