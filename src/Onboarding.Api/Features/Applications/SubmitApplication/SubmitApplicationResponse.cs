namespace Atlas.Onboarding.Api.Features.Applications.SubmitApplication;

/// <param name="ApplicationId">Identifies the application from now on, e.g. in <c>GET /applications/{id}</c>.</param>
/// <param name="Status"><c>APPROVED</c> or <c>REJECTED</c> are final; otherwise check back later.</param>
public sealed record SubmitApplicationResponse(Guid ApplicationId, ApplicationStatus Status);
