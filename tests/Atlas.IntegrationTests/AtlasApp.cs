using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Data.SqlClient;

namespace Atlas.IntegrationTests;

/// <summary>
/// The whole system, started once for all tests from the real AppHost: every service as a process, against the SQL
/// Server, RabbitMQ, Azurite and Redis from docker compose. Nothing is replaced by an in-memory substitute; only the
/// external providers are the stand-ins in src/Stubs, as when running locally.
/// Stop a locally running AppHost first: both would consume from the same RabbitMQ queues.
/// </summary>
[TestClass]
public static class AtlasApp
{
    private static readonly string[] Services = ["stubs", "onboarding-api", "verification-worker", "accounts-worker", "gateway"];

    private static DistributedApplication? _app;

    private static DistributedApplication App => _app ?? throw new InvalidOperationException("The application is not started.");

    /// <summary>The public API, as mobile calls it.</summary>
    public static HttpClient Gateway { get; private set; } = null!;

    [AssemblyInitialize]
    public static async Task StartAsync(TestContext context)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationTokenSource.Token);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>(timeout.Token);

        // The tests submit many applications from one address; the limit has its own switch (docs/decisions.md).
        builder.CreateResourceBuilder<ProjectResource>("gateway").WithEnvironment("Gateway__RateLimiting__Enabled", "false");

        _app = await builder.BuildAsync(timeout.Token);
        await _app.StartAsync(timeout.Token);

        foreach (var service in Services)
        {
            await _app.ResourceNotifications.WaitForResourceHealthyAsync(service, timeout.Token);
        }

        Gateway = _app.CreateHttpClient("gateway");
    }

    [AssemblyCleanup]
    public static async Task StopAsync()
    {
        Gateway?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    /// <summary>A connection to a service's own database, for checking what was stored.</summary>
    public static async Task<SqlConnection> OpenDatabaseAsync(string connectionName)
    {
        var connection = new SqlConnection(await App.GetConnectionStringAsync(connectionName));
        await connection.OpenAsync();
        return connection;
    }
}
