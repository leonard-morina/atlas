namespace Atlas.Onboarding.Domain.Applications;

/// <summary>The person applying, as they entered their details.</summary>
/// <param name="Nationality">ISO 3166-1 alpha-3. Not the market: residents are not always nationals.</param>
public sealed record Applicant(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Nationality,
    string Email,
    string Phone);
