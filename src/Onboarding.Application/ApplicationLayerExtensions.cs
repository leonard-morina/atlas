using Atlas.Onboarding.Application.Behaviors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Atlas.Onboarding.Application;

/// <summary>
/// The use cases as MediatR commands. Handlers carry them out; behaviours wrap every command
/// (logging now, a database transaction once there is a database).
/// </summary>
public static class ApplicationLayerExtensions
{
    public static IHostApplicationBuilder AddApplicationLayer(this IHostApplicationBuilder builder)
    {
        builder.Services.AddMediatR(options =>
        {
            options.RegisterServicesFromAssemblyContaining(typeof(ApplicationLayerExtensions));
            options.AddOpenBehavior(typeof(LoggingBehavior<,>));
        });

        builder.Services.TryAddSingleton(TimeProvider.System);

        return builder;
    }
}
