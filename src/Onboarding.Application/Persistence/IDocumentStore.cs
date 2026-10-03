using Atlas.Onboarding.Domain.Applications;

namespace Atlas.Onboarding.Application.Persistence;

/// <summary>Where submitted images are kept: blob storage, not the database. Implemented by Infrastructure.</summary>
public interface IDocumentStore
{
    /// <summary>Stores an image under its market and application: where it is, its SHA-256 and its size.</summary>
    Task<ApplicationDocument> StoreAsync(
        string market,
        Guid applicationId,
        DocumentType type,
        byte[] content,
        CancellationToken cancellationToken);
}
