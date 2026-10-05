using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.IntegrationTests;

/// <summary>
/// The whole system, started once for all tests from the real AppHost: every service as a process, against the SQL
/// Server, RabbitMQ, Azurite and Redis from docker compose. Nothing is replaced by an in-memory substitute; only the
/// external providers are the stand-ins in src/Stubs, as when running locally.
/// By default it runs as the AppHost instance "tests": its own databases (atlas_*_tests) and RabbitMQ virtual host on the
/// same containers, so local development data is never touched and the tests can run while development is running.
/// Data is kept between runs (every test makes its own applicant, so earlier rows never get in the way), and a run can
/// be looked at in the databases afterwards. ATLAS_TEST_RESET=true starts from empty databases instead, built by the
/// migrations. ATLAS_TEST_INSTANCE picks another instance, or "dev" the development setup itself (never reset; stop a
/// locally running AppHost first, both would use the same queues). "dev" rather than empty: Windows deletes a variable
/// set to empty.
/// </summary>
[TestClass]
public static class AtlasApp
{
    private const string DefaultInstance = "tests";

    private static readonly string[] Services =
        ["stubs", "onboarding-api", "verification-worker", "accounts-worker", "gateway"];

    private static DistributedApplication? _app;

    private static DistributedApplication App =>
        _app ?? throw new InvalidOperationException("The application is not started.");

    /// <summary>The public API, as mobile calls it.</summary>
    public static HttpClient Gateway { get; private set; } = null!;

    /// <summary>The provider stand-ins, for what they received and did (core banking's accounts, for one).</summary>
    public static HttpClient Stubs { get; private set; } = null!;

    [AssemblyInitialize]
    public static async Task StartAsync(TestContext context)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationTokenSource.Token);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));

        var instance = Environment.GetEnvironmentVariable("ATLAS_TEST_INSTANCE") switch
        {
            null or "" => DefaultInstance,
            "dev" => "", // the development setup
            var name => name,
        };
        var reset = Environment.GetEnvironmentVariable("ATLAS_TEST_RESET") is "true";
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>(
            [$"--Atlas:Instance={instance}", $"--Atlas:ResetInstance={reset}"], timeout.Token);

        // There is no Aspire dashboard under the test host, so nothing listens for the services' telemetry. Without this,
        // every service waits about ten seconds on shutdown trying to send its last batch. Logs still go to Seq.
        foreach (var project in builder.Resources.OfType<ProjectResource>())
        {
            builder.CreateResourceBuilder(project).WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "");
        }

        // The tests submit many applications from one address; the limit has its own switch (docs/decisions.md).
        builder.CreateResourceBuilder<ProjectResource>("gateway")
            .WithEnvironment("Gateway__RateLimiting__Enabled", "false");

        // A slow provider on a faster clock, in the same order: the submission stops waiting (5 s) before World-Check
        // answers (8 s). 5 s, not less: every other test expects its submission decided within that wait.
        builder.CreateResourceBuilder<ProjectResource>("onboarding-api")
            .WithEnvironment("Onboarding__DecisionWait__Budget", "00:00:05");

        // Account opening on a faster clock, in the same order as in production: an ordinary OpenAccount takes 2 s (long
        // enough to stop a worker in the middle of one), our call gives up after 5 s, core banking's slow case opens the
        // account anyway after 8 s, and its lookup replica shows it 3 s after that.
        // The end-of-day window is off, or the tests waiting for an account would fail between 22:00 and 06:00.
        builder.CreateResourceBuilder<ProjectResource>("accounts-worker")
            .WithEnvironment("Accounts__Opening__OpenAccountTimeout", "00:00:05")
            .WithEnvironment("Accounts__Opening__LookupTimeout", "00:00:03")
            .WithEnvironment("Accounts__Opening__LeaseMargin", "00:00:05")
            .WithEnvironment("Accounts__Opening__ConfirmationDelay", "00:00:02")
            .WithEnvironment("Accounts__Opening__BusyRetryDelay", "00:00:01")
            .WithEnvironment("Accounts__Opening__ObserveEndOfDayWindow", "false");
        builder.CreateResourceBuilder<ProjectResource>("stubs")
            .WithEnvironment("Stubs__SlowResponseDelay", "00:00:08")
            .WithEnvironment("Stubs__CoreBanking__OpenAccountDelay", "00:00:02")
            .WithEnvironment("Stubs__CoreBanking__TimeoutScenarioDelay", "00:00:08")
            .WithEnvironment("Stubs__CoreBanking__ReplicaDelay", "00:00:03")
            .WithEnvironment("Stubs__CoreBanking__EnforceEndOfDay", "false");

        _app = await builder.BuildAsync(timeout.Token);
        await _app.StartAsync(timeout.Token);

        foreach (var service in Services)
        {
            await _app.ResourceNotifications.WaitForResourceHealthyAsync(service, timeout.Token);
        }

        Gateway = _app.CreateHttpClient("gateway");
        Stubs = _app.CreateHttpClient("stubs");
    }

    [AssemblyCleanup]
    public static async Task StopAsync()
    {
        Gateway?.Dispose();
        Stubs?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    /// <summary>Stops a service as a deployment would (a graceful stop signal), and waits until it has exited.</summary>
    public static async Task StopServiceAsync(string service)
    {
        var result = await App.Services.GetRequiredService<ResourceCommandService>()
            .ExecuteCommandAsync(service, KnownResourceCommands.StopCommand);
        Assert.IsTrue(result.Success, $"Stopping {service}: {result.Message}");
    }

    /// <summary>Starts a stopped service again and waits until it is healthy.</summary>
    public static async Task StartServiceAsync(string service)
    {
        var result = await App.Services.GetRequiredService<ResourceCommandService>()
            .ExecuteCommandAsync(service, KnownResourceCommands.StartCommand);
        Assert.IsTrue(result.Success, $"Starting {service}: {result.Message}");
        await App.ResourceNotifications.WaitForResourceHealthyAsync(service);
    }

    /// <summary>A connection to a service's own database, for checking what was stored.</summary>
    public static async Task<SqlConnection> OpenDatabaseAsync(string connectionName)
    {
        var connection = new SqlConnection(await App.GetConnectionStringAsync(connectionName));
        await connection.OpenAsync();
        return connection;
    }
}
