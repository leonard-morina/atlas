namespace Atlas.Accounts.Domain.Openings;

/// <summary>
/// The person the account is for, as core banking needs them. Identified by a national identifier, or, for MF
/// non-citizen residents, by a passport (Annex B).
/// </summary>
public sealed record Customer(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string? NationalId,
    string? PassportNumber,
    string? PassportIssuingCountry)
{
    /// <summary>
    /// Whether core banking can find this customer's accounts. Lookups are by national ID only; passport lookups are
    /// on its roadmap (CBS-4471, unscheduled).
    /// </summary>
    public bool CanBeLookedUp => NationalId is not null;
}
