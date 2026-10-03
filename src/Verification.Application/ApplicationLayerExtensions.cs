using Atlas.Verification.Application.VerifyApplication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Atlas.Verification.Application;

/// <summary>
/// Verification has one use case and one entry point, the message consumer, whose pipeline (retry, inbox, outbox)
/// MassTransit already provides. So the handler is called directly; a mediator would add a second pipeline.
/// </summary>
public static class ApplicationLayerExtensions
{
    public static IHostApplicationBuilder AddApplicationLayer(this IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<VerifyApplicationHandler>();
        builder.Services.TryAddSingleton(TimeProvider.System);

        return builder;
    }
}
