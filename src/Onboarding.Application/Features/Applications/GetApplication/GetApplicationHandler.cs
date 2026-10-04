using Atlas.Onboarding.Application.Persistence;
using MediatR;

namespace Atlas.Onboarding.Application.Features.Applications.GetApplication;

public sealed class GetApplicationHandler(IOnboardingApplicationRepository applications)
    : IRequestHandler<GetApplicationQuery, ApplicationState?>
{
    public async Task<ApplicationState?> Handle(GetApplicationQuery query, CancellationToken cancellationToken) =>
        await applications.FindStatusAsync(query.ApplicationId, cancellationToken) is { } snapshot
            ? new ApplicationState(snapshot.ApplicationId, snapshot.Status, snapshot.SubmittedAt, snapshot.DecidedAt)
            : null;
}
