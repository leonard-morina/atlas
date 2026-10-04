using System.Collections.Concurrent;
using Atlas.Accounts.Application.Abstractions;
using Atlas.Accounts.Domain.Markets;
using Atlas.Accounts.Domain.Openings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Accounts.Application.ProcessOpenings;

/// <summary>
/// Works through the queued openings. Per market with work due and outside its end-of-day window, it claims as many
/// openings as this service's share of the market's call slots allows and calls core banking for each in the
/// background. Then it sleeps until the next opening is due, a lease runs out, or new work arrives in this instance;
/// at most <see cref="AccountOpeningOptions.IdlePollInterval"/>, for work another instance queued or freed. The claim
/// is atomic in the database, so any number of instances can run this.
/// </summary>
public sealed class AccountOpeningProcessor(
    IServiceScopeFactory scopes,
    OpeningSignal signal,
    IOptions<AccountOpeningOptions> options,
    TimeProvider time,
    ILogger<AccountOpeningProcessor> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, Task> _calls = new();

    private AccountOpeningOptions Options => options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var untilNextWork = Options.IdlePollInterval;
                try
                {
                    untilNextWork = await PollAsync(stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    // A database blip: the next poll tries again.
                    logger.LogError(exception, "Polling for account openings failed");
                }

                await signal.WaitAsync(untilNextWork, time, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            // Calls cut short by the shutdown still record their outcome (as unknown) before the worker goes.
            await Task.WhenAll(_calls.Values);
        }
    }

    /// <returns>How long until there is work again, as far as this instance can tell.</returns>
    private async Task<TimeSpan> PollAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var openings = scope.ServiceProvider.GetRequiredService<IAccountOpeningRepository>();

        var now = time.GetUtcNow();
        var schedule = await openings.FindScheduleAsync(stoppingToken);

        if (schedule.NextLeaseExpiry <= now)
        {
            await scope.ServiceProvider.GetRequiredService<RecoverLostCallsHandler>().HandleAsync(stoppingToken);
        }

        var nextWork = schedule.NextLeaseExpiry ?? DateTimeOffset.MaxValue;

        foreach (var (market, due) in schedule.NextDueByMarket)
        {
            // A market in its end-of-day window has nothing due before it reopens.
            var startAt = Options.ObserveEndOfDayWindow
                ? Max(due, EndOfDayWindow.NextOpening(now, Options.TimeZoneOf(market)))
                : due;

            if (startAt <= now)
            {
                var claimed = await openings.ClaimDueAsync(
                    market, Options.CallsInFlightPerMarket, now, opening => opening.StartCall(now, LeaseFor(opening)), stoppingToken);

                foreach (var applicationId in claimed)
                {
                    _calls[applicationId] = CallAsync(applicationId, stoppingToken);
                }
            }

            nextWork = Min(nextWork, startAt);
        }

        // Due work left behind because the market's slots are taken is looked at again shortly, or when a call ends.
        return Clamp(nextWork - now, Options.PollInterval, Options.IdlePollInterval);
    }

    private async Task CallAsync(Guid applicationId, CancellationToken stoppingToken)
    {
        // Off the polling loop: an OpenAccount call takes a minute or more.
        await Task.Yield();

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<CallCoreBankingHandler>().HandleAsync(applicationId, stoppingToken);
        }
        catch (Exception exception)
        {
            // The opening stays in flight; its lease runs out and recovery takes it from there.
            logger.LogError(exception, "Account opening call for application {ApplicationId} failed", applicationId);
        }
        finally
        {
            _calls.TryRemove(applicationId, out _);

            // A slot is free, and the opening may be due again (deferred): worth a look now rather than later.
            signal.Notify();
        }
    }

    private TimeSpan LeaseFor(AccountOpening opening) =>
        (opening.Status == OpeningStatus.Queued ? Options.OpenAccountTimeout : Options.LookupTimeout) + Options.LeaseMargin;

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) => left > right ? left : right;

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left < right ? left : right;

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max) =>
        value < min ? min : value > max ? max : value;
}
