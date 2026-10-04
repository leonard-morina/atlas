using Atlas.Onboarding.Domain.Applications;

namespace Atlas.Onboarding.Application.Persistence;

/// <summary>Stored onboarding applications. Implemented by Infrastructure.</summary>
public interface IOnboardingApplicationRepository
{
    /// <summary>
    /// Where an application stands, and nothing else: four columns, no documents. Cheap enough to poll, which is what
    /// a submission does while it waits for its decision.
    /// </summary>
    Task<ApplicationStatusSnapshot?> FindStatusAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Tracked: for changing an application, followed by <see cref="SaveChangesAsync"/>.</summary>
    Task<OnboardingApplication?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<OnboardingApplication?> FindByIdempotencyKeyAsync(Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Whether this identifier already has an application in the market that blocks a new one.</summary>
    Task<bool> HasBlockingApplicationAsync(string market, ApplicantIdentifier identifier, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a new application, or returns <c>false</c> when a concurrent request was stored first under the
    /// same Idempotency-Key or for the same identifier. The database enforces both: checks made before the
    /// insert cannot see a request still in flight on another replica.
    /// </summary>
    Task<bool> TryAddAsync(OnboardingApplication application, CancellationToken cancellationToken);

    /// <summary>Saves changes to applications loaded for update, with any message published in the same unit of work.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record ApplicationStatusSnapshot(
    Guid ApplicationId,
    ApplicationStatus Status,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? DecidedAt);
