namespace Atlas.Onboarding.Api.Features.Applications.SubmitApplication;

/// <param name="ApplicationId">Identifies the application from now on, e.g. in <c>GET /applications/{id}</c>.</param>
/// <param name="Status">
/// A decision (<c>APPROVED</c>, <c>REJECTED</c>, <c>AWAITING_BRANCH_VISIT</c>, <c>ACCOUNT_OPENED</c>) with 201; otherwise
/// (<c>PROCESSING</c>, <c>REFERRED</c>) 202, and the app checks <c>GET /applications/{id}</c> later.
/// </param>
public sealed record SubmitApplicationResponse(Guid ApplicationId, ApplicationStatus Status);
