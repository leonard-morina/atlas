using System.Collections.Concurrent;

namespace Atlas.Onboarding.Api.Features.Applications.SubmitApplication;

/// <summary>
/// The submissions this instance is holding while they wait for their decision, so the decision's announcement can wake
/// the right one. Announcements reach every instance (<see cref="Consumers.ApplicationDecidedConsumer"/>); an instance
/// that is not waiting for that application ignores it.
/// </summary>
public sealed class DecisionSignal
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _waiting = new();

    /// <summary>
    /// Completes when this application's decision is announced. Call before reading the application, so a decision
    /// announced in between is not missed, and <see cref="Forget"/> when no longer waiting.
    /// </summary>
    public Task WhenDecided(Guid applicationId) =>
        _waiting.GetOrAdd(applicationId, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task;

    public void Forget(Guid applicationId) => _waiting.TryRemove(applicationId, out _);

    public void Decided(Guid applicationId)
    {
        if (_waiting.TryRemove(applicationId, out var decided))
        {
            decided.TrySetResult();
        }
    }
}
