namespace Atlas.Onboarding.Api.Features.Applications.SubmitApplication;

/// <param name="Type"><c>NATIONAL_ID</c>, or <c>PASSPORT</c> where the market accepts it (MF).</param>
/// <param name="Value">The identifier exactly as issued. Leading zeros are significant.</param>
/// <param name="IssuingCountry">Passport only: ISO 3166-1 alpha-3 code of the issuing state.</param>
public sealed record IdentifierRequest(IdentifierType? Type, string? Value, string? IssuingCountry = null);
