using Atlas.Onboarding.Application.Persistence;
using Atlas.Onboarding.Contracts;
using Atlas.Onboarding.Domain.Applications;
using Atlas.Onboarding.Domain.Markets;
using MassTransit;
using MediatR;

namespace Atlas.Onboarding.Application.Features.Applications.RecordVerification;

public sealed class RecordVerificationHandler(
    IOnboardingApplicationRepository applications,
    SupportedMarkets supportedMarkets,
    IPublishEndpoint publishEndpoint) : IRequestHandler<RecordVerificationCommand>
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

        var alreadyDecided = application.DecidedAt is not null;
        application.RecordVerification(command.Verdict, market, command.DecidedAt);

        // Approved with remote activation: the account is opened next, by Accounts. Saved with the decision (outbox),
        // so an approval never goes without its request, and a redelivered verdict does not request a second.
        if (!alreadyDecided && application.NeedsAccount)
        {
            await publishEndpoint.Publish(ToAccountOpeningRequested(application), cancellationToken);
        }

        await applications.SaveChangesAsync(cancellationToken);
    }

    private static AccountOpeningRequested ToAccountOpeningRequested(OnboardingApplication application) =>
        new(
            application.Id,
            application.Market,
            application.Applicant.FirstName,
            application.Applicant.LastName,
            application.Applicant.DateOfBirth,
            application.Identifier.Type == IdentifierType.NationalId ? application.Identifier.Value : null,
            application.Identifier.Type == IdentifierType.Passport
                ? new PassportIdentity(application.Identifier.Value, application.Identifier.IssuingCountry!)
                : null);
}
