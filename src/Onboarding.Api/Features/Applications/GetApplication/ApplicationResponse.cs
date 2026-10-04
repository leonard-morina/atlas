namespace Atlas.Onboarding.Api.Features.Applications.GetApplication;

/// <param name="Status"><c>APPROVED</c>, <c>REJECTED</c> and <c>AWAITING_BRANCH_VISIT</c> are final; otherwise check back later.</param>
/// <param name="DecidedAt">When the decision was made; absent while verification is still running.</param>
public sealed record ApplicationResponse(
    Guid ApplicationId,
    ApplicationStatus Status,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? DecidedAt);
