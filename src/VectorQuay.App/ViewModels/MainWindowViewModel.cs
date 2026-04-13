using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VectorQuay.Core.Configuration;

namespace VectorQuay.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private string? _pendingRiskConfirmationTarget;

    public MainWindowViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        AssetRows = [];
        SourceEntries = [];
        ValidationMessages = [];
        AlertItems = [];
        ActivityItems = [];
        PolicyRules = [];
        LoadFromSnapshot(_settingsService.Load());
    }

    public ObservableCollection<AssetRowViewModel> AssetRows { get; }

    public ObservableCollection<SourceEntryViewModel> SourceEntries { get; }

    public ObservableCollection<string> ValidationMessages { get; }

    public ObservableCollection<string> AlertItems { get; }

    public ObservableCollection<string> ActivityItems { get; }

    public ObservableCollection<PolicyRuleViewModel> PolicyRules { get; }

    public IReadOnlyList<string> ProtectedAssetModes { get; } =
    [
        "Do Not Buy",
        "Do Not Sell",
        "Do Not Trade",
    ];

    public string AppVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";

    [ObservableProperty]
    private string shellStatus = "Pre-Integration";

    [ObservableProperty]
    private string currentSection = "Home";

    [ObservableProperty]
    private string shellMessage = "Trading is inactive in Phase 1. The shell is reserved for safe local setup, policy editing, and future read-only Coinbase integration.";

    [ObservableProperty]
    private string currentValuationCurrency = "USD";

    [ObservableProperty]
    private bool usdcSecondaryEnabled = true;

    [ObservableProperty]
    private string approvedCandidatesText = string.Empty;

    [ObservableProperty]
    private string watchlistNote = string.Empty;

    [ObservableProperty]
    private string protectedBtcMode = "Do Not Sell";

    [ObservableProperty]
    private string protectedEthMode = "Do Not Trade";

    [ObservableProperty]
    private string operatorNotes = string.Empty;

    [ObservableProperty]
    private string selectedRiskProfile = "Medium Risk";

    [ObservableProperty]
    private string customMaxPositionPct = "12";

    [ObservableProperty]
    private string customDailyLossPct = "3";

    [ObservableProperty]
    private string customTurnoverPct = "25";

    [ObservableProperty]
    private string configurationSummary = string.Empty;

    [ObservableProperty]
    private string settingsPath = string.Empty;

    [ObservableProperty]
    private string secretsPath = string.Empty;

    [ObservableProperty]
    private string templatePath = string.Empty;

    [ObservableProperty]
    private string coinbaseApiKeyStatus = "Missing";

    [ObservableProperty]
    private string coinbaseApiSecretStatus = "Missing";

    [ObservableProperty]
    private string openAiApiKeyStatus = "Missing";

    [ObservableProperty]
    private string releaseFeedUrl = string.Empty;

    [ObservableProperty]
    private string updateStatus = "Manual update check has not run yet.";

    [ObservableProperty]
    private string updateActionUrl = string.Empty;

    [ObservableProperty]
    private string validationSummary = string.Empty;

    [ObservableProperty]
    private string settingsActionMessage = "Use Save, Validate, and Reset to manage local Phase 1 configuration safely.";

    [ObservableProperty]
    private string riskProfileMessage = "Preset profiles are available now. Direct threshold edits are reserved for Custom mode.";

    [ObservableProperty]
    private string sourceRegistrySummary = string.Empty;

    [ObservableProperty]
    private PolicyRuleViewModel? selectedPolicyRule;

    public bool IsHomeSection => CurrentSection == "Home";

    public bool IsConfigurationSection => CurrentSection == "Configuration";

    public bool IsAssetsSection => CurrentSection == "Assets";

    public bool IsPoliciesSection => CurrentSection == "Policies";

    public bool IsPortfolioSection => CurrentSection == "Portfolio";

    public bool IsActivitySection => CurrentSection == "Activity";

    public bool IsPerformanceSection => CurrentSection == "Performance";

    public bool IsRiskSection => CurrentSection == "Risk & Thresholds";

    public bool IsSourcesSection => CurrentSection == "Sources";

    public bool IsAlertsSection => CurrentSection == "Alerts";

    public bool IsAboutSection => CurrentSection == "About";

    public bool CanEditRiskThresholds => SelectedRiskProfile == "Custom";

    private void LoadFromSnapshot(SettingsSnapshot snapshot)
    {
        SettingsPath = snapshot.Paths.SettingsPath;
        SecretsPath = snapshot.Paths.SecretsPath;
        TemplatePath = snapshot.Paths.TemplatePath;

        ShellStatus = snapshot.Settings.General.ApplicationState;
        CurrentValuationCurrency = snapshot.Settings.General.ValuationCurrency;
        UsdcSecondaryEnabled = snapshot.Settings.General.AllowUsdcSecondary;
        ReleaseFeedUrl = snapshot.Settings.General.ReleaseFeedUrl;

        ApprovedCandidatesText = string.Join(", ", snapshot.Settings.Policy.ApprovedCandidates);
        WatchlistNote = snapshot.Settings.Policy.WatchlistNote;
        ProtectedBtcMode = snapshot.Settings.Policy.ProtectedBtcMode;
        ProtectedEthMode = snapshot.Settings.Policy.ProtectedEthMode;
        OperatorNotes = snapshot.Settings.Policy.OperatorNotes;

        SelectedRiskProfile = snapshot.Settings.Risk.ActiveProfile;
        CustomMaxPositionPct = snapshot.Settings.Risk.CustomMaxPositionPct.ToString("0.##");
        CustomDailyLossPct = snapshot.Settings.Risk.CustomDailyLossPct.ToString("0.##");
        CustomTurnoverPct = snapshot.Settings.Risk.CustomTurnoverPct.ToString("0.##");

        ConfigurationSummary = BuildConfigurationSummary(snapshot);
        CoinbaseApiKeyStatus = DescribeSecretStatus(snapshot.SecretStatuses, SecretNames.CoinbaseApiKey);
        CoinbaseApiSecretStatus = DescribeSecretStatus(snapshot.SecretStatuses, SecretNames.CoinbaseApiSecret);
        OpenAiApiKeyStatus = DescribeSecretStatus(snapshot.SecretStatuses, SecretNames.OpenAiApiKey);
        ValidationSummary = string.Join(Environment.NewLine, snapshot.ValidationMessages);

        ValidationMessages.Clear();
        foreach (var message in snapshot.ValidationMessages)
        {
            ValidationMessages.Add(message);
        }

        AssetRows.Clear();
        foreach (var asset in BuildAssetRows(snapshot.Settings.Policy))
        {
            AssetRows.Add(asset);
        }

        SourceEntries.Clear();
        foreach (var entry in snapshot.Settings.Sources.Entries)
        {
            SourceEntries.Add(new SourceEntryViewModel(entry.Name, entry.Type, entry.State, entry.Scope));
        }

        PolicyRules.Clear();
        foreach (var rule in BuildPolicyRules(snapshot.Settings.Policy))
        {
            PolicyRules.Add(rule);
        }
        SelectedPolicyRule = PolicyRules.FirstOrDefault();

        AlertItems.Clear();
        foreach (var item in BuildAlerts(snapshot))
        {
            AlertItems.Add(item);
        }

        ActivityItems.Clear();
        foreach (var item in BuildActivityItems())
        {
            ActivityItems.Add(item);
        }

        SourceRegistrySummary = "Locked states: Active, Observed, Ignored, Blocked, Needs Review. Watchers default to Observed or Needs Review until later-phase logic is added.";
        RiskProfileMessage = "Switching away from Custom with unsaved threshold changes requires confirmation. Reset restores the recommended default set.";
        UpdateStatus = "Manual update check has not run yet.";
        SettingsActionMessage = "Use Save, Validate, and Reset to manage local Phase 1 configuration safely.";
    }

    [RelayCommand]
    private void SaveSettings()
    {
        try
        {
            var settings = BuildSettingsFromEditor();
            _settingsService.Save(settings);
            LoadFromSnapshot(_settingsService.Load());
            SettingsActionMessage = "Save complete. Local non-secret settings were written to settings.json.";
            ValidationSummary = "Settings saved successfully." + Environment.NewLine + ValidationSummary;
        }
        catch (Exception ex)
        {
            SettingsActionMessage = "Save failed. Correct the highlighted issue and try again.";
            ValidationSummary = $"Save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ValidateSettings()
    {
        try
        {
            var settings = BuildSettingsFromEditor();
            var messages = _settingsService.Validate(settings);
            ValidationMessages.Clear();
            foreach (var message in messages)
            {
                ValidationMessages.Add(message);
            }

            SettingsActionMessage = ClassifyValidationMessages(messages);
            ValidationSummary = string.Join(Environment.NewLine, messages);
        }
        catch (Exception ex)
        {
            SettingsActionMessage = "Validation failed before classification completed.";
            ValidationSummary = $"Validation failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ResetToSaved()
    {
        LoadFromSnapshot(_settingsService.Load());
        SettingsActionMessage = "Reset complete. The editor matches the last saved local settings.";
        ValidationSummary = "Reset editor state to the last saved local settings.";
    }

    [RelayCommand]
    private void ResetRiskDefaults()
    {
        var defaults = AppSettings.CreateDefault().Risk;
        SelectedRiskProfile = defaults.ActiveProfile;
        CustomMaxPositionPct = defaults.CustomMaxPositionPct.ToString("0.##");
        CustomDailyLossPct = defaults.CustomDailyLossPct.ToString("0.##");
        CustomTurnoverPct = defaults.CustomTurnoverPct.ToString("0.##");
        RiskProfileMessage = "Risk settings were reset to the recommended default state.";
        SettingsActionMessage = "Risk defaults restored to the approved recommended values.";
        _pendingRiskConfirmationTarget = null;
    }

    [RelayCommand]
    private void SetSection(string? sectionName)
    {
        if (!string.IsNullOrWhiteSpace(sectionName))
        {
            CurrentSection = sectionName;
        }
    }

    [RelayCommand]
    private void SelectPolicyRule(PolicyRuleViewModel? rule)
    {
        if (rule is not null)
        {
            SelectedPolicyRule = rule;
        }
    }

    [RelayCommand]
    private void ApplyRiskProfile(string? profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return;
        }

        if (SelectedRiskProfile == "Custom" &&
            profileName != "Custom" &&
            HasUnsavedCustomRiskChanges() &&
            _pendingRiskConfirmationTarget != profileName)
        {
            _pendingRiskConfirmationTarget = profileName;
            RiskProfileMessage = $"Applying {profileName} will overwrite unsaved Custom threshold edits. Click the same preset again to confirm.";
            SettingsActionMessage = "Confirmation required before unsaved Custom threshold edits are replaced.";
            return;
        }

        _pendingRiskConfirmationTarget = null;
        SelectedRiskProfile = profileName;
        ApplyRiskDefaults(profileName);
        RiskProfileMessage = profileName == "Custom"
            ? "Custom mode allows direct threshold edits."
            : $"{profileName} preset applied. Custom threshold fields were updated to the recommended values for that profile.";
        SettingsActionMessage = profileName == "Custom"
            ? "Custom risk editing is enabled."
            : $"{profileName} preset applied. Direct threshold editing is now locked until Custom is selected again.";
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (string.IsNullOrWhiteSpace(ReleaseFeedUrl))
        {
            UpdateStatus = "No release feed is configured.";
            return;
        }

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VectorQuay", AppVersion));

            using var response = await client.GetAsync(ReleaseFeedUrl);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            var release = ParseReleaseDocument(document);
            UpdateActionUrl = release.ActionUrl;
            UpdateStatus = $"Current version: {AppVersion}. Latest available: {release.Version} ({release.Name}). Manual download only.";
        }
        catch (Exception ex)
        {
            UpdateStatus = $"Update check failed: {ex.Message}";
        }
    }

    private AppSettings BuildSettingsFromEditor()
    {
        return new AppSettings
        {
            General = new GeneralSettings
            {
                ApplicationState = ShellStatus,
                ValuationCurrency = "USD",
                AllowUsdcSecondary = UsdcSecondaryEnabled,
                ReleaseFeedUrl = ReleaseFeedUrl.Trim(),
            },
            Policy = new PolicySettings
            {
                ApprovedCandidates = ApprovedCandidatesText
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                WatchlistNote = WatchlistNote.Trim(),
                ProtectedBtcMode = ProtectedBtcMode.Trim(),
                ProtectedEthMode = ProtectedEthMode.Trim(),
                OperatorNotes = OperatorNotes.Trim(),
            },
            Risk = new RiskSettings
            {
                ActiveProfile = SelectedRiskProfile,
                CustomMaxPositionPct = ParseDecimal(CustomMaxPositionPct, nameof(CustomMaxPositionPct)),
                CustomDailyLossPct = ParseDecimal(CustomDailyLossPct, nameof(CustomDailyLossPct)),
                CustomTurnoverPct = ParseDecimal(CustomTurnoverPct, nameof(CustomTurnoverPct)),
            },
            Sources = new SourceSettings
            {
                Entries = SourceEntries
                    .Select(entry => new SourceEntrySettings
                    {
                        Name = entry.Name,
                        Type = entry.Type,
                        State = entry.State,
                        Scope = entry.Scope,
                    })
                    .ToList(),
            },
        };
    }

    private static decimal ParseDecimal(string value, string fieldName)
    {
        if (!decimal.TryParse(value, out var parsed))
        {
            throw new InvalidOperationException($"{fieldName} must be a numeric value.");
        }

        return parsed;
    }

    private static string DescribeSecretStatus(IReadOnlyDictionary<string, SecretStatus> statuses, string name)
    {
        if (!statuses.TryGetValue(name, out var status))
        {
            return "Missing";
        }

        return status.Source switch
        {
            SecretSource.Environment => "Present via environment override",
            SecretSource.SecretFile => "Present via secrets.env",
            _ => "Missing",
        };
    }

    private static string BuildConfigurationSummary(SettingsSnapshot snapshot)
    {
        return $"Template: {(snapshot.Paths.TemplateExists ? "found" : "missing")} | Local settings: {(snapshot.Paths.SettingsExists ? "present" : "not created yet")} | Secret file: {(snapshot.Paths.SecretsExists ? "present" : "missing")} | Highest secret precedence: environment variables";
    }

    private static IEnumerable<AssetRowViewModel> BuildAssetRows(PolicySettings policy)
    {
        foreach (var asset in policy.ApprovedCandidates)
        {
            var priority = asset is "BTC" or "ETH" ? "High" : asset is "XRP" or "SOL" ? "Medium" : "Screened";
            var notes = asset == "ETH" ? "Initial holding" : asset == "BTC" ? "USD preferred" : "Expansion set";
            yield return new AssetRowViewModel(asset, "Approved", priority, notes);
        }

        yield return new AssetRowViewModel("DOGE", "Watchlist", "Low", policy.WatchlistNote);
        yield return new AssetRowViewModel("New Listing", "Observed", "Pending", "Auto-discovered, not tradable yet");
    }

    private static IEnumerable<PolicyRuleViewModel> BuildPolicyRules(PolicySettings policy)
    {
        yield return new PolicyRuleViewModel("ETH", policy.ProtectedEthMode, "Protected current holding", true);
        yield return new PolicyRuleViewModel("BTC", policy.ProtectedBtcMode, "Operator lock", false);
    }

    private static IEnumerable<string> BuildAlerts(SettingsSnapshot snapshot)
    {
        yield return "Trading is inactive in Phase 1.";
        if (!snapshot.Paths.SecretsExists)
        {
            yield return "No external secret file detected yet.";
        }

        if (!snapshot.Paths.SettingsExists)
        {
            yield return "Local settings file has not been created yet. Saving from Configuration will create it.";
        }

        yield return "Coinbase integration is reserved for Phase 2 read-only work.";
    }

    private static IEnumerable<string> BuildActivityItems()
    {
        yield return "Decision trace placeholder: BTC-USD buy candidate rejected because trading engine is inactive.";
        yield return "Policy audit placeholder: Protected asset modes are editable and persist locally.";
        yield return "Release management placeholder: Manual update checks execute from About only.";
    }

    private static string ClassifyValidationMessages(IReadOnlyList<string> messages)
    {
        if (messages.Any(message => message.StartsWith("Blocking:", StringComparison.Ordinal)))
        {
            return "Validation found blocking issues. Resolve them before saving or proceeding.";
        }

        if (messages.Any(message => message.StartsWith("Warning:", StringComparison.Ordinal)))
        {
            return "Validation completed with warnings. Review the configuration before proceeding.";
        }

        return "Validation passed with informational messages only.";
    }

    public static ReleaseCheckResult ParseReleaseDocument(JsonDocument document)
    {
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Release feed returned an invalid JSON object.");
        }

        if (!root.TryGetProperty("tag_name", out var tagName) || string.IsNullOrWhiteSpace(tagName.GetString()))
        {
            throw new InvalidOperationException("Release feed did not include a valid tag_name.");
        }

        var version = tagName.GetString()!;
        var name = root.TryGetProperty("name", out var nameProperty) && !string.IsNullOrWhiteSpace(nameProperty.GetString())
            ? nameProperty.GetString()!
            : version;
        var actionUrl = root.TryGetProperty("html_url", out var htmlUrl) && !string.IsNullOrWhiteSpace(htmlUrl.GetString())
            ? htmlUrl.GetString()!
            : string.Empty;

        return new ReleaseCheckResult(version, name, actionUrl);
    }

    private bool HasUnsavedCustomRiskChanges()
    {
        var defaults = AppSettings.CreateDefault().Risk;
        return !string.Equals(CustomMaxPositionPct, defaults.CustomMaxPositionPct.ToString("0.##"), StringComparison.Ordinal) ||
               !string.Equals(CustomDailyLossPct, defaults.CustomDailyLossPct.ToString("0.##"), StringComparison.Ordinal) ||
               !string.Equals(CustomTurnoverPct, defaults.CustomTurnoverPct.ToString("0.##"), StringComparison.Ordinal);
    }

    private void ApplyRiskDefaults(string profileName)
    {
        var profile = profileName switch
        {
            "High Risk" => new RiskSettings { ActiveProfile = profileName, CustomMaxPositionPct = 18m, CustomDailyLossPct = 5m, CustomTurnoverPct = 40m },
            "Low Risk" => new RiskSettings { ActiveProfile = profileName, CustomMaxPositionPct = 8m, CustomDailyLossPct = 2m, CustomTurnoverPct = 15m },
            "Custom" => new RiskSettings { ActiveProfile = profileName, CustomMaxPositionPct = ParseDecimal(CustomMaxPositionPct, nameof(CustomMaxPositionPct)), CustomDailyLossPct = ParseDecimal(CustomDailyLossPct, nameof(CustomDailyLossPct)), CustomTurnoverPct = ParseDecimal(CustomTurnoverPct, nameof(CustomTurnoverPct)) },
            _ => new RiskSettings { ActiveProfile = "Medium Risk", CustomMaxPositionPct = 12m, CustomDailyLossPct = 3m, CustomTurnoverPct = 25m },
        };

        CustomMaxPositionPct = profile.CustomMaxPositionPct.ToString("0.##");
        CustomDailyLossPct = profile.CustomDailyLossPct.ToString("0.##");
        CustomTurnoverPct = profile.CustomTurnoverPct.ToString("0.##");
    }

    partial void OnCurrentSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsHomeSection));
        OnPropertyChanged(nameof(IsConfigurationSection));
        OnPropertyChanged(nameof(IsAssetsSection));
        OnPropertyChanged(nameof(IsPoliciesSection));
        OnPropertyChanged(nameof(IsPortfolioSection));
        OnPropertyChanged(nameof(IsActivitySection));
        OnPropertyChanged(nameof(IsPerformanceSection));
        OnPropertyChanged(nameof(IsRiskSection));
        OnPropertyChanged(nameof(IsSourcesSection));
        OnPropertyChanged(nameof(IsAlertsSection));
        OnPropertyChanged(nameof(IsAboutSection));
    }

    partial void OnSelectedRiskProfileChanged(string value)
    {
        OnPropertyChanged(nameof(CanEditRiskThresholds));
    }
}

public sealed class AssetRowViewModel(string asset, string state, string priority, string notes)
{
    public string Asset { get; } = asset;

    public string State { get; } = state;

    public string Priority { get; } = priority;

    public string Notes { get; } = notes;
}

public sealed class PolicyRuleViewModel(string asset, string mode, string notes, bool isSelected)
{
    public string Asset { get; } = asset;

    public string Mode { get; } = mode;

    public string Notes { get; } = notes;

    public bool IsSelected { get; } = isSelected;
}

public sealed class SourceEntryViewModel(string name, string type, string state, string scope)
{
    public string Name { get; } = name;

    public string Type { get; } = type;

    public string State { get; } = state;

    public string Scope { get; } = scope;
}

public sealed record ReleaseCheckResult(string Version, string Name, string ActionUrl);
