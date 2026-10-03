namespace Atlas.AppHost;

/// <summary>
/// Reads the repository-root <c>.env</c> — the same file Docker Compose uses — so ports and
/// passwords are defined once for both the containers and the .NET services.
/// Precedence mirrors Docker Compose: process environment, then <c>.env.local</c>
/// (git-ignored personal overrides), then <c>.env</c>.
/// </summary>
internal sealed class DotEnv
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    private DotEnv() { }

    public static DotEnv Load(string startDirectory)
    {
        var root = FindRepositoryRoot(startDirectory)
            ?? throw new FileNotFoundException(
                $"No .env file found in '{startDirectory}' or any parent directory. " +
                "It lives in the repository root and holds the local infrastructure settings.");

        var env = new DotEnv();
        env.ReadFile(Path.Combine(root, ".env"));
        env.ReadFile(Path.Combine(root, ".env.local"));
        return env;
    }

    public string this[string key] =>
        Environment.GetEnvironmentVariable(key)
        ?? (_values.TryGetValue(key, out var value) ? value : null)
        ?? throw new InvalidOperationException($"'{key}' is not set in .env or the environment.");

    private void ReadFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('"');
            _values[key] = value;
        }
    }

    private static string? FindRepositoryRoot(string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, ".env")))
            {
                return directory.FullName;
            }
        }

        return null;
    }
}
