namespace Atlas.Onboarding.Domain.Markets.Identifiers;

/// <summary>
/// ME: 10 digits. Format only: the check-digit algorithm is held by the ME civil registry under licence
/// and is not in CDS-ID-04 (§3.4). Known limitation; an invalid check digit is caught by verification instead.
/// </summary>
public sealed class CivilNumberValidator : INationalIdValidator
{
    public IdentifierScheme Scheme => IdentifierScheme.CivilNumber;

    public string? Validate(string value, DateOnly dateOfBirth) =>
        value.Length == 10 && value.All(char.IsAsciiDigit) ? null : "Must be 10 digits.";
}
