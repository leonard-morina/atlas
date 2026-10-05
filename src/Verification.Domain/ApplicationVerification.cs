using Atlas.Verification.Domain.Checks;
using Atlas.Verification.Domain.Decisions;

namespace Atlas.Verification.Domain;

/// <summary>
/// The verification of one application: what both providers said and what was decided. Kept as evidence: rejected
/// applications and their supporting material are retained for ten years (Compliance 4).
/// </summary>
public sealed class ApplicationVerification
{
    private readonly List<Reason> _reasons = [];

    private ApplicationVerification(
        Guid applicationId,
        string market,
        IdentityCheck identity,
        ScreeningCheck screening,
        VerificationDecision decision,
        DateTimeOffset completedAt)
    {
        ApplicationId = applicationId;
        Market = market;
        Identity = identity;
        Screening = screening;
        Outcome = decision.Outcome;
        _reasons.AddRange(decision.Reasons);
        CompletedAt = completedAt;
    }

    // Used by persistence to rebuild a stored verification.
    private ApplicationVerification()
    {
        Market = null!;
        Identity = null!;
        Screening = null!;
    }

    /// <summary>One verification per application: the application's id is the key.</summary>
    public Guid ApplicationId { get; private set; }

    public string Market { get; private set; }

    public IdentityCheck Identity { get; private set; }

    public ScreeningCheck Screening { get; private set; }

    public Outcome Outcome { get; private set; }

    public IReadOnlyList<Reason> Reasons => _reasons;

    public DateTimeOffset CompletedAt { get; private set; }

    public static ApplicationVerification Complete(
        Guid applicationId,
        string market,
        IdentityCheck identity,
        ScreeningCheck screening,
        DateTimeOffset completedAt) =>
        new(applicationId, market, identity, screening, VerificationDecision.Decide(identity, screening), completedAt);
}
