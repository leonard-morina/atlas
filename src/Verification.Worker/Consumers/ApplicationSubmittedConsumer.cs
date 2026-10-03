using Atlas.Onboarding.Contracts;
using Atlas.Verification.Application.VerifyApplication;
using MassTransit;

namespace Atlas.Verification.Worker.Consumers;

/// <summary>
/// Verifies every application Onboarding submits. The entry point of this service, as an endpoint is for an API:
/// it maps the message to the use case and nothing else.
/// </summary>
public sealed class ApplicationSubmittedConsumer(VerifyApplicationHandler handler) : IConsumer<ApplicationSubmitted>
{
    public Task Consume(ConsumeContext<ApplicationSubmitted> context) =>
        handler.HandleAsync(ToCommand(context.Message), context.CancellationToken);

    private static VerifyApplicationCommand ToCommand(ApplicationSubmitted message) =>
        new(
            message.ApplicationId,
            message.Market,
            message.FirstName,
            message.LastName,
            message.DateOfBirth,
            message.Nationality,
            message.IdentityDocumentType switch
            {
                Onboarding.Contracts.IdentityDocumentType.Passport => Application.VerifyApplication.IdentityDocumentType.Passport,
                Onboarding.Contracts.IdentityDocumentType.IdCard => Application.VerifyApplication.IdentityDocumentType.IdCard,
                _ => throw new ArgumentOutOfRangeException(nameof(message), message.IdentityDocumentType, null),
            },
            new DocumentLocation(message.IdentityDocument.BlobName, message.IdentityDocument.Sha256),
            new DocumentLocation(message.Selfie.BlobName, message.Selfie.Sha256));
}
