using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Atlas.AppHost;

/// <summary>
/// Which copy of the system runs on the local containers, from "Atlas:Instance". Empty (the default) is local
/// development. A name gives a separate copy on the same containers: its own databases and RabbitMQ virtual host, so it
/// can run next to development without either taking the other's messages, and its logs tagged in Seq. Blob storage and
/// Redis are shared: blob names contain the application id, and the tests switch the rate limiter off.
/// A named instance keeps its data between starts unless "Atlas:ResetInstance" is true; then it begins empty and its
/// databases are built by the migrations. The integration tests run as the instance "tests" and keep their data unless
/// asked to reset (ATLAS_TEST_RESET=true).
/// </summary>
internal sealed partial class AtlasInstance
{
    private AtlasInstance(string name) => Name = name;

    public string Name { get; }

    public bool IsDevelopment => Name.Length == 0;

    public string VirtualHost => IsDevelopment ? "/" : $"atlas-{Name}";

    public static AtlasInstance From(string? name)
    {
        name ??= "";

        // The name becomes part of database and virtual host names.
        if (name.Length > 0 && !ValidName().IsMatch(name))
        {
            throw new ArgumentException($"Atlas:Instance '{name}' must be lowercase letters and digits only.");
        }

        return new AtlasInstance(name);
    }

    public string Database(string name) => IsDevelopment ? name : $"{name}_{Name}";

    /// <summary>
    /// Deletes everything this instance left behind (its databases and virtual host) and creates an empty virtual host.
    /// Never for development, whose data is always kept.
    /// </summary>
    public async Task ResetAsync(string sqlConnectionString, IEnumerable<string> databases, RabbitMqManagement rabbitMq)
    {
        if (IsDevelopment)
        {
            throw new InvalidOperationException("Local development data is never reset; only a named instance can be.");
        }

        await using (var sql = new SqlConnection(
                         new SqlConnectionStringBuilder(sqlConnectionString) { InitialCatalog = "master" }
                             .ConnectionString))
        {
            await sql.OpenAsync();
            foreach (var database in databases)
            {
                await using var drop = sql.CreateCommand();
                drop.CommandText =
                    """
                    IF DB_ID(@database) IS NOT NULL
                    BEGIN
                        DECLARE @name nvarchar(300) = QUOTENAME(@database);
                        EXEC('ALTER DATABASE ' + @name + ' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + @name);
                    END
                    """;
                drop.Parameters.AddWithValue("@database", database);
                await drop.ExecuteNonQueryAsync();
            }
        }

        await rabbitMq.RecreateVirtualHostAsync(VirtualHost);
    }

    /// <summary>Makes sure the instance's virtual host exists (the services create their databases themselves).</summary>
    public Task EnsureCreatedAsync(RabbitMqManagement rabbitMq) =>
        IsDevelopment ? Task.CompletedTask : rabbitMq.EnsureVirtualHostAsync(VirtualHost);

    [GeneratedRegex("^[a-z0-9]+$")]
    private static partial Regex ValidName();
}

/// <summary>RabbitMQ's management API, for the virtual hosts of named instances.</summary>
internal sealed class RabbitMqManagement(int port, string user, string password)
{
    public async Task RecreateVirtualHostAsync(string virtualHost)
    {
        using var http = Client();
        var name = Uri.EscapeDataString(virtualHost);

        // Deleting the virtual host deletes its queues and exchanges with it; 404 means there was none.
        using var deleted = await http.DeleteAsync($"vhosts/{name}");
        if (!deleted.IsSuccessStatusCode && deleted.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            deleted.EnsureSuccessStatusCode();
        }

        await CreateAsync(http, name);
    }

    /// <summary>Creating an existing virtual host, or granting a permission again, changes nothing.</summary>
    public async Task EnsureVirtualHostAsync(string virtualHost)
    {
        using var http = Client();
        await CreateAsync(http, Uri.EscapeDataString(virtualHost));
    }

    private async Task CreateAsync(HttpClient http, string name)
    {
        (await http.PutAsync($"vhosts/{name}", null)).EnsureSuccessStatusCode();
        (await http.PutAsync(
                $"permissions/{name}/{Uri.EscapeDataString(user)}",
                new StringContent("""{"configure":".*","write":".*","read":".*"}""", Encoding.UTF8,
                    "application/json")))
            .EnsureSuccessStatusCode();
    }

    private HttpClient Client()
    {
        var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}/api/") };
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));
        return http;
    }
}
