using System.Text.Json;

namespace VectorQuay.Core.Configuration;

public sealed class SettingsService
{
    private static readonly HashSet<string> AllowedProtectedModes = new(StringComparer.Ordinal)
    {
        "Allow Full Trade",
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
        var validationNotes = new List<string>();
        var templateSettings = TryReadSettings(_paths.TemplatePath, "template settings", validationNotes) ?? defaultSettings;
        var localSettings = TryReadSettings(_paths.SettingsPath, "local settings", validationNotes) ?? templateSettings;
        NormalizePolicySettings(localSettings.Policy);
        NormalizeAlertSettings(localSettings.Alerts);

        localSettings.General.ValuationCurrency = "USD";
        if (string.IsNullOrWhiteSpace(localSettings.General.ReleaseFeedUrl))
        {
            localSettings.General.ReleaseFeedUrl = AppSettings.DefaultReleaseFeedUrl;
        }

        var secretsFromFile = LoadSecrets(validationNotes);

        var secretStatuses = SecretNames.All.ToDictionary(
            name => name,
            name => ResolveSecretStatus(name, secretsFromFile),
            StringComparer.Ordinal);

        return new SettingsSnapshot
        {
            Paths = _paths,
            Settings = localSettings,
            SecretStatuses = secretStatuses,
            ValidationMessages = Validate(localSettings, secretStatuses, _paths, validationNotes),
        };
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_paths.SettingsPath)!);
        settings.General.ValuationCurrency = "USD";
        NormalizePolicySettings(settings.Policy);
        NormalizeAlertSettings(settings.Alerts);
        if (string.IsNullOrWhiteSpace(settings.General.ReleaseFeedUrl))
        {
            settings.General.ReleaseFeedUrl = AppSettings.DefaultReleaseFeedUrl;
        }
        File.WriteAllText(_paths.SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public IReadOnlyList<string> Validate(AppSettings settings)
    {
        var validationNotes = new List<string>();
        var secretsFromFile = LoadSecrets(validationNotes);
        var secretStatuses = SecretNames.All.ToDictionary(
            name => name,
            name => ResolveSecretStatus(name, secretsFromFile),
            StringComparer.Ordinal);
        return Validate(settings, secretStatuses, _paths, validationNotes);
    }

    private static IReadOnlyList<string> Validate(
        AppSettings settings,
        IReadOnlyDictionary<string, SecretStatus> secretStatuses,
        VectorQuayPaths paths,
        IReadOnlyList<string>? leadingMessages = null)
    {
        var messages = new List<string>();
        if (leadingMessages is not null)
        {
            messages.AddRange(leadingMessages);
        }

        messages.AddRange(
        [
            $"Info: template path {(paths.TemplateExists ? "resolved" : "missing")} at {paths.TemplatePath}",
            $"Info: local settings {(paths.SettingsExists ? "exist" : "not created yet")} at {paths.SettingsPath}",
            $"Info: external secret file {(paths.SecretsExists ? "exists" : "is missing")} at {paths.SecretsPath}",
            "Info: shell startup is valid without secrets; missing secrets are warnings until exchange integration starts.",
        ]);

        if (messages.Count == 0)
        {
            messages.Add("Info: validation completed.");
        }

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
            messages.Add("Blocking: BTC protected mode must be one of Allow Full Trade, Do Not Buy, Do Not Sell, or Do Not Trade.");
        }

        if (!AllowedProtectedModes.Contains(settings.Policy.ProtectedEthMode))
        {
            messages.Add("Blocking: ETH protected mode must be one of Allow Full Trade, Do Not Buy, Do Not Sell, or Do Not Trade.");
        }

        foreach (var assetPolicy in settings.Policy.AssetPolicies)
        {
            if (string.IsNullOrWhiteSpace(assetPolicy.Asset))
            {
                messages.Add("Blocking: asset policies must include a non-empty asset symbol.");
            }

            if (!AllowedProtectedModes.Contains(assetPolicy.Mode))
            {
                messages.Add($"Blocking: {assetPolicy.Asset} policy mode must be one of Allow Full Trade, Do Not Buy, Do Not Sell, or Do Not Trade.");
            }
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

    private Dictionary<string, string> LoadSecrets(List<string> validationNotes)
    {
        if (!_paths.SecretsExists)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            var parsed = SecretFileParser.ParseWithDiagnostics(File.ReadAllText(_paths.SecretsPath));
            if (parsed.InvalidLines.Count > 0)
            {
                validationNotes.Add($"Warning: secrets.env contains {parsed.InvalidLines.Count} malformed line(s) that were ignored.");
            }

            return parsed.Values;
        }
        catch (Exception ex)
        {
            validationNotes.Add($"Blocking: external secret file could not be read safely ({ex.GetType().Name}).");
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static AppSettings? TryReadSettings(string path, string label, List<string> validationNotes)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            validationNotes.Add($"Blocking: {label} could not be parsed safely ({ex.GetType().Name}).");
            return null;
        }
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

    private static void NormalizePolicySettings(PolicySettings policy)
    {
        policy.ApprovedCandidates = policy.ApprovedCandidates
            .Where(asset => !string.IsNullOrWhiteSpace(asset))
            .Select(asset => asset.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var normalizedPolicies = new Dictionary<string, AssetPolicySettings>(StringComparer.OrdinalIgnoreCase);
        foreach (var assetPolicy in policy.AssetPolicies)
        {
            if (string.IsNullOrWhiteSpace(assetPolicy.Asset))
            {
                continue;
            }

            var asset = assetPolicy.Asset.Trim().ToUpperInvariant();
            var mode = AllowedProtectedModes.Contains(assetPolicy.Mode) ? assetPolicy.Mode : "Allow Full Trade";
            normalizedPolicies[asset] = new AssetPolicySettings
            {
                Asset = asset,
                Mode = mode,
                Notes = assetPolicy.Notes?.Trim() ?? string.Empty,
            };
        }

        foreach (var asset in policy.ApprovedCandidates)
        {
            if (!normalizedPolicies.ContainsKey(asset))
            {
                normalizedPolicies[asset] = new AssetPolicySettings
                {
                    Asset = asset,
                    Mode = asset switch
                    {
                        "BTC" => policy.ProtectedBtcMode,
                        "ETH" => policy.ProtectedEthMode,
                        _ => "Allow Full Trade",
                    },
                    Notes = string.Empty,
                };
            }
        }

        policy.AssetPolicies = normalizedPolicies.Values
            .OrderBy(assetPolicy => assetPolicy.Asset, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedPolicies.TryGetValue("BTC", out var btcPolicy))
        {
            policy.ProtectedBtcMode = btcPolicy.Mode;
        }

        if (normalizedPolicies.TryGetValue("ETH", out var ethPolicy))
        {
            policy.ProtectedEthMode = ethPolicy.Mode;
        }
    }

    private static void NormalizeAlertSettings(AlertSettings alerts)
    {
        alerts.EmailAddress = alerts.EmailAddress?.Trim() ?? string.Empty;
        alerts.SmsNumber = alerts.SmsNumber?.Trim() ?? string.Empty;
        alerts.QuietHours = string.IsNullOrWhiteSpace(alerts.QuietHours) ? "None configured" : alerts.QuietHours.Trim();

        if (alerts.Rules is null || alerts.Rules.Count == 0)
        {
            alerts.Rules = AlertDefaults.CreateDefaultRules();
            return;
        }

        alerts.Rules = alerts.Rules
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Rule))
            .Select(rule => new AlertRuleSettings
            {
                Rule = rule.Rule.Trim(),
                Severity = string.IsNullOrWhiteSpace(rule.Severity) ? "Info" : rule.Severity.Trim(),
                Destination = string.IsNullOrWhiteSpace(rule.Destination) ? "In-App" : rule.Destination.Trim(),
                Enabled = rule.Enabled,
            })
            .ToList();

        if (alerts.Rules.Count == 0)
        {
            alerts.Rules = AlertDefaults.CreateDefaultRules();
        }
    }
}
