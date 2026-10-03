namespace Atlas.Onboarding.Api.Features.Applications.SubmitApplication;

/// <summary>Body of <c>POST /applications</c> (v1).</summary>
/// <remarks>
/// Fields are nullable so that a missing field is reported by validation (422, per field) instead of
/// failing deserialization. Changes from the ticket's draft: <c>nationalId</c> became
/// <see cref="Identifier"/> (MF residents may have only a passport) and <see cref="Nationality"/> was
/// added (sanctions screening needs nationality, which is not the market of residence).
/// </remarks>
/// <param name="FirstName">Given name(s).</param>
/// <param name="LastName">Family name(s).</param>
/// <param name="DateOfBirth">ISO 8601 date, e.g. <c>1991-03-04</c>.</param>
/// <param name="Country">Market of residence: <c>MA</c> to <c>MF</c>.</param>
/// <param name="Nationality">ISO 3166-1 alpha-3 country code, e.g. <c>MKD</c>.</param>
/// <param name="Identifier">The customer's national identifier, or passport where the market accepts it.</param>
/// <param name="Email">Contact email address.</param>
/// <param name="Phone">International format, e.g. <c>+38970123456</c>.</param>
/// <param name="Documents">Exactly one identity document (<c>PASSPORT</c> or <c>ID_CARD</c>) and one <c>SELFIE</c>.</param>
/// <param name="TermsAccepted">Must be <c>true</c>.</param>
public sealed record SubmitApplicationRequest(
    string? FirstName,
    string? LastName,
    DateOnly? DateOfBirth,
    string? Country,
    string? Nationality,
    IdentifierRequest? Identifier,
    string? Email,
    string? Phone,
    IReadOnlyList<DocumentRequest>? Documents,
    bool? TermsAccepted);

/// <param name="Type"><c>NATIONAL_ID</c>, or <c>PASSPORT</c> where the market accepts it (MF).</param>
/// <param name="Value">The identifier exactly as issued. Leading zeros are significant.</param>
/// <param name="IssuingCountry">Passport only: ISO 3166-1 alpha-3 code of the issuing state.</param>
public sealed record IdentifierRequest(IdentifierType? Type, string? Value, string? IssuingCountry);

/// <param name="Type"><c>PASSPORT</c>, <c>ID_CARD</c> or <c>SELFIE</c>.</param>
/// <param name="Image">Base64-encoded image.</param>
public sealed record DocumentRequest(DocumentType? Type, string? Image);

public enum IdentifierType
{
    NationalId,
    Passport,
}

public enum DocumentType
{
    Passport,
    IdCard,
    Selfie,
}
