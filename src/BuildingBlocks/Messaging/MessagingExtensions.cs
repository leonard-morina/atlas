using System.Text.Json.Serialization;
using MassTransit;
using MassTransit.Logging;
using MassTransit.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Atlas.Messaging;

public static class MessagingExtensions
{
    private const string BrokerConnectionName = "rabbitmq";

    /// <summary>
    /// RabbitMQ through MassTransit, with a transactional outbox and inbox in the service's own database.
    /// A published message is stored in the same SaveChanges as the data it describes and delivered afterwards,
    /// so neither exists without the other; a consumed message is recorded, so a redelivery is not handled twice.
    /// Swapping RabbitMQ for Azure Service Bus in production changes only the transport line below.
    /// </summary>
    public static IHostApplicationBuilder AddMessaging<TDbContext>(
        this IHostApplicationBuilder builder,
        Action<IBusRegistrationConfigurator>? configure = null)
        where TDbContext : DbContext
    {
        var broker = builder.Configuration.GetConnectionString(BrokerConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{BrokerConnectionName}' is missing.");

        builder.Services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();

            bus.AddEntityFrameworkOutbox<TDbContext>(outbox =>
            {
                outbox.UseSqlServer();
                outbox.UseBusOutbox();
            });

            bus.AddConfigureEndpointsCallback((context, name, endpoint) =>
            {
                // A broadcast only signals this process; it has no database work to make atomic or to deduplicate.
                if (IsBroadcastEndpoint(name))
                {
                    return;
                }

                // Transient failures (a provider briefly down or too slow, a database blip) get a few spaced
                // attempts; after that the message moves to the endpoint's _error queue for operations to look at.
                // Longer delayed redelivery needs a message scheduler: RabbitMQ only with a plugin, Azure Service
                // Bus natively (production).
                endpoint.UseMessageRetry(retry => retry.Intervals(
                    TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30)));

                // Inside the retry, so every attempt is its own transaction.
                endpoint.UseEntityFrameworkOutbox<TDbContext>(context);
            });

            configure?.Invoke(bus);

            bus.UsingRabbitMq((context, rabbit) =>
            {
                rabbit.Host(new Uri(broker));

                // Readable messages in the broker: "IdCard", not 1.
                rabbit.ConfigureJsonSerializerOptions(options =>
                {
                    options.Converters.Add(new JsonStringEnumConverter());
                    return options;
                });

                rabbit.ConfigureEndpoints(context);
            });
        });

        // Start the web host only once the bus is connected, so /health never reports a bus that is still starting
        // ("Not ready: not started"), and a broker that cannot be reached fails the start instead of a later check.
        builder.Services.Configure<MassTransitHostOptions>(host =>
        {
            host.WaitUntilStarted = true;
            host.StartTimeout = TimeSpan.FromSeconds(30);
        });

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource(DiagnosticHeaders.DefaultListenerName))
            .WithMetrics(metrics => metrics.AddMeter(InstrumentationOptions.MeterName));

        return builder;
    }

    /// <summary>
    /// Registers a consumer that every running instance of the service receives, not just one of them: each instance gets
    /// its own queue, deleted when the instance stops. For signals about work another instance did, such as waking a
    /// request this instance holds. Missed while an instance is down, so only for hints that have a fallback.
    /// </summary>
    public static void AddBroadcastConsumer<TConsumer>(this IBusRegistrationConfigurator bus)
        where TConsumer : class, IConsumer =>
        bus.AddConsumer<TConsumer>().Endpoint(endpoint =>
        {
            endpoint.Name = $"{KebabCaseEndpointNameFormatter.Instance.Consumer<TConsumer>()}{BroadcastMarker}{InstanceName}";
            endpoint.Temporary = true;
        });

    private const string BroadcastMarker = "-broadcast-";

    // Unique per process, so two instances on one machine get a queue each.
    private static readonly string InstanceName = $"{Environment.MachineName}-{Environment.ProcessId}".ToLowerInvariant();

    private static bool IsBroadcastEndpoint(string name) => name.Contains(BroadcastMarker, StringComparison.Ordinal);
}
