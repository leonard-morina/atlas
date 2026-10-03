using Atlas.Onboarding.Domain.Markets;
using Atlas.Onboarding.Domain.Markets.Identifiers;
using Microsoft.Extensions.Options;

namespace Atlas.Onboarding.Api.Extensions;

/// <summary>Binds the "Markets" configuration (Annex B) to the domain's market.</summary>
public static class MarketsExtensions
{
    public static IHostApplicationBuilder AddMarkets(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<Dictionary<string, MarketRules>>()
            .Bind(builder.Configuration.GetSection("Markets"))
            .Validate(markets => markets.Count > 0, "No markets configured in the 'Markets' section.")
            .ValidateOnStart();

        builder.Services.AddSingleton<INationalIdValidator, PersonalNumberValidator>();
        builder.Services.AddSingleton<INationalIdValidator, UnifiedCitizenIdValidator>();
        builder.Services.AddSingleton<INationalIdValidator, CitizenNumberValidator>();
        builder.Services.AddSingleton<INationalIdValidator, CivilNumberValidator>();

        builder.Services.AddSingleton(services => new SupportedMarkets(
            services.GetRequiredService<IOptions<Dictionary<string, MarketRules>>>().Value,
            services.GetServices<INationalIdValidator>()));

        return builder;
    }
}
