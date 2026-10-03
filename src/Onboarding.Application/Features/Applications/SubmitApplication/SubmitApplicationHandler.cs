using Atlas.Onboarding.Application.Persistence;
using Atlas.Onboarding.Domain.Applications;
using Atlas.Onboarding.Domain.Markets;
using MediatR;

namespace Atlas.Onboarding.Application.Features.Applications.SubmitApplication;

public sealed class SubmitApplicationHandler(
    IOnboardingApplicationRepository applications,
    SupportedMarkets supportedMarkets,
    TimeProvider time) : IRequestHandler<SubmitApplicationCommand, SubmitApplicationResult>
{
    public async Task<SubmitApplicationResult> Handle(SubmitApplicationCommand command, CancellationToken cancellationToken)
    {
        var fingerprint = SubmissionFingerprint.Of(command);

        // A retry of a request that was already stored gets the same answer, never a second application.
        if (await applications.FindByIdempotencyKeyAsync(command.IdempotencyKey, cancellationToken) is { } earlier)
        {
            return Replay(earlier, fingerprint);
        }

        if (await applications.HasBlockingApplicationAsync(command.Market, command.Identifier, cancellationToken))
        {
            return new SubmitApplicationResult.ApplicantAlreadyApplied();
        }

        if (!supportedMarkets.TryGet(command.Market, out var market))
        {
            throw new InvalidOperationException($"Market {command.Market} reached the handler but is not supported.");
        }

        var application = OnboardingApplication.Submit(
            command.IdempotencyKey,
            fingerprint,
            market,
            new Applicant(command.FirstName, command.LastName, command.DateOfBirth, command.Nationality, command.Email, command.Phone),
            command.Identifier,
            time.GetUtcNow());

        if (await applications.TryAddAsync(application, cancellationToken))
        {
            return new SubmitApplicationResult.Accepted(application.Id, application.Status, Replayed: false);
        }

        // A concurrent request was stored first. With the same key it is a retry, even though the identifier
        // index may be the one that reported the conflict; otherwise the applicant applied twice at once.
        return await applications.FindByIdempotencyKeyAsync(command.IdempotencyKey, cancellationToken) is { } winner
            ? Replay(winner, fingerprint)
            : new SubmitApplicationResult.ApplicantAlreadyApplied();
    }

    private static SubmitApplicationResult Replay(OnboardingApplication earlier, string fingerprint) =>
        earlier.RequestFingerprint == fingerprint
            ? new SubmitApplicationResult.Accepted(earlier.Id, earlier.Status, Replayed: true)
            : new SubmitApplicationResult.IdempotencyKeyReused();
}
