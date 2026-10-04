using Atlas.Onboarding.Application.Persistence;
using MediatR;

namespace Atlas.Onboarding.Application.Features.Applications.GetApplication;

public sealed class GetApplicationHandler(IOnboardingApplicationRepository applications)
    : IRequestHandler<GetApplicationQuery, ApplicationState?>
{
    public async Task<ApplicationState?> Handle(GetApplicationQuery query, CancellationToken cancellationToken) =>
        await applications.FindAsync(query.ApplicationId, cancellationToken) is { } application
            ? new ApplicationState(application.Id, application.Status, application.SubmittedAt, application.DecidedAt)
            : null;
}
