namespace Atlas.Onboarding.Domain.Markets.Identifiers;

/// <summary>
/// MD: 10 digits, the tenth a check digit. Weights 10..2 on the first nine digits,
/// m = 11 - (sum mod 11), with 10 and 11 mapping to 0 (CDS-ID-04 §3.3).
/// </summary>
public sealed class CitizenNumberValidator : INationalIdValidator
{
    public IdentifierScheme Scheme => IdentifierScheme.CitizenNumber;

    public string? Validate(string value, DateOnly dateOfBirth)
    {
        if (value.Length != 10 || !value.All(char.IsAsciiDigit))
        {
            return "Must be 10 digits.";
        }

        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            sum += (10 - i) * (value[i] - '0');
        }

        var m = 11 - sum % 11;
        var checkDigit = m >= 10 ? 0 : m;

        return value[9] - '0' == checkDigit ? null : "Check digit does not match.";
    }
}
