namespace Atlas.Onboarding.Domain.Markets.Identifiers;

/// <summary>
/// MC: 8 digits followed by a check letter, the letter at position (N mod 23) in
/// TRWAGMYFPDXBNJZSQVHLCKE where N is the eight digits as a number (CDS-ID-04 §3.2).
/// </summary>
public sealed class UnifiedCitizenIdValidator : INationalIdValidator
{
    private const string CheckLetters = "TRWAGMYFPDXBNJZSQVHLCKE";

    public IdentifierScheme Scheme => IdentifierScheme.UnifiedCitizenId;

    public string? Validate(string value, DateOnly dateOfBirth)
    {
        if (value.Length != 9 || !value[..8].All(char.IsAsciiDigit) || !char.IsAsciiLetterUpper(value[8]))
        {
            return "Must be 8 digits followed by an upper-case letter.";
        }

        var number = int.Parse(value[..8], System.Globalization.CultureInfo.InvariantCulture);

        return value[8] == CheckLetters[number % CheckLetters.Length] ? null : "Check letter does not match.";
    }
}
