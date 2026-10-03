using Atlas.Verification.Domain;

namespace Atlas.Verification.Application.Abstractions;

/// <summary>Stored verifications. Implemented by Infrastructure.</summary>
public interface IVerificationRepository
{
    Task<bool> ExistsAsync(Guid applicationId, CancellationToken cancellationToken);

    void Add(ApplicationVerification verification);

    /// <summary>Saves the verification together with any message published in the same unit of work (outbox).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
