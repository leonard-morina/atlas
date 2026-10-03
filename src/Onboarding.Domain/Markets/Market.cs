using Atlas.Onboarding.Domain.Markets.Identifiers;

namespace Atlas.Onboarding.Domain.Markets;

/// <summary>A configured market, with the validator for its national identifier.</summary>
public sealed record Market(
    string Code,
    bool AcceptsPassport,
    ActivationMode Activation,
    INationalIdValidator NationalId);
