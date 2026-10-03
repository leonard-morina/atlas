using Atlas.Onboarding.Domain.Applications;

namespace Atlas.Onboarding.Application.Features.Applications.SubmitApplication;

/// <summary>Every way a submission can end. The endpoint turns each into an HTTP response.</summary>
public abstract record SubmitApplicationResult
{
    private SubmitApplicationResult()
    {
    }

    /// <summary>Stored now, or earlier by a request with the same Idempotency-Key and content (a retry).</summary>
    public sealed record Accepted(Guid ApplicationId, ApplicationStatus Status, bool Replayed) : SubmitApplicationResult;

    /// <summary>The Idempotency-Key was already used for a different application.</summary>
    public sealed record IdempotencyKeyReused : SubmitApplicationResult;

    /// <summary>
    /// The identifier already has an application in this market that blocks a new one. Which one is not
    /// revealed: whoever sends someone else's identifier must not learn anything from the answer.
    /// </summary>
    public sealed record ApplicantAlreadyApplied : SubmitApplicationResult;
}
