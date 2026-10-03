using Atlas.Verification.Application.Abstractions;
using Atlas.Verification.Contracts;
using Atlas.Verification.Domain;
using Atlas.Verification.Domain.Decisions;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Atlas.Verification.Application.VerifyApplication;

public sealed class VerifyApplicationHandler(
    IVerificationRepository verifications,
    IDocumentReader documents,
    IIdentityVerification identityVerification,
    ISanctionsScreening sanctionsScreening,
    IPublishEndpoint publishEndpoint,
    TimeProvider time,
    ILogger<VerifyApplicationHandler> logger)
{
    public async Task HandleAsync(VerifyApplicationCommand command, CancellationToken cancellationToken)
    {
        // The inbox already skips a redelivered message; this also covers the same application arriving in a
        // different message (republished by hand, for example), so the providers are never asked twice.
        if (await verifications.ExistsAsync(command.ApplicationId, cancellationToken))
        {
            logger.LogInformation("Application {ApplicationId} is already verified", command.ApplicationId);
            return;
        }

        var document = documents.ReadAsync(command.IdentityDocument, cancellationToken);
        var selfie = documents.ReadAsync(command.Selfie, cancellationToken);

        // The two providers are independent, so they are asked at the same time: the slower one sets the pace.
        var identity = identityVerification.VerifyAsync(
            command.IdentityDocumentType, await document, await selfie, cancellationToken);
        var screening = sanctionsScreening.ScreenAsync(
            $"{command.FirstName} {command.LastName}", command.DateOfBirth, command.Nationality, cancellationToken);

        var verification = ApplicationVerification.Complete(
            command.ApplicationId, command.Market, await identity, await screening, time.GetUtcNow());

        verifications.Add(verification);
        await publishEndpoint.Publish(ToEvent(verification), cancellationToken);
        await verifications.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Application {ApplicationId} verified: {Outcome}", verification.ApplicationId, verification.Outcome);
    }

    private static VerificationCompleted ToEvent(ApplicationVerification verification) =>
        new(
            verification.ApplicationId,
            verification.Outcome switch
            {
                Outcome.Approved => VerificationOutcome.Approved,
                Outcome.Rejected => VerificationOutcome.Rejected,
                Outcome.Referred => VerificationOutcome.Referred,
                _ => throw new ArgumentOutOfRangeException(nameof(verification), verification.Outcome, null),
            },
            [.. verification.Reasons.Select(reason => Enum.Parse<VerificationReason>(reason.ToString()))],
            verification.Identity.IdentificationId,
            verification.Screening.CaseId,
            verification.CompletedAt);
}
