using System.Threading.Channels;

namespace Atlas.Accounts.Application.ProcessOpenings;

/// <summary>
/// Wakes the processor early when there is new work in this instance: an opening was queued, or a call ended and freed
/// its slot. Several notifications before the processor wakes count as one.
/// </summary>
public sealed class OpeningSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Notify() => _channel.Writer.TryWrite(true);

    /// <summary>Waits until notified or until <paramref name="timeout"/> has passed, whichever comes first.</summary>
    public async Task WaitAsync(TimeSpan timeout, TimeProvider time, CancellationToken cancellationToken)
    {
        using var timer = new CancellationTokenSource(timeout, time);
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(timer.Token, cancellationToken);

        try
        {
            await _channel.Reader.WaitToReadAsync(wait.Token);
            _channel.Reader.TryRead(out _);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timed out: time to look again anyway.
        }
    }
}
