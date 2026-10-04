namespace Atlas.Onboarding.Contracts;

/// <summary>
/// Published by Onboarding once an application's decision is recorded (approved, rejected, referred or awaiting a branch
/// visit). A notification: what was decided is read from Onboarding, which keeps it out of the broker. Sent through the
/// outbox, so it is only delivered once the decision is committed.
/// </summary>
public sealed record ApplicationDecided(Guid ApplicationId, DateTimeOffset DecidedAt);
