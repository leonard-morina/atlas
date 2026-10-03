using Atlas.Verification.Application.VerifyApplication;

namespace Atlas.Verification.Application.Abstractions;

/// <summary>Reads submitted images from Onboarding's document storage (read-only access). Implemented by Infrastructure.</summary>
public interface IDocumentReader
{
    /// <summary>
    /// Returns the image only if it still has the SHA-256 recorded when it was submitted; otherwise fails, because
    /// verifying a changed image would decide on the wrong evidence.
    /// </summary>
    Task<byte[]> ReadAsync(DocumentLocation location, CancellationToken cancellationToken);
}
