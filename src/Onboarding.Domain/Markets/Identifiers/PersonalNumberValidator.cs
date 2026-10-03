namespace Atlas.Onboarding.Domain.Markets.Identifiers;

/// <summary>
/// MA, MB and MF citizens: 13 digits, DDMMYYY date of birth, RR region, BBB sequence, K check digit
/// (CDS-ID-04 §3.1). The encoded date of birth must match the one captured separately (§1.4).
/// </summary>
public sealed class PersonalNumberValidator : INationalIdValidator
{
    private static readonly int[] Weights = [7, 6, 5, 4, 3, 2];

    public IdentifierScheme Scheme => IdentifierScheme.PersonalNumber;

    public string? Validate(string value, DateOnly dateOfBirth)
    {
        if (value.Length != 13 || !value.All(char.IsAsciiDigit))
        {
            return "Must be 13 digits.";
        }

        var digits = value.Select(c => c - '0').ToArray();

        var sum = 0;
        for (var i = 0; i < Weights.Length; i++)
        {
            sum += Weights[i] * (digits[i] + digits[i + 6]);
        }

        // m = 11 gives check digit 0; m = 10 is never issued, so no digit can match it.
        var m = 11 - sum % 11;
        var checkDigit = m switch
        {
            11 => 0,
            10 => -1,
            _ => m,
        };

        if (digits[12] != checkDigit)
        {
            return "Check digit does not match.";
        }

        return EncodedDateOfBirth(digits) == dateOfBirth ? null : "Does not match the date of birth.";
    }

    // DDMMYYY: the year is written as its last three digits. The sheet does not say how to expand them;
    // assumed 9xx = 19xx and 0xx = 20xx (listed in docs as an open question).
    private static DateOnly? EncodedDateOfBirth(int[] digits)
    {
        var day = digits[0] * 10 + digits[1];
        var month = digits[2] * 10 + digits[3];
        var lastThree = digits[4] * 100 + digits[5] * 10 + digits[6];

        var year = lastThree switch
        {
            >= 900 => 1000 + lastThree,
            < 100 => 2000 + lastThree,
            _ => 0,
        };

        if (year == 0 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return null;
        }

        return new DateOnly(year, month, day);
    }
}
