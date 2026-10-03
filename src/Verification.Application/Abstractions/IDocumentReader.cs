using Atlas.Verification.Application.VerifyApplication;

namespace Atlas.Verification.Application.Abstractions;

/// <summary>Reads submitted images from Onboarding's document storage (read-only access). Implemented by Infrastructure.</summary>
public interface IDocumentReader
{
    /// <exception cref="DocumentIntegrityException">The stored image does not have the expected SHA-256.</exception>
    Task<byte[]> ReadAsync(DocumentLocation location, CancellationToken cancellationToken);
}

/// <summary>
/// The stored image is not the one that was submitted. Verification refuses to go on: verifying a changed image
/// would approve or reject the wrong evidence.
/// </summary>
public sealed class DocumentIntegrityException(string blobName)
    : Exception($"Document '{blobName}' does not match the SHA-256 recorded when it was submitted.");
