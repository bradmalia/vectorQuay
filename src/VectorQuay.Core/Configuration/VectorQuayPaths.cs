namespace VectorQuay.Core.Configuration;

public sealed class VectorQuayPaths
{
    public required string ConfigDirectory { get; init; }

    public required string SettingsPath { get; init; }

    public required string SecretsPath { get; init; }

    public required string TemplatePath { get; init; }

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
