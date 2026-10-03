using Atlas.Onboarding.Domain.Applications;

namespace Atlas.Onboarding.Application.Persistence;

/// <summary>Stored onboarding applications. Implemented by Infrastructure.</summary>
public interface IOnboardingApplicationRepository
{
    Task<OnboardingApplication?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<OnboardingApplication?> FindByIdempotencyKeyAsync(Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Whether this identifier already has an application in the market that blocks a new one.</summary>
    Task<bool> HasBlockingApplicationAsync(string market, ApplicantIdentifier identifier, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a new application, or returns <c>false</c> when a concurrent request was stored first under the
    /// same Idempotency-Key or for the same identifier. The database enforces both: checks made before the
    /// insert cannot see a request still in flight on another replica.
    /// </summary>
    Task<bool> TryAddAsync(OnboardingApplication application, CancellationToken cancellationToken);
}
