namespace Atlas.Onboarding.Domain.Applications;

/// <summary>
/// A submitted image, stored outside the database. The application keeps where it is and its SHA-256, so
/// whoever reads it later (verification, a compliance officer) can tell that it is the one the customer sent.
/// </summary>
public sealed record ApplicationDocument(DocumentType Type, string BlobName, string Sha256, long SizeBytes);
