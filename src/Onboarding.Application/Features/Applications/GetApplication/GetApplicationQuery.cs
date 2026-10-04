using Atlas.Onboarding.Domain.Applications;
using MediatR;

namespace Atlas.Onboarding.Application.Features.Applications.GetApplication;

/// <summary>Where an application stands. <c>null</c> when there is no such application.</summary>
public sealed record GetApplicationQuery(Guid ApplicationId) : IRequest<ApplicationState?>;

/// <summary>
/// What may be told about an application: its state, never the personal data, and never why it was rejected or
/// referred. Telling a customer they matched a sanctions list would be tipping off.
/// </summary>
public sealed record ApplicationState(Guid ApplicationId, ApplicationStatus Status, DateTimeOffset SubmittedAt, DateTimeOffset? DecidedAt);
