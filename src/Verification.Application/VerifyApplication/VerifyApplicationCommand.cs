namespace Atlas.Verification.Application.VerifyApplication;

/// <summary>Verify one submitted application. Mapped from Onboarding's ApplicationSubmitted by the consumer.</summary>
public sealed record VerifyApplicationCommand(
    Guid ApplicationId,
    string Market,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Nationality,
    IdentityDocumentType IdentityDocumentType,
    DocumentLocation IdentityDocument,
    DocumentLocation Selfie);

/// <summary>Where a submitted image is stored, and the SHA-256 it must have.</summary>
public sealed record DocumentLocation(string BlobName, string Sha256);

public enum IdentityDocumentType
{
    Passport,
    IdCard,
}
