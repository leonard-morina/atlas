using Atlas.Accounts.Domain.Openings;

namespace Atlas.Accounts.Application.Abstractions;

/// <summary>
/// Core banking's ACCOUNTS service (CBS integration page v3.1). Reports what happened to a call rather than
/// throwing, because each outcome means something different for an operation without idempotency.
/// </summary>
public interface ICoreBanking
{
    /// <summary>Opens a current account. Never retried here: a second call opens a second account (CBS §3).</summary>
    /// <returns>The new account number when answered.</returns>
    Task<CoreBankingResult<string>> OpenAccountAsync(
        string market,
        Customer customer,
        string channelReference,
        CancellationToken cancellationToken);

    /// <summary>The customer's accounts, by national ID only, from a replica up to 60 seconds behind (CBS §2).</summary>
    Task<CoreBankingResult<IReadOnlyList<CustomerAccount>>> FindCustomerAccountsAsync(
        string market,
        string nationalId,
        CancellationToken cancellationToken);
}

public sealed record CustomerAccount(string AccountNumber, string? ChannelReference);

/// <summary>What became of a call to core banking.</summary>
public abstract record CoreBankingResult<T>
{
    private CoreBankingResult()
    {
    }

    /// <summary>Core banking answered.</summary>
    public sealed record Answered(T Value) : CoreBankingResult<T>;

    /// <summary>Core banking refused the call with a documented fault (CBS §7).</summary>
    public sealed record Faulted(CoreBankingFault Fault, string Message) : CoreBankingResult<T>;

    /// <summary>The request never reached core banking (no connection was made): certainly nothing happened.</summary>
    public sealed record NotDelivered(string Detail) : CoreBankingResult<T>;

    /// <summary>
    /// The request may have reached core banking, but no usable answer came back: a timeout, a dropped connection,
    /// an undocumented fault or reply. Whether anything happened is unknown (CBS §4).
    /// </summary>
    public sealed record Unanswered(string Detail) : CoreBankingResult<T>;
}

public enum CoreBankingFault
{
    /// <summary>EOD_IN_PROGRESS: the market is in its end-of-day window. Safe to retry after it.</summary>
    EndOfDayInProgress,

    /// <summary>CONCURRENCY_LIMIT: the market's ceiling of calls in flight was reached. Safe to retry.</summary>
    ConcurrencyLimit,

    /// <summary>VALIDATION: rejected before processing. Not safe to retry; the request must change.</summary>
    Validation,
}
