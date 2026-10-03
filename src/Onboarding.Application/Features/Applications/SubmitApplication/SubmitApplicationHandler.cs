using System.Security.Cryptography;
using Atlas.Onboarding.Application.Persistence;
using Atlas.Onboarding.Contracts;
using Atlas.Onboarding.Domain.Applications;
using Atlas.Onboarding.Domain.Markets;
using MassTransit;
using MediatR;

namespace Atlas.Onboarding.Application.Features.Applications.SubmitApplication;

public sealed class SubmitApplicationHandler(
    IOnboardingApplicationRepository applications,
    IDocumentStore documentStore,
    IPublishEndpoint publishEndpoint,
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

        // Images first, then the row that refers to them. They cannot share a transaction, and this is the safe
        // order: if saving fails, an unreferenced image is left (wasted storage, removed by a cleanup policy);
        // the other order could save an application whose images do not exist.
        var applicationId = Guid.NewGuid();
        var documents = await Task.WhenAll(command.Documents.Select(document =>
            StoreAsync(market.Code, applicationId, document, cancellationToken)));

        var application = OnboardingApplication.Submit(
            applicationId,
            command.IdempotencyKey,
            fingerprint,
            market,
            new Applicant(command.FirstName, command.LastName, command.DateOfBirth, command.Nationality, command.Email, command.Phone),
            command.Identifier,
            documents,
            time.GetUtcNow());

        // With the outbox this only adds the message to the unit of work: it is stored by the same SaveChanges as
        // the application and sent afterwards. If the insert loses a race, the message is discarded with it.
        await publishEndpoint.Publish(ToEvent(application), cancellationToken);

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

    private async Task<ApplicationDocument> StoreAsync(
        string market,
        Guid applicationId,
        ApplicantDocument document,
        CancellationToken cancellationToken)
    {
        var blobName = await documentStore.StoreAsync(market, applicationId, document.Type, document.Content, cancellationToken);

        return new ApplicationDocument(
            document.Type, blobName, Convert.ToHexString(SHA256.HashData(document.Content)), document.Content.LongLength);
    }

    private static ApplicationSubmitted ToEvent(OnboardingApplication application)
    {
        var identityDocument = application.Documents.Single(document => document.Type != DocumentType.Selfie);
        var selfie = application.Documents.Single(document => document.Type == DocumentType.Selfie);

        return new ApplicationSubmitted(
            application.Id,
            application.Market,
            application.Applicant.FirstName,
            application.Applicant.LastName,
            application.Applicant.DateOfBirth,
            application.Applicant.Nationality,
            identityDocument.Type == DocumentType.Passport ? IdentityDocumentType.Passport : IdentityDocumentType.IdCard,
            new DocumentReference(identityDocument.BlobName, identityDocument.Sha256),
            new DocumentReference(selfie.BlobName, selfie.Sha256));
    }

    private static SubmitApplicationResult Replay(OnboardingApplication earlier, string fingerprint) =>
        earlier.RequestFingerprint == fingerprint
            ? new SubmitApplicationResult.Accepted(earlier.Id, earlier.Status, Replayed: true)
            : new SubmitApplicationResult.IdempotencyKeyReused();
}
