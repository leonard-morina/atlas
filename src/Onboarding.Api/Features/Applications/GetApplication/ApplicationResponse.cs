namespace Atlas.Onboarding.Api.Features.Applications.GetApplication;

/// <param name="Status">
/// <c>APPROVED</c>, <c>REJECTED</c>, <c>AWAITING_BRANCH_VISIT</c> and <c>ACCOUNT_OPENED</c> are decisions; otherwise check
/// back later. <c>APPROVED</c> becomes <c>ACCOUNT_OPENED</c> once the account is open.
/// </param>
/// <param name="DecidedAt">When the decision was made; absent while verification is still running.</param>
public sealed record ApplicationResponse(
    Guid ApplicationId,
    ApplicationStatus Status,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? DecidedAt);
