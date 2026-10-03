using System.Collections.Concurrent;

namespace Atlas.Stubs.Calls;

public sealed record StubCall(string Provider, string Scenario, DateTimeOffset At);

/// <summary>
/// Every call the stubs received, so a test can check side effects: for example that IDNow was called exactly
/// once for an application even though its message was delivered twice. In memory; resets with the process.
/// </summary>
public sealed class CallLog(TimeProvider time)
{
    private readonly ConcurrentQueue<StubCall> _calls = new();

    public void Record(string provider, string scenario) => _calls.Enqueue(new StubCall(provider, scenario, time.GetUtcNow()));

    public IReadOnlyCollection<StubCall> All => _calls.ToArray();

    public void Clear() => _calls.Clear();
}

public static class CallLogEndpoints
{
    public static IEndpointRouteBuilder MapCallLog(this IEndpointRouteBuilder endpoints)
    {
        var calls = endpoints.MapGroup("/_stub/calls");

        calls.MapGet("", (CallLog log) => new
        {
            IdNow = log.All.Count(call => call.Provider == IdNow.IdNowEndpoints.Provider),
            WorldCheck = log.All.Count(call => call.Provider == WorldCheck.WorldCheckEndpoints.Provider),
            Calls = log.All,
        });

        calls.MapDelete("", (CallLog log) =>
        {
            log.Clear();
            return TypedResults.NoContent();
        });

        return endpoints;
    }
}
