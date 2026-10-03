using Atlas.Documents;
using Atlas.Verification.Application.Abstractions;
using Atlas.Verification.Application.VerifyApplication;

namespace Atlas.Verification.Infrastructure.Documents;

/// <summary>
/// Reads the images Onboarding stored (claim check), checking each against the SHA-256 in the message. Read access
/// only: in production the worker's identity holds the Storage Blob Data Reader role on the documents container.
/// </summary>
internal sealed class BlobDocumentReader(DocumentStorage storage) : IDocumentReader
{
    public Task<byte[]> ReadAsync(DocumentLocation location, CancellationToken cancellationToken) =>
        storage.DownloadVerifiedAsync(location.BlobName, location.Sha256, cancellationToken);
}
