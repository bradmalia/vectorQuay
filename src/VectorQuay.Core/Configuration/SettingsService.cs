using System.Text.Json;

namespace VectorQuay.Core.Configuration;

public sealed class SettingsService
{
    private static readonly HashSet<string> AllowedProtectedModes = new(StringComparer.Ordinal)
    {
        "Do Not Buy",
        "Do Not Sell",
        "Do Not Trade",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly VectorQuayPaths _paths;

    public SettingsService(VectorQuayPaths paths)
    {
        _paths = paths;
    }

    public static SettingsService CreateForCurrentUser(string? baseDirectory = null)
    {
        return new SettingsService(VectorQuayPaths.Resolve(baseDirectory: baseDirectory));
    }

    public SettingsSnapshot Load()
    {
        var defaultSettings = AppSettings.CreateDefault();
        var templateSettings = TryReadSettings(_paths.TemplatePath) ?? defaultSettings;
        var localSettings = TryReadSettings(_paths.SettingsPath) ?? templateSettings;

        localSettings.General.ValuationCurrency = "USD";

        var secretsFromFile = _paths.SecretsExists
            ? SecretFileParser.ParseFile(_paths.SecretsPath)
            : new Dictionary<string, string>(StringComparer.Ordinal);

        var secretStatuses = SecretNames.All.ToDictionary(
            name => name,
            name => ResolveSecretStatus(name, secretsFromFile),
            StringComparer.Ordinal);

        return new SettingsSnapshot
        {
            Paths = _paths,
            Settings = localSettings,
            SecretStatuses = secretStatuses,
            ValidationMessages = Validate(localSettings, secretStatuses, _paths),
        };
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_paths.SettingsPath)!);
        settings.General.ValuationCurrency = "USD";
        File.WriteAllText(_paths.SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public IReadOnlyList<string> Validate(AppSettings settings)
    {
        var secretsFromFile = _paths.SecretsExists
            ? SecretFileParser.ParseFile(_paths.SecretsPath)
            : new Dictionary<string, string>(StringComparer.Ordinal);
        var secretStatuses = SecretNames.All.ToDictionary(
            name => name,
            name => ResolveSecretStatus(name, secretsFromFile),
            StringComparer.Ordinal);
        return Validate(settings, secretStatuses, _paths);
    }

    private static IReadOnlyList<string> Validate(
        AppSettings settings,
        IReadOnlyDictionary<string, SecretStatus> secretStatuses,
        VectorQuayPaths paths)
    {
        var messages = new List<string>
        {
            $"Info: template path {(paths.TemplateExists ? "resolved" : "missing")} at {paths.TemplatePath}",
            $"Info: local settings {(paths.SettingsExists ? "exist" : "not created yet")} at {paths.SettingsPath}",
            $"Info: external secret file {(paths.SecretsExists ? "exists" : "is missing")} at {paths.SecretsPath}",
            "Info: shell startup is valid without secrets; missing secrets are warnings until exchange integration starts.",
        };

        if (settings.General.ValuationCurrency != "USD")
        {
            messages.Add("Warning: valuation currency is forced back to USD by the Phase 1 baseline.");
        }

        if (settings.Policy.ApprovedCandidates.Count == 0)
        {
            messages.Add("Blocking: at least one approved candidate asset should remain configured.");
        }

        if (!AllowedProtectedModes.Contains(settings.Policy.ProtectedBtcMode))
        {
            messages.Add("Blocking: BTC protected mode must be one of Do Not Buy, Do Not Sell, or Do Not Trade.");
        }

        if (!AllowedProtectedModes.Contains(settings.Policy.ProtectedEthMode))
        {
            messages.Add("Blocking: ETH protected mode must be one of Do Not Buy, Do Not Sell, or Do Not Trade.");
        }

        if (settings.Risk.CustomMaxPositionPct <= 0 ||
            settings.Risk.CustomDailyLossPct <= 0 ||
            settings.Risk.CustomTurnoverPct <= 0)
        {
            messages.Add("Blocking: custom threshold values must be positive numbers.");
        }

        if (string.IsNullOrWhiteSpace(settings.General.ReleaseFeedUrl))
        {
            messages.Add("Warning: no GitHub Releases feed is configured yet for manual update checks.");
        }

        foreach (var name in SecretNames.All)
        {
            var status = secretStatuses[name];
            if (status.Source == SecretSource.Missing)
            {
                messages.Add($"Warning: {name} is not configured yet.");
            }
            else
            {
                messages.Add($"Info: {name} is available via {status.Source}.");
            }
        }

        messages.Add("Info: supported top-level views are Home, Configuration, Assets, Policies, Portfolio, Activity, Performance, Risk & Thresholds, Sources, Alerts, and About.");
        return messages;
    }

    private static AppSettings? TryReadSettings(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
    }

    private static SecretStatus ResolveSecretStatus(string name, IReadOnlyDictionary<string, string> secretsFromFile)
    {
        var environmentValue = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return new SecretStatus
            {
                Name = name,
                Source = SecretSource.Environment,
            };
        }

        if (secretsFromFile.TryGetValue(name, out var secretFileValue) &&
            !string.IsNullOrWhiteSpace(secretFileValue))
        {
            return new SecretStatus
            {
                Name = name,
                Source = SecretSource.SecretFile,
            };
        }

        return new SecretStatus
        {
            Name = name,
            Source = SecretSource.Missing,
        };
    }
}
