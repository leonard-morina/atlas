using Atlas.Onboarding.Domain.Markets.Identifiers;

namespace Atlas.Onboarding.Domain.Markets;

/// <summary>
/// One market's rules from Annex B to GC-2026-0814, bound from the "Markets" configuration section.
/// Market differences are configuration, not code (ATLAS-1, AC4).
/// </summary>
public sealed class MarketRules
{
    public IdentifierScheme IdentifierScheme { get; init; }

    /// <summary>MF: non-citizen residents have no Personal Number and are identified by passport.</summary>
    public bool AcceptsPassport { get; init; }

    /// <summary>MD: remote identification covers the application only; activation needs a branch visit.</summary>
    public ActivationMode Activation { get; init; } = ActivationMode.Remote;
}
