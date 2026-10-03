namespace Atlas.AppHost;

internal static class ResourceBuilderExtensions
{
    /// <summary>
    /// Trust the gateway's X-Forwarded-* headers (real client IP, public host). Never use on the gateway:
    /// at the edge it would let clients spoof their IP.
    /// </summary>
    public static IResourceBuilder<T> WithForwardedHeaders<T>(this IResourceBuilder<T> builder)
        where T : IResourceWithEnvironment =>
        builder.WithEnvironment("ASPNETCORE_FORWARDEDHEADERS_ENABLED", "true");
}
