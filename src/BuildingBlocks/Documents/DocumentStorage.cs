using System.Security.Cryptography;
using Azure.Storage.Blobs;

namespace Atlas.Documents;


/// <summary>
/// Blob storage for submitted documents, shared by the services that write and read them so the container, the
/// hash and the integrity check cannot drift apart. Sharing the code does not share the data: Onboarding owns the
/// container, and in production other services' identities only hold read access to it.
/// </summary>
public sealed class DocumentStorage(BlobServiceClient blobs)
{
    private const string ContainerName = "documents";

    private BlobContainerClient Container => blobs.GetBlobContainerClient(ContainerName);

    /// <summary>Stores a new document; an existing one is never overwritten.</summary>
    public async Task<StoredDocument> UploadAsync(string blobName, byte[] content, CancellationToken cancellationToken)
    {
        await Container.GetBlobClient(blobName).UploadAsync(BinaryData.FromBytes(content), overwrite: false, cancellationToken);

        return new StoredDocument(blobName, Hash(content), content.LongLength);
    }

    /// <summary>Reads a document and checks it is the one that was stored.</summary>
    /// <exception cref="DocumentIntegrityException">The content no longer has the expected SHA-256.</exception>
    public async Task<byte[]> DownloadVerifiedAsync(string blobName, string expectedSha256, CancellationToken cancellationToken)
    {
        var download = await Container.GetBlobClient(blobName).DownloadContentAsync(cancellationToken);
        var content = download.Value.Content.ToArray();

        return string.Equals(Hash(content), expectedSha256, StringComparison.OrdinalIgnoreCase)
            ? content
            : throw new DocumentIntegrityException(blobName);
    }

    /// <summary>Development only: in production the container is provisioned with the infrastructure.</summary>
    public Task CreateContainerAsync(CancellationToken cancellationToken) =>
        Container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

    /// <summary>The one hash format every service uses: SHA-256 as upper-case hex.</summary>
    private static string Hash(byte[] content) => Convert.ToHexString(SHA256.HashData(content));
}

/// <summary>
/// A stored document is not the one that was submitted. Whoever reads it must not go on: verifying or reviewing a
/// changed image would decide on the wrong evidence.
/// </summary>
public sealed class DocumentIntegrityException(string blobName)
    : Exception($"Document '{blobName}' does not match the SHA-256 recorded when it was submitted.");
