using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
    private readonly List<AssetRowViewModel> _allAssetRows = [];
    private readonly List<PolicyRuleViewModel> _allPolicyRules = [];
    private readonly List<ActivityEntryViewModel> _allActivityEntries = [];
    private readonly List<AlertEntryViewModel> _allAlertEntries = [];
    private readonly List<AlertRuleViewModel> _allAlertRules = [];

    public MainWindowViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        AssetRows = [];
        SourceEntries = [];
        VisibleSourceEntries = [];
        VisiblePolicyRules = [];
        ActivityEntries = [];
        AlertEntries = [];
        AlertRules = [];
        ValidationMessages = [];
        AlertItems = [];
        ActivityItems = [];
        PolicyRules = [];
        LoadFromSnapshot(_settingsService.Load());
    }

    public ObservableCollection<AssetRowViewModel> AssetRows { get; }

    public ObservableCollection<SourceEntryViewModel> SourceEntries { get; }

    public ObservableCollection<SourceEntryViewModel> VisibleSourceEntries { get; }

    public ObservableCollection<PolicyRuleViewModel> VisiblePolicyRules { get; }

    public ObservableCollection<ActivityEntryViewModel> ActivityEntries { get; }

    public ObservableCollection<AlertEntryViewModel> AlertEntries { get; }

    public ObservableCollection<AlertRuleViewModel> AlertRules { get; }

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
    private string selectedSourceView = "All";

    [ObservableProperty]
    private string sourceActionMessage = "Source management controls in Phase 1 are local shell placeholders. Use the view toggle to inspect direct sources and watchers separately.";

    [ObservableProperty]
    private SourceEntryViewModel? selectedSourceEntry;

    [ObservableProperty]
    private string alertActionMessage = "Alert preference editing and delivery tests become interactive in a later phase.";

    [ObservableProperty]
    private bool inAppAlertsEnabled = true;

    [ObservableProperty]
    private bool emailAlertsEnabled = true;

    [ObservableProperty]
    private bool smsAlertsEnabled;

    [ObservableProperty]
    private string alertEmailAddress = "brad@example.com";

    [ObservableProperty]
    private string alertSmsNumber = "+1 555-0100";

    [ObservableProperty]
    private string quietHoursSummary = "Scheduling arrives in a later phase";

    [ObservableProperty]
    private string lastAlertDeliveryTest = "Not Run";

    [ObservableProperty]
    private string assetSearchText = string.Empty;

    [ObservableProperty]
    private string assetStateFilter = "All States";

    [ObservableProperty]
    private string assetSortOrder = "Trade Priority";

    [ObservableProperty]
    private string policySearchText = string.Empty;

    [ObservableProperty]
    private string policyAssetFilter = "All Assets";

    [ObservableProperty]
    private string policyModeFilter = "All Modes";

    [ObservableProperty]
    private string activityAssetFilter = "All Assets";

    [ObservableProperty]
    private string activityActionFilter = "All Actions";

    [ObservableProperty]
    private string activityOutcomeFilter = "All Outcomes";

    [ObservableProperty]
    private string activitySelectionMessage = "Select a row to inspect its mock decision details.";

    [ObservableProperty]
    private ActivityEntryViewModel? selectedActivityEntry;

    [ObservableProperty]
    private string performanceRange = "24H";

    [ObservableProperty]
    private string performanceRangeSummary = "24H shell view selected. Live performance history arrives in later phases.";

    [ObservableProperty]
    private string sourceSearchText = string.Empty;

    [ObservableProperty]
    private string sourceStateFilter = "All States";

    [ObservableProperty]
    private string alertSeverityFilter = "All Severities";

    [ObservableProperty]
    private string alertDestinationFilter = "All Destinations";

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

    public bool IsAllSourceView => SelectedSourceView == "All";

    public bool IsDirectSourceView => SelectedSourceView == "Direct Sources";

    public bool IsWatchersSourceView => SelectedSourceView == "Watchers";

    public bool HasSelectedSourceEntry => SelectedSourceEntry is not null;

    public bool CanOpenUpdateActionUrl => !string.IsNullOrWhiteSpace(UpdateActionUrl);

    public bool IsPerformanceRange24H => PerformanceRange == "24H";

    public bool IsPerformanceRange7D => PerformanceRange == "7D";

    public bool IsPerformanceRange30D => PerformanceRange == "30D";

    public bool IsPerformanceRange90D => PerformanceRange == "90D";

    public bool IsPerformanceRange1Y => PerformanceRange == "1Y";

    public bool IsPerformanceRangeAll => PerformanceRange == "ALL";

    public string SelectedSourceReliability => SelectedSourceEntry is null
        ? "Reliability: Not selected"
        : SelectedSourceEntry.IsWatcher
            ? "Reliability: Needs human review"
            : "Reliability: Reviewed shell baseline";

    public string SelectedSourceSignalSummary => SelectedSourceEntry is null
        ? "Signals: None"
        : SelectedSourceEntry.IsWatcher
            ? "Signals: Discovery / sentiment / narrative watch"
            : "Signals: Exchange / market surface";

    public string SelectedSourceContributionSummary => SelectedSourceEntry is null
        ? "Last Contribution: No source selected"
        : $"Last Contribution: {SelectedSourceEntry.State} shell-only state, no live scoring yet";

    public string SelectedActivityDecisionSummary => SelectedActivityEntry?.DecisionSummary ?? "Select an activity row to inspect a mock rationale summary.";

    public string SelectedActivityPolicyRiskSummary => SelectedActivityEntry?.PolicyRiskSummary ?? "No activity row is selected yet.";

    public IReadOnlyList<ActivitySourceContributionViewModel> SelectedActivitySources => SelectedActivityEntry?.Sources ?? [];

    public IReadOnlyList<string> AssetStateOptions { get; } = ["All States", "Approved", "Watchlist", "Observed"];

    public IReadOnlyList<string> AssetSortOptions { get; } = ["Trade Priority", "Asset", "State"];

    public IReadOnlyList<string> PolicyAssetOptions { get; } = ["All Assets", "BTC", "ETH"];

    public IReadOnlyList<string> PolicyModeOptions { get; } = ["All Modes", "Do Not Buy", "Do Not Sell", "Do Not Trade"];

    public IReadOnlyList<string> ActivityAssetOptions { get; } = ["All Assets", "BTC-USD", "ETH-USD"];

    public IReadOnlyList<string> ActivityActionOptions { get; } = ["All Actions", "Buy", "Sell"];

    public IReadOnlyList<string> ActivityOutcomeOptions { get; } = ["All Outcomes", "Selected", "Rejected"];

    public IReadOnlyList<string> SourceStateOptions { get; } = ["All States", "Active", "Observed", "Needs Review"];

    public IReadOnlyList<string> AlertSeverityOptions { get; } = ["All Severities", "Info", "Warning", "Error"];

    public IReadOnlyList<string> AlertDestinationOptions { get; } = ["All Destinations", "In-App", "In-App + Email", "In-App + SMS", "In-App + Email + SMS"];

    public string OpenAlertsSummary => $"{AlertEntries.Count} visible";

    public string MutedRulesSummary => $"{CountMutedAlertChannels()} muted";

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

        _allAssetRows.Clear();
        _allAssetRows.AddRange(BuildAssetRows(snapshot.Settings.Policy));
        RefreshAssetRows();

        SourceEntries.Clear();
        foreach (var entry in snapshot.Settings.Sources.Entries)
        {
            SourceEntries.Add(new SourceEntryViewModel(entry.Name, entry.Type, entry.State, entry.Scope));
        }
        RefreshVisibleSourceEntries();

        _allPolicyRules.Clear();
        _allPolicyRules.AddRange(BuildPolicyRules(snapshot.Settings.Policy));
        PolicyRules.Clear();
        foreach (var rule in _allPolicyRules)
        {
            PolicyRules.Add(rule);
        }
        RefreshPolicyRules();

        _allAlertEntries.Clear();
        _allAlertEntries.AddRange(BuildAlertEntries(snapshot));
        RefreshAlertEntries();

        _allAlertRules.Clear();
        _allAlertRules.AddRange(BuildAlertRules());
        RefreshAlertRules();

        AlertItems.Clear();
        foreach (var item in AlertEntries.Select(entry => entry.Summary))
        {
            AlertItems.Add(item);
        }

        _allActivityEntries.Clear();
        _allActivityEntries.AddRange(BuildActivityItems());
        RefreshActivityEntries();

        SourceRegistrySummary = "Locked states: Active, Observed, Ignored, Blocked, Needs Review. Watchers default to Observed or Needs Review until later-phase logic is added.";
        RiskProfileMessage = "Switching away from Custom with unsaved threshold changes requires confirmation. Reset restores the recommended default set.";
        UpdateStatus = "Manual update check has not run yet.";
        SettingsActionMessage = "Use Save, Validate, and Reset to manage local Phase 1 configuration safely.";
        SourceActionMessage = "Source management controls in Phase 1 are local shell placeholders. Use the view toggle to inspect direct sources and watchers separately.";
        AlertActionMessage = "Use the dropdowns and contact fields below to configure the local shell state for alerts.";
        ActivitySelectionMessage = "Select a row to inspect its mock decision details.";
        PerformanceRangeSummary = "24H shell view selected. Live performance history arrives in later phases.";
        InAppAlertsEnabled = true;
        EmailAlertsEnabled = true;
        SmsAlertsEnabled = false;
        AlertEmailAddress = "brad@example.com";
        AlertSmsNumber = "+1 555-0100";
        QuietHoursSummary = "Scheduling arrives in a later phase";
        LastAlertDeliveryTest = "Not Run";
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
    private void SetSourceView(string? viewName)
    {
        if (string.IsNullOrWhiteSpace(viewName))
        {
            return;
        }

        SelectedSourceView = viewName;
        RefreshVisibleSourceEntries();
        SourceActionMessage = viewName switch
        {
            "Direct Sources" => "Showing direct sources only. These represent exchange or deterministic feed surfaces.",
            "Watchers" => "Showing watcher entries only. These are future discovery and signal-watch workflows.",
            _ => "Showing all defined source entries for the Phase 1 shell.",
        };
    }

    [RelayCommand]
    private void SelectSourceEntry(SourceEntryViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        SelectedSourceEntry = entry;
        SourceActionMessage = $"Selected source: {entry.Name}. Detail actions remain shell-level placeholders in Phase 1.";
    }

    [RelayCommand]
    private void RunSourceAction(string? actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName))
        {
            return;
        }

        SourceActionMessage = actionName switch
        {
            "Add Source" => "Manual direct-source creation is reserved for a later phase. This shell currently shows the planned management surface only.",
            "Add Watcher" => "Watcher creation arrives in later phases after AI-assisted source workflows are defined.",
            "Edit Weight" => SelectedSourceEntry is null ? "Select a source first." : $"Weight editing for {SelectedSourceEntry.Name} is reserved for a later phase.",
            "Edit Scope" => SelectedSourceEntry is null ? "Select a source first." : $"Scope editing for {SelectedSourceEntry.Name} is reserved for a later phase.",
            "More Actions" => SelectedSourceEntry is null ? "Select a source first." : $"Lifecycle actions for {SelectedSourceEntry.Name} will be added in later phases.",
            "View History" => SelectedSourceEntry is null ? "Select a source first." : $"History for {SelectedSourceEntry.Name} is not populated until source events are tracked.",
            "Automation Settings" => SelectedSourceEntry is null ? "Select a source first." : $"Automation settings for {SelectedSourceEntry.Name} arrive in later phases.",
            _ => "That source action is not available yet.",
        };
    }

    [RelayCommand]
    private void RunAlertAction(string? actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName))
        {
            return;
        }

        switch (actionName)
        {
            case "Test Alert":
                LastAlertDeliveryTest = $"Passed at {DateTime.Now:HH:mm:ss}";
                _allAlertEntries.Insert(0, new AlertEntryViewModel(
                    $"Manual test alert fired at {DateTime.Now:HH:mm:ss}.",
                    "Info",
                    BuildAlertDestinationLabel()));
                RefreshAlertEntries();
                AlertActionMessage = "Manual test alert generated. The recent-alerts list and delivery-test summary were updated.";
                OnPropertyChanged(nameof(OpenAlertsSummary));
                break;

            default:
                AlertActionMessage = "That alert action is not available yet.";
                break;
        }
    }

    [RelayCommand]
    private void SetPerformanceRange(string? rangeName)
    {
        if (string.IsNullOrWhiteSpace(rangeName))
        {
            return;
        }

        PerformanceRange = rangeName;
        PerformanceRangeSummary = rangeName switch
        {
            "7D" => "7D shell view selected. Weekly rollups become meaningful after performance history is available.",
            "30D" => "30D shell view selected. Monthly comparisons arrive with later-phase strategy history.",
            "90D" => "90D shell view selected. Medium-horizon trend reporting is reserved for later phases.",
            "1Y" => "1Y shell view selected. Long-horizon reporting depends on sustained live operation.",
            "ALL" => "ALL shell view selected. Full-lifecycle performance history is not available in Phase 1.",
            _ => "24H shell view selected. Live performance history arrives in later phases.",
        };
    }

    [RelayCommand]
    private void SelectActivityEntry(ActivityEntryViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        SelectedActivityEntry = entry;
        ActivitySelectionMessage = $"Selected {entry.AssetPair} {entry.Action} activity row. Detailed execution history becomes available in later phases.";
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

    [RelayCommand]
    private void OpenUpdateActionUrl()
    {
        var targetUrl = !string.IsNullOrWhiteSpace(UpdateActionUrl)
            ? UpdateActionUrl
            : TryBuildRepositoryReleasePageUrl(ReleaseFeedUrl);

        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            UpdateStatus = "No release page is available yet. Run Check for Updates first.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = targetUrl,
                UseShellExecute = true,
            });
            UpdateStatus = $"Opened release page: {targetUrl}";
        }
        catch (Exception ex)
        {
            UpdateStatus = $"Unable to open the release page: {ex.Message}";
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

    private static IEnumerable<AlertEntryViewModel> BuildAlertEntries(SettingsSnapshot snapshot)
    {
        yield return new AlertEntryViewModel("Trading is inactive in Phase 1.", "Info", "In-App");
        if (!snapshot.Paths.SecretsExists)
        {
            yield return new AlertEntryViewModel("No external secret file detected yet.", "Warning", "In-App");
        }

        if (!snapshot.Paths.SettingsExists)
        {
            yield return new AlertEntryViewModel("Local settings file has not been created yet. Saving from Configuration will create it.", "Info", "In-App");
        }

        yield return new AlertEntryViewModel("Coinbase integration is reserved for Phase 2 read-only work.", "Info", "In-App + Email");
    }

    private static IEnumerable<ActivityEntryViewModel> BuildActivityItems()
    {
        yield return new ActivityEntryViewModel(
            "2026-04-13 09:14",
            "BTC-USD",
            "Buy",
            "$12.40",
            "Selected",
            "0.74",
            "BTC-USD was elevated because the approved-candidate list still favors BTC, market-trend inputs were positive, and no Phase 1 policy gate blocked the trade candidate.",
            "Protected-asset checks passed, sizing stayed within the Medium Risk shell profile, and the trade remained advisory only because execution is inactive in Phase 1.",
            [
                new ActivitySourceContributionViewModel("Market Trend", "Positive", "Default"),
                new ActivitySourceContributionViewModel("Coinbase Surface", "Eligible Pair", "Baseline"),
                new ActivitySourceContributionViewModel("Operator Policy", "Approved Candidate", "Required")
            ]);
        yield return new ActivityEntryViewModel(
            "2026-04-13 08:52",
            "ETH-USD",
            "Sell",
            "$8.10",
            "Rejected",
            "0.41",
            "ETH-USD remained visible, but the shell rejected the sell candidate because ETH is currently configured as a protected asset with Do Not Trade mode.",
            "Protected-asset enforcement blocked the action before later-phase risk sizing or execution checks could proceed. The result remains a valid no-trade outcome.",
            [
                new ActivitySourceContributionViewModel("Market Trend", "Negative", "Default"),
                new ActivitySourceContributionViewModel("Protected Asset Rule", "Do Not Trade", "Blocking"),
                new ActivitySourceContributionViewModel("Risk Profile", "Medium Risk", "Context")
            ]);
    }

    private static IEnumerable<AlertRuleViewModel> BuildAlertRules()
    {
        yield return new AlertRuleViewModel("Missing Coinbase secret", "Warning", "In-App");
        yield return new AlertRuleViewModel("Settings validation failure", "Error", "In-App + Email");
        yield return new AlertRuleViewModel("Update feed unavailable", "Info", "In-App");
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

    private static string TryBuildRepositoryReleasePageUrl(string releaseFeedUrl)
    {
        if (!Uri.TryCreate(releaseFeedUrl, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        if (!string.Equals(uri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 5 ||
            !string.Equals(segments[0], "repos", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(segments[3], "releases", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return $"https://github.com/{segments[1]}/{segments[2]}/releases";
    }

    private int CountMutedAlertChannels()
    {
        var muted = 0;
        if (!InAppAlertsEnabled)
        {
            muted++;
        }

        if (!EmailAlertsEnabled)
        {
            muted++;
        }

        if (!SmsAlertsEnabled)
        {
            muted++;
        }

        return muted;
    }

    private string BuildAlertDestinationLabel()
    {
        var channels = new List<string>();

        if (InAppAlertsEnabled)
        {
            channels.Add("In-App");
        }

        if (EmailAlertsEnabled)
        {
            channels.Add("Email");
        }

        if (SmsAlertsEnabled)
        {
            channels.Add("SMS");
        }

        return channels.Count == 0 ? "No active channel" : string.Join(" + ", channels);
    }

    private void RefreshAssetRows()
    {
        var filtered = _allAssetRows
            .Where(asset => string.IsNullOrWhiteSpace(AssetSearchText) ||
                            asset.Asset.Contains(AssetSearchText, StringComparison.OrdinalIgnoreCase) ||
                            asset.Notes.Contains(AssetSearchText, StringComparison.OrdinalIgnoreCase))
            .Where(asset => AssetStateFilter == "All States" || string.Equals(asset.State, AssetStateFilter, StringComparison.OrdinalIgnoreCase));

        filtered = AssetSortOrder switch
        {
            "Asset" => filtered.OrderBy(asset => asset.Asset, StringComparer.OrdinalIgnoreCase),
            "State" => filtered.OrderBy(asset => asset.State, StringComparer.OrdinalIgnoreCase).ThenBy(asset => asset.Asset, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderBy(asset => asset.PriorityRank).ThenBy(asset => asset.Asset, StringComparer.OrdinalIgnoreCase),
        };

        AssetRows.Clear();
        foreach (var asset in filtered)
        {
            AssetRows.Add(asset);
        }
    }

    private void RefreshPolicyRules()
    {
        var filtered = _allPolicyRules
            .Where(rule => string.IsNullOrWhiteSpace(PolicySearchText) ||
                           rule.Asset.Contains(PolicySearchText, StringComparison.OrdinalIgnoreCase) ||
                           rule.Mode.Contains(PolicySearchText, StringComparison.OrdinalIgnoreCase) ||
                           rule.Notes.Contains(PolicySearchText, StringComparison.OrdinalIgnoreCase))
            .Where(rule => PolicyAssetFilter == "All Assets" || string.Equals(rule.Asset, PolicyAssetFilter, StringComparison.OrdinalIgnoreCase))
            .Where(rule => PolicyModeFilter == "All Modes" || string.Equals(rule.Mode, PolicyModeFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        PolicyRules.Clear();
        VisiblePolicyRules.Clear();
        foreach (var rule in filtered)
        {
            PolicyRules.Add(rule);
            VisiblePolicyRules.Add(rule);
        }

        SelectedPolicyRule = VisiblePolicyRules.FirstOrDefault();
    }

    private void RefreshActivityEntries()
    {
        var filtered = _allActivityEntries
            .Where(entry => ActivityAssetFilter == "All Assets" || string.Equals(entry.AssetPair, ActivityAssetFilter, StringComparison.OrdinalIgnoreCase))
            .Where(entry => ActivityActionFilter == "All Actions" || string.Equals(entry.Action, ActivityActionFilter, StringComparison.OrdinalIgnoreCase))
            .Where(entry => ActivityOutcomeFilter == "All Outcomes" || string.Equals(entry.Outcome, ActivityOutcomeFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        ActivityEntries.Clear();
        ActivityItems.Clear();
        foreach (var entry in filtered)
        {
            ActivityEntries.Add(entry);
            ActivityItems.Add($"{entry.AssetPair} {entry.Action} · {entry.Outcome}");
        }

        SelectedActivityEntry = ActivityEntries.FirstOrDefault();
    }

    private void RefreshVisibleSourceEntries()
    {
        var filtered = SelectedSourceView switch
        {
            "Direct Sources" => SourceEntries.Where(entry => entry.IsDirectSource),
            "Watchers" => SourceEntries.Where(entry => entry.IsWatcher),
            _ => SourceEntries.AsEnumerable(),
        };

        filtered = filtered
            .Where(entry => string.IsNullOrWhiteSpace(SourceSearchText) ||
                            entry.Name.Contains(SourceSearchText, StringComparison.OrdinalIgnoreCase) ||
                            entry.Scope.Contains(SourceSearchText, StringComparison.OrdinalIgnoreCase))
            .Where(entry => SourceStateFilter == "All States" || string.Equals(entry.State, SourceStateFilter, StringComparison.OrdinalIgnoreCase));

        VisibleSourceEntries.Clear();
        foreach (var entry in filtered)
        {
            VisibleSourceEntries.Add(entry);
        }

        SelectedSourceEntry = VisibleSourceEntries.FirstOrDefault();
    }

    private void RefreshAlertEntries()
    {
        var filtered = _allAlertEntries
            .Where(entry => AlertSeverityFilter == "All Severities" || string.Equals(entry.Severity, AlertSeverityFilter, StringComparison.OrdinalIgnoreCase))
            .Where(entry => AlertDestinationFilter == "All Destinations" || string.Equals(entry.Destination, AlertDestinationFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        AlertEntries.Clear();
        AlertItems.Clear();
        foreach (var entry in filtered)
        {
            AlertEntries.Add(entry);
            AlertItems.Add(entry.Summary);
        }

        OnPropertyChanged(nameof(OpenAlertsSummary));
    }

    private void RefreshAlertRules()
    {
        var filtered = _allAlertRules
            .Where(rule => AlertSeverityFilter == "All Severities" || string.Equals(rule.Severity, AlertSeverityFilter, StringComparison.OrdinalIgnoreCase))
            .Where(rule => AlertDestinationFilter == "All Destinations" || string.Equals(rule.Destination, AlertDestinationFilter, StringComparison.OrdinalIgnoreCase));

        AlertRules.Clear();
        foreach (var rule in filtered)
        {
            AlertRules.Add(rule);
        }
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

    partial void OnAssetSearchTextChanged(string value) => RefreshAssetRows();

    partial void OnAssetStateFilterChanged(string value) => RefreshAssetRows();

    partial void OnAssetSortOrderChanged(string value) => RefreshAssetRows();

    partial void OnPolicySearchTextChanged(string value) => RefreshPolicyRules();

    partial void OnPolicyAssetFilterChanged(string value) => RefreshPolicyRules();

    partial void OnPolicyModeFilterChanged(string value) => RefreshPolicyRules();

    partial void OnActivityAssetFilterChanged(string value) => RefreshActivityEntries();

    partial void OnActivityActionFilterChanged(string value) => RefreshActivityEntries();

    partial void OnActivityOutcomeFilterChanged(string value) => RefreshActivityEntries();

    partial void OnSelectedActivityEntryChanged(ActivityEntryViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedActivityDecisionSummary));
        OnPropertyChanged(nameof(SelectedActivityPolicyRiskSummary));
        OnPropertyChanged(nameof(SelectedActivitySources));
    }

    partial void OnSelectedSourceViewChanged(string value)
    {
        OnPropertyChanged(nameof(IsAllSourceView));
        OnPropertyChanged(nameof(IsDirectSourceView));
        OnPropertyChanged(nameof(IsWatchersSourceView));
        RefreshVisibleSourceEntries();
    }

    partial void OnSelectedSourceEntryChanged(SourceEntryViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedSourceEntry));
        OnPropertyChanged(nameof(SelectedSourceReliability));
        OnPropertyChanged(nameof(SelectedSourceSignalSummary));
        OnPropertyChanged(nameof(SelectedSourceContributionSummary));
    }

    partial void OnUpdateActionUrlChanged(string value)
    {
        OnPropertyChanged(nameof(CanOpenUpdateActionUrl));
    }

    partial void OnSourceSearchTextChanged(string value) => RefreshVisibleSourceEntries();

    partial void OnSourceStateFilterChanged(string value) => RefreshVisibleSourceEntries();

    partial void OnAlertSeverityFilterChanged(string value)
    {
        RefreshAlertEntries();
        RefreshAlertRules();
    }

    partial void OnAlertDestinationFilterChanged(string value)
    {
        RefreshAlertEntries();
        RefreshAlertRules();
    }

    partial void OnInAppAlertsEnabledChanged(bool value)
    {
        AlertActionMessage = "In-app alert preference updated for the current local shell session.";
        OnPropertyChanged(nameof(MutedRulesSummary));
    }

    partial void OnEmailAlertsEnabledChanged(bool value)
    {
        AlertActionMessage = "Email alert preference updated for the current local shell session.";
        OnPropertyChanged(nameof(MutedRulesSummary));
    }

    partial void OnSmsAlertsEnabledChanged(bool value)
    {
        AlertActionMessage = "SMS alert preference updated for the current local shell session.";
        OnPropertyChanged(nameof(MutedRulesSummary));
    }

    partial void OnAlertEmailAddressChanged(string value)
    {
        AlertActionMessage = string.IsNullOrWhiteSpace(value)
            ? "Email destination cleared for the current local shell session."
            : "Email destination updated for the current local shell session.";
    }

    partial void OnAlertSmsNumberChanged(string value)
    {
        AlertActionMessage = string.IsNullOrWhiteSpace(value)
            ? "SMS destination cleared for the current local shell session."
            : "SMS destination updated for the current local shell session.";
    }

    partial void OnPerformanceRangeChanged(string value)
    {
        OnPropertyChanged(nameof(IsPerformanceRange24H));
        OnPropertyChanged(nameof(IsPerformanceRange7D));
        OnPropertyChanged(nameof(IsPerformanceRange30D));
        OnPropertyChanged(nameof(IsPerformanceRange90D));
        OnPropertyChanged(nameof(IsPerformanceRange1Y));
        OnPropertyChanged(nameof(IsPerformanceRangeAll));
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
}

public sealed class AssetRowViewModel(string asset, string state, string priority, string notes)
{
    public string Asset { get; } = asset;

    public string State { get; } = state;

    public string Priority { get; } = priority;

    public string Notes { get; } = notes;

    public int PriorityRank => Priority switch
    {
        "High" => 0,
        "Medium" => 1,
        "Screened" => 2,
        "Low" => 3,
        _ => 4,
    };
}

public sealed class PolicyRuleViewModel(string asset, string mode, string notes, bool isSelected)
{
    public string Asset { get; } = asset;

    public string Mode { get; } = mode;

    public string Notes { get; } = notes;

    public bool IsSelected { get; } = isSelected;
}

public sealed class ActivityEntryViewModel(
    string timestamp,
    string assetPair,
    string action,
    string amount,
    string outcome,
    string confidence,
    string decisionSummary,
    string policyRiskSummary,
    IReadOnlyList<ActivitySourceContributionViewModel> sources)
{
    public string Timestamp { get; } = timestamp;

    public string AssetPair { get; } = assetPair;

    public string Action { get; } = action;

    public string Amount { get; } = amount;

    public string Outcome { get; } = outcome;

    public string Confidence { get; } = confidence;

    public string DecisionSummary { get; } = decisionSummary;

    public string PolicyRiskSummary { get; } = policyRiskSummary;

    public IReadOnlyList<ActivitySourceContributionViewModel> Sources { get; } = sources;
}

public sealed class ActivitySourceContributionViewModel(string source, string signal, string weight)
{
    public string Source { get; } = source;

    public string Signal { get; } = signal;

    public string Weight { get; } = weight;
}

public sealed class AlertEntryViewModel(string summary, string severity, string destination)
{
    public string Summary { get; } = summary;

    public string Severity { get; } = severity;

    public string Destination { get; } = destination;
}

public sealed class AlertRuleViewModel(string rule, string severity, string destination)
{
    public string Rule { get; } = rule;

    public string Severity { get; } = severity;

    public string Destination { get; } = destination;
}

public sealed class SourceEntryViewModel(string name, string type, string state, string scope)
{
    public string Name { get; } = name;

    public string Type { get; } = type;

    public string State { get; } = state;

    public string Scope { get; } = scope;

    public bool IsWatcher => Type.Contains("Watcher", StringComparison.OrdinalIgnoreCase);

    public bool IsDirectSource => !IsWatcher;
}

public sealed record ReleaseCheckResult(string Version, string Name, string ActionUrl);
