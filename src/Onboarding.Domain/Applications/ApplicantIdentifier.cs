namespace Atlas.Onboarding.Domain.Applications;

/// <summary>
/// How the applicant is identified: a national identifier, or a passport with its issuing state
/// (CDS-ID-04 §2: the passport number alone does not identify a person). A validation input,
/// never a key for the application (Annex B, note 2).
/// </summary>
public sealed record ApplicantIdentifier(IdentifierType Type, string Value, string? IssuingCountry);
