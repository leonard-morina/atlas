using MediatR;

namespace Atlas.Onboarding.Application.Features.Applications.SubmitApplication;

public sealed class SubmitApplicationHandler : IRequestHandler<SubmitApplicationCommand, SubmitApplicationResult>
{
    public Task<SubmitApplicationResult> Handle(SubmitApplicationCommand command, CancellationToken cancellationToken) =>
        throw new NotImplementedException("Storing applications is not implemented yet.");
}
