namespace Atlas.Accounts.Application.ProcessOpenings;

/// <summary>
/// Accounts' own signal that an opening was queued, published through the outbox, so it is delivered only once the
/// opening is committed, and received by every instance of the service (a broadcast), so whichever instance has a free
/// slot starts it now rather than at its next look. Not a contract: nothing outside Accounts consumes it.
/// </summary>
public sealed record AccountOpeningQueued(Guid ApplicationId, string Market);
