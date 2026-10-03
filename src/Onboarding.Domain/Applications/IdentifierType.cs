namespace Atlas.Onboarding.Domain.Applications;

/// <summary>
/// What the applicant identifies with. Most use their national identifier; MF non-citizen residents have
/// none and use a passport instead (Annex B; CDS-ID-04 §2).
/// </summary>
public enum IdentifierType
{
    NationalId,
    Passport,
}
