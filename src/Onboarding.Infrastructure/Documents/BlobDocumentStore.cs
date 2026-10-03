using Atlas.Documents;
using Atlas.Onboarding.Application.Persistence;
using Atlas.Onboarding.Domain.Applications;

namespace Atlas.Onboarding.Infrastructure.Documents;

internal sealed class BlobDocumentStore(DocumentStorage storage) : IDocumentStore
{
    public async Task<ApplicationDocument> StoreAsync(
        string market,
        Guid applicationId,
        DocumentType type,
        byte[] content,
        CancellationToken cancellationToken)
    {
        // Market first: in production each market's documents live in storage in that market (Compliance §1),
        // so the first path segment is what decides where a document is kept.
        var stored = await storage.UploadAsync(
            $"{market}/{applicationId}/{type.ToString().ToLowerInvariant()}", content, cancellationToken);

        return new ApplicationDocument(type, stored.BlobName, stored.Sha256, stored.SizeBytes);
    }
}
