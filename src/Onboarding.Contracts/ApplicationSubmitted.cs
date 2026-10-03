namespace Atlas.Onboarding.Contracts;

/// <summary>
/// Published by Onboarding once an application is stored. Carries only what verification needs: the person for
/// sanctions screening, and references to the images (claim check) rather than the images themselves.
/// </summary>
/// <param name="Market">Market of residence, MA to MF. Decides where the documents are kept.</param>
/// <param name="Nationality">ISO 3166-1 alpha-3; screening needs nationality, not residence.</param>
public sealed record ApplicationSubmitted(
    Guid ApplicationId,
    string Market,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Nationality,
    IdentityDocumentType IdentityDocumentType,
    DocumentReference IdentityDocument,
    DocumentReference Selfie);

/// <summary>Where an image is kept, and its SHA-256 so the reader can check it is the one that was submitted.</summary>
public sealed record DocumentReference(string BlobName, string Sha256);

public enum IdentityDocumentType
{
    Passport,
    IdCard,
}
