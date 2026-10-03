using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Atlas.Onboarding.Application.Behaviors;

/// <summary>
/// Logs every command by name and duration. Never the payload: commands carry personal data (Compliance §2).
/// Failures are logged once, by the exception handler.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var command = typeof(TRequest).Name;
        logger.LogInformation("Handling {Command}", command);

        var started = Stopwatch.GetTimestamp();
        var response = await next();

        logger.LogInformation(
            "Handled {Command} in {ElapsedMilliseconds:0} ms",
            command,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);

        return response;
    }
}
