using Atlas.Onboarding.Application.Persistence;
using Atlas.Onboarding.Domain.Applications;
using Azure.Storage.Blobs;

namespace Atlas.Onboarding.Infrastructure.Documents;

internal sealed class BlobDocumentStore(BlobServiceClient blobs) : IDocumentStore
{
    public const string ContainerName = "documents";

    public async Task<string> StoreAsync(
        string market,
        Guid applicationId,
        DocumentType type,
        byte[] content,
        CancellationToken cancellationToken)
    {
        // Market first: in production each market's documents live in storage in that market (Compliance §1),
        // so the first path segment is what decides where a document is kept.
        var blobName = $"{market}/{applicationId}/{type.ToString().ToLowerInvariant()}";

        await blobs.GetBlobContainerClient(ContainerName)
            .GetBlobClient(blobName)
            .UploadAsync(BinaryData.FromBytes(content), overwrite: false, cancellationToken);

        return blobName;
    }
}
