using System.Diagnostics.CodeAnalysis;
using Atlas.Onboarding.Domain.Markets.Identifiers;

namespace Atlas.Onboarding.Domain.Markets;

/// <summary>
/// The markets Atlas operates in. A market that is not configured is not supported: adding one
/// means reissuing Annex B first (note 3), then adding its configuration.
/// </summary>
public sealed class SupportedMarkets
{
    private readonly Dictionary<string, Market> _markets;

    public SupportedMarkets(
        IReadOnlyDictionary<string, MarketRules> markets,
        IEnumerable<INationalIdValidator> validators)
    {
        var validatorsByScheme = validators.ToDictionary(v => v.Scheme);

        _markets = markets.ToDictionary(
            entry => entry.Key,
            entry => new Market(
                entry.Key,
                entry.Value.AcceptsPassport,
                entry.Value.Activation,
                validatorsByScheme.TryGetValue(entry.Value.IdentifierScheme, out var validator)
                    ? validator
                    : throw new InvalidOperationException(
                        $"Market {entry.Key} uses {entry.Value.IdentifierScheme}, which has no validator.")),
            StringComparer.Ordinal);
    }

    public bool TryGet(string? code, [NotNullWhen(true)] out Market? market)
    {
        market = null;
        return code is not null && _markets.TryGetValue(code, out market);
    }
}
