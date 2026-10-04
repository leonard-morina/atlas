namespace Atlas.Accounts.Contracts;

/// <summary>Published by Accounts once core banking has opened the applicant's current account.</summary>
/// <param name="AccountNumber">Core banking's account number.</param>
public sealed record AccountOpened(Guid ApplicationId, string Market, string AccountNumber, DateTimeOffset OpenedAt);
