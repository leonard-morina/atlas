using System.Security.Cryptography;
using Atlas.Verification.Application.Abstractions;
using Atlas.Verification.Application.VerifyApplication;
using Azure.Storage.Blobs;

namespace Atlas.Verification.Infrastructure.Documents;

/// <summary>
/// Reads the images Onboarding stored (claim check). Read access only: in production the worker's identity holds
/// the Storage Blob Data Reader role on the documents container, and only Onboarding can write.
/// </summary>
internal sealed class BlobDocumentReader(BlobServiceClient blobs) : IDocumentReader
{
    public const string ContainerName = "documents";

    public async Task<byte[]> ReadAsync(DocumentLocation location, CancellationToken cancellationToken)
    {
        var download = await blobs.GetBlobContainerClient(ContainerName)
            .GetBlobClient(location.BlobName)
            .DownloadContentAsync(cancellationToken);

        var content = download.Value.Content.ToArray();

        if (!string.Equals(Convert.ToHexString(SHA256.HashData(content)), location.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new DocumentIntegrityException(location.BlobName);
        }

        return content;
    }
}
