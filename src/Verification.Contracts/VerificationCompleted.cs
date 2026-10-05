namespace Atlas.Verification.Contracts;

/// <summary>
/// Published by Verification once identity verification and sanctions screening have both answered and a
/// decision is made. Carries the provider references a compliance officer needs, not the provider responses.
/// </summary>
/// <param name="Reasons">Why the outcome is not a plain approval; empty when approved.</param>
/// <param name="IdentificationId">IDNow's reference for the identification.</param>
/// <param name="ScreeningCaseId">World-Check's case, which a compliance officer reviews when referred.</param>
public sealed record VerificationCompleted(
    Guid ApplicationId,
    VerificationOutcome Outcome,
    IReadOnlyList<VerificationReason> Reasons,
    string IdentificationId,
    string ScreeningCaseId,
    DateTimeOffset CompletedAt);

public enum VerificationOutcome
{
    Approved,
    Rejected,
    /// <summary>A possible sanctions or PEP match: a compliance officer must decide (Compliance 3).</summary>
    Referred,
}

public enum VerificationReason
{
    DocumentInvalid,
    DocumentInconclusive,
    FaceMismatch,
    PossibleSanctionsMatch,
    PossiblePepMatch,
}
