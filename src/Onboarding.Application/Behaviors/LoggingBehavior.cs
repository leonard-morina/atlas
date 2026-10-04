using System.Diagnostics;
using Atlas.Onboarding.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Atlas.Onboarding.Application.Behaviors;

/// <summary>
/// Logs every request by name and duration: commands at Information, queries at Debug (see <see cref="IQuery{TResponse}"/>).
/// Never the payload: requests carry personal data (Compliance §2). Failures are logged once, by the exception handler.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Decided once per request type, not on every call.
    private static readonly LogLevel Level = typeof(TRequest).GetInterfaces()
        .Any(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IQuery<>))
        ? LogLevel.Debug
        : LogLevel.Information;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        logger.Log(Level, "Handling {Request}", name);

        var started = Stopwatch.GetTimestamp();
        var response = await next();

        logger.Log(Level, "Handled {Request} in {ElapsedMilliseconds:0} ms", name, Stopwatch.GetElapsedTime(started).TotalMilliseconds);

        return response;
    }
}
