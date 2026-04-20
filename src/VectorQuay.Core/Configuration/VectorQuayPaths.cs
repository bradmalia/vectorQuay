namespace VectorQuay.Core.Configuration;

public sealed class VectorQuayPaths
{
    public required string ConfigDirectory { get; init; }

    public required string SettingsPath { get; init; }

    public required string SecretsPath { get; init; }

    public required string TemplatePath { get; init; }

    public required string CoinbaseApiKeyJsonPath { get; init; }

    public required string CoinbaseApiKeyJsonTextPath { get; init; }

    public required string DataDirectory { get; init; }

    public required string StateDirectory { get; init; }

    public required string HistoryDirectory { get; init; }

    public required string AuditDirectory { get; init; }

    public required string LastSnapshotPath { get; init; }

    public required string ActivityHistoryPath { get; init; }

    public required string AlertHistoryPath { get; init; }

    public required string PortfolioValueHistoryPath { get; init; }

    public required string AuditEventsPath { get; init; }

    public required string DataSchemaVersionPath { get; init; }

    public bool SettingsExists => File.Exists(SettingsPath);

    public bool SecretsExists => File.Exists(SecretsPath);

    public bool TemplateExists => File.Exists(TemplatePath);

    public static VectorQuayPaths Resolve(
        string? homeDirectory = null,
        string? xdgConfigHome = null,
        string? baseDirectory = null)
    {
        var home = homeDirectory ??
                   Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var xdgHome = xdgConfigHome ??
                      Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ??
                      Path.Combine(home, ".config");
        var configDirectory = Path.Combine(xdgHome, "VectorQuay");

        return new VectorQuayPaths
        {
            ConfigDirectory = configDirectory,
            SettingsPath = Path.Combine(configDirectory, "settings.json"),
            SecretsPath = Path.Combine(configDirectory, "secrets.env"),
            CoinbaseApiKeyJsonPath = Path.Combine(configDirectory, "cdp_api_key.json"),
            CoinbaseApiKeyJsonTextPath = Path.Combine(configDirectory, "cdp_api_key.json.txt"),
            DataDirectory = Path.Combine(configDirectory, "data"),
            StateDirectory = Path.Combine(configDirectory, "data", "state"),
            HistoryDirectory = Path.Combine(configDirectory, "data", "history"),
            AuditDirectory = Path.Combine(configDirectory, "data", "audit"),
            LastSnapshotPath = Path.Combine(configDirectory, "data", "state", "last-snapshot.json"),
            ActivityHistoryPath = Path.Combine(configDirectory, "data", "history", "activity.jsonl"),
            AlertHistoryPath = Path.Combine(configDirectory, "data", "history", "alerts.jsonl"),
            PortfolioValueHistoryPath = Path.Combine(configDirectory, "data", "history", "portfolio-value.jsonl"),
            AuditEventsPath = Path.Combine(configDirectory, "data", "audit", "events.jsonl"),
            DataSchemaVersionPath = Path.Combine(configDirectory, "data", "schema-version.txt"),
            TemplatePath = ResolveTemplatePath(baseDirectory),
        };
    }

    private static string ResolveTemplatePath(string? baseDirectory)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(baseDirectory))
        {
            candidates.Add(baseDirectory);
        }

        candidates.Add(AppContext.BaseDirectory);
        candidates.Add(Directory.GetCurrentDirectory());

        foreach (var candidate in candidates.Distinct(StringComparer.Ordinal))
        {
            var directory = new DirectoryInfo(candidate);
            while (directory is not null)
            {
                var path = Path.Combine(directory.FullName, "config", "templates", "appsettings.template.json");
                if (File.Exists(path))
                {
                    return path;
                }

                directory = directory.Parent;
            }
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "config", "templates", "appsettings.template.json");
    }
}
