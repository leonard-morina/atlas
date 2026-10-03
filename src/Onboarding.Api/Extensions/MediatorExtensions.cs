using Atlas.Onboarding.Api.Behaviors;

namespace Atlas.Onboarding.Api.Extensions;

/// <summary>
/// MediatR: endpoints send commands, handlers carry out the use cases, and behaviours wrap every command
/// (logging now; a database transaction once there is a database).
/// </summary>
public static class MediatorExtensions
{
    public static IHostApplicationBuilder AddMediatorPipeline(this IHostApplicationBuilder builder)
    {
        builder.Services.AddMediatR(options =>
        {
            options.RegisterServicesFromAssemblyContaining<LoggingBehavior<object, object>>();
            options.AddOpenBehavior(typeof(LoggingBehavior<,>));
        });

        return builder;
    }
}
