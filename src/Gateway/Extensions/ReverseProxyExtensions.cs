namespace Atlas.Gateway.Extensions;

/// <summary>
/// YARP reverse proxy. Routes and clusters live in the "ReverseProxy" configuration section;
/// cluster addresses such as <c>http://onboarding-api</c> are resolved by service discovery.
/// </summary>
public static class ReverseProxyExtensions
{
    public static IHostApplicationBuilder AddReverseProxyRoutes(this IHostApplicationBuilder builder)
    {
        builder.Services.AddReverseProxy()
            .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
            .AddServiceDiscoveryDestinationResolver();

        return builder;
    }
}
