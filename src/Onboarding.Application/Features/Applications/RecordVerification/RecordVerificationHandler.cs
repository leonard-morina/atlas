using Atlas.Onboarding.Application.Persistence;
using Atlas.Onboarding.Domain.Markets;
using MediatR;

namespace Atlas.Onboarding.Application.Features.Applications.RecordVerification;

public sealed class RecordVerificationHandler(
    IOnboardingApplicationRepository applications,
    SupportedMarkets supportedMarkets) : IRequestHandler<RecordVerificationCommand>
{
    public async Task Handle(RecordVerificationCommand command, CancellationToken cancellationToken)
    {
        // Verification only verifies applications Onboarding published, so an unknown one is a fault worth a retry and,
        // failing that, the error queue: not something to skip silently.
        var application = await applications.FindForUpdateAsync(command.ApplicationId, cancellationToken)
                          ?? throw new InvalidOperationException(
                              $"Verification arrived for unknown application {command.ApplicationId}.");

        // A market removed from configuration while the verdict was in flight: refused, so a person decides
        // (see docs/decisions.md).
        if (!supportedMarkets.TryGet(application.Market, out var market))
        {
            throw new InvalidOperationException(
                $"Application {application.Id} is in market {application.Market}, which is no longer configured.");
        }

        application.RecordVerification(command.Verdict, market, command.DecidedAt);
        await applications.SaveChangesAsync(cancellationToken);
    }
}
