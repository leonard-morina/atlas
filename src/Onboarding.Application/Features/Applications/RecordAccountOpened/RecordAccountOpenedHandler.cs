using Atlas.Onboarding.Application.Persistence;
using MediatR;

namespace Atlas.Onboarding.Application.Features.Applications.RecordAccountOpened;

public sealed class RecordAccountOpenedHandler(IOnboardingApplicationRepository applications)
    : IRequestHandler<RecordAccountOpenedCommand>
{
    public async Task Handle(RecordAccountOpenedCommand command, CancellationToken cancellationToken)
    {
        // Accounts only opens accounts Onboarding requested, so an unknown application is a fault, not a skip.
        var application = await applications.FindForUpdateAsync(command.ApplicationId, cancellationToken)
                          ?? throw new InvalidOperationException(
                              $"An account was opened for unknown application {command.ApplicationId}.");

        application.RecordAccountOpened(command.AccountNumber, command.OpenedAt);
        await applications.SaveChangesAsync(cancellationToken);
    }
}
