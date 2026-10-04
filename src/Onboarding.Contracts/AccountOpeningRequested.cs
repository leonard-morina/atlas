namespace Atlas.Onboarding.Contracts;

/// <summary>
/// Published by Onboarding when an application is approved in a market with remote activation. Carries what core
/// banking needs to open the account; the application's id goes along as the reference that finds it again.
/// Exactly one of <paramref name="NationalId"/> and <paramref name="Passport"/> is set.
/// </summary>
/// <param name="Market">Market of residence, MA to MF: the core banking endpoint to call.</param>
/// <param name="Passport">Only for MF non-citizen residents, who have no national identifier (Annex B).</param>
public sealed record AccountOpeningRequested(
    Guid ApplicationId,
    string Market,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string? NationalId,
    PassportIdentity? Passport);

/// <summary>A passport identifies a person only together with its issuing state (CDS-ID-04 §2).</summary>
public sealed record PassportIdentity(string Number, string IssuingCountry);
