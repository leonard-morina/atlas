namespace Atlas.Onboarding.Domain.Markets.Identifiers;

/// <summary>
/// Format and check-digit validation for one national identifier scheme (CDS-ID-04 §3).
/// Identifiers are validation inputs only, never customer keys (Annex B, note 2).
/// </summary>
public interface INationalIdValidator
{
    IdentifierScheme Scheme { get; }

    /// <returns><c>null</c> when valid; otherwise the reason, phrased for the customer.</returns>
    string? Validate(string value, DateOnly dateOfBirth);
}
