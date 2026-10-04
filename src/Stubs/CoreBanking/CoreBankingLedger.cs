using System.Collections.Concurrent;

namespace Atlas.Stubs.CoreBanking;

public sealed record StubAccount(
    string AccountNumber,
    string Market,
    string? NationalId,
    string? PassportNumber,
    string FirstName,
    string LastName,
    string? ChannelReference,
    DateTimeOffset OpenedAt,
    DateTimeOffset VisibleAt);

/// <summary>
/// The stand-in's state: every account it opened (duplicates included, so a test can prove there are none), the
/// calls in flight per market and the highest number seen at once. In memory; resets with the process.
/// </summary>
public sealed class CoreBankingLedger(TimeProvider time)
{
    private readonly ConcurrentQueue<StubAccount> _accounts = new();
    private readonly ConcurrentDictionary<string, int> _attempts = new();
    private readonly Dictionary<string, int> _inFlight = [];
    private readonly Dictionary<string, int> _peakInFlight = [];
    private readonly Lock _gate = new();
    private int _nextAccount;

    /// <summary>Takes one of the market's call slots, or returns false when they are all taken.</summary>
    public bool TryEnter(string market, int available)
    {
        lock (_gate)
        {
            var current = _inFlight.GetValueOrDefault(market);
            if (current >= available)
            {
                return false;
            }

            _inFlight[market] = current + 1;
            _peakInFlight[market] = Math.Max(_peakInFlight.GetValueOrDefault(market), current + 1);
            return true;
        }
    }

    public void Exit(string market)
    {
        lock (_gate)
        {
            _inFlight[market] = _inFlight.GetValueOrDefault(market) - 1;
        }
    }

    /// <summary>How many times this request has been tried, this attempt included.</summary>
    public int CountAttempt(string key) => _attempts.AddOrUpdate(key, 1, (_, count) => count + 1);

    public StubAccount Open(
        string market,
        string? nationalId,
        string? passportNumber,
        string firstName,
        string lastName,
        string? channelReference,
        TimeSpan replicaDelay)
    {
        var now = time.GetUtcNow();
        var account = new StubAccount(
            $"{market}{Interlocked.Increment(ref _nextAccount):D10}",
            market, nationalId, passportNumber, firstName, lastName, channelReference, now, now + replicaDelay);

        _accounts.Enqueue(account);
        return account;
    }

    /// <summary>What the reporting replica shows: only accounts opened long enough ago.</summary>
    public IEnumerable<StubAccount> VisibleFor(string market, string nationalId)
    {
        var now = time.GetUtcNow();
        return _accounts.Where(account =>
            account.Market == market && account.NationalId == nationalId && account.VisibleAt <= now);
    }

    public object Snapshot()
    {
        lock (_gate)
        {
            var accounts = _accounts.ToArray();
            return new
            {
                Accounts = accounts,
                // A person with more than one account in a market: what the integration must never cause.
                Duplicates = accounts
                    .GroupBy(account => (account.Market, Identity: account.NationalId ?? account.PassportNumber))
                    .Where(group => group.Count() > 1)
                    .Select(group => new { group.Key.Market, group.Key.Identity, Accounts = group.Count() }),
                InFlightNow = new Dictionary<string, int>(_inFlight),
                PeakInFlight = new Dictionary<string, int>(_peakInFlight),
            };
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _accounts.Clear();
            _attempts.Clear();
            _peakInFlight.Clear();
        }
    }
}
