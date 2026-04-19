using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VectorQuay.App.Models;
using VectorQuay.Core.Coinbase;
using VectorQuay.Core.Configuration;

namespace VectorQuay.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly string[] AllocationPalette = ["#2F3C8C", "#5B7CFF", "#00A3A3", "#F59E0B", "#8B5CF6", "#E35D8F"];

    private readonly SettingsService _settingsService;
    private readonly ICoinbaseReadOnlyService? _coinbaseService;
    private string? _pendingRiskConfirmationTarget;
    private CoinbaseShellSnapshot? _latestCoinbaseSnapshot;
    private List<AssetPolicySettings>? _launchPolicyBaseline;
    private string? _launchProtectedBtcMode;
    private string? _launchProtectedEthMode;
    private readonly List<AssetRowViewModel> _allAssetRows = [];
    private readonly List<PolicyRuleViewModel> _allPolicyRules = [];
    private readonly List<ActivityEntryViewModel> _allActivityEntries = [];
    private readonly List<AlertEntryViewModel> _allAlertEntries = [];
    private readonly List<AlertRuleViewModel> _allAlertRules = [];
    private bool _isRefreshingActivityFilters;

    public MainWindowViewModel(SettingsService settingsService)
        : this(settingsService, null, false)
    {
    }

    public MainWindowViewModel(SettingsService settingsService, ICoinbaseReadOnlyService? coinbaseService, bool enableStartupRefresh)
    {
        _settingsService = settingsService;
        _coinbaseService = coinbaseService;
        AssetRows = [];
        TopTradeAssetItems = [];
        RecentOverviewActivityItems = [];
        AllocationSlices = [];
        PortfolioHoldings = [];
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
        ActivityAssetOptions = [];
        ActivityActionOptions = [];
        ActivityOutcomeOptions = [];
        LoadFromSnapshot(_settingsService.Load());

        if (enableStartupRefresh && _coinbaseService is not null)
        {
            ShellStatus = "Connection Pending";
            _ = RefreshCoinbaseDataAsync(isStartup: true);
        }
    }

    public ObservableCollection<AssetRowViewModel> AssetRows { get; }

    public ObservableCollection<TopTradeAssetViewModel> TopTradeAssetItems { get; }

    public ObservableCollection<OverviewActivityRowViewModel> RecentOverviewActivityItems { get; }

    public ObservableCollection<AllocationSliceViewModel> AllocationSlices { get; }

    public ObservableCollection<PortfolioHoldingViewModel> PortfolioHoldings { get; }

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

    public ObservableCollection<string> ActivityAssetOptions { get; }

    public ObservableCollection<string> ActivityActionOptions { get; }

    public ObservableCollection<string> ActivityOutcomeOptions { get; }

    public IReadOnlyList<string> ProtectedAssetModes { get; } =
    [
        "Allow Full Trade",
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
    private string coinbaseJsonKeyFileStatus = "Not Found";

    [ObservableProperty]
    private string coinbaseJsonKeyFilePath = string.Empty;

    [ObservableProperty]
    private string coinbaseJsonImportPath = string.Empty;

    [ObservableProperty]
    private string openAiApiKeyStatus = "Missing";

    [ObservableProperty]
    private string openAiApiKeyEditor = string.Empty;

    [ObservableProperty]
    private string coinbaseConnectionSummary = "Not Connected";

    [ObservableProperty]
    private string coinbaseRefreshSummary = "No Coinbase refresh has run yet.";

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
    private string connectionsActionMessage = "Configure Coinbase and OpenAI connectivity here.";

    [ObservableProperty]
    private string refreshCoinbaseButtonText = "Refresh Coinbase";

    [ObservableProperty]
    private string refreshUniverseButtonText = "Refresh Universe";

    [ObservableProperty]
    private string overviewAppHealth = "Ready";

    [ObservableProperty]
    private string overviewCoinbaseStatus = "Not Connected";

    [ObservableProperty]
    private string overviewTotalValue = "Awaiting Coinbase connection";

    [ObservableProperty]
    private string overviewOpenPositions = "Awaiting Coinbase connection";

    [ObservableProperty]
    private string overviewUsdAvailable = "Awaiting Coinbase connection";

    [ObservableProperty]
    private string overviewRefreshSummary = "No Coinbase refresh has run yet.";

    [ObservableProperty]
    private string allocationSummary = "Connect Coinbase to see current allocation.";

    [ObservableProperty]
    private string portfolioTotalValue = "Awaiting Coinbase connection";

    [ObservableProperty]
    private string portfolioUnrealizedSummary = "Read-only current-state view";

    [ObservableProperty]
    private string portfolioCashStableSummary = "Awaiting Coinbase connection";

    [ObservableProperty]
    private string portfolioLargestExposure = "Awaiting Coinbase connection";

    [ObservableProperty]
    private string portfolioProtectedAssetsSummary = "Awaiting approved assets";

    [ObservableProperty]
    private string portfolioCashShareSummary = "Awaiting Coinbase connection";

    [ObservableProperty]
    private string portfolioProtectedPolicyCount = "Awaiting approved assets";

    [ObservableProperty]
    private string portfolioRiskPostureSummary = "Awaiting Coinbase connection";

    [ObservableProperty]
    private string portfolioSelectedPositionSummary = "Select a live holding to inspect it in later phases.";

    [ObservableProperty]
    private string portfolioHistorySummary = "Historical portfolio tracking remains a later-phase feature.";

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
    private string sourceNameEditor = string.Empty;

    [ObservableProperty]
    private string sourceScopeEditor = string.Empty;

    [ObservableProperty]
    private string sourceWeightEditor = "Default";

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
    private string selectedPolicyModeEditor = "Allow Full Trade";

    [ObservableProperty]
    private string selectedPolicyNotesEditor = string.Empty;

    [ObservableProperty]
    private bool areAllPoliciesSelected;

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
    private string activityDetailTitle = "No activity selected";

    [ObservableProperty]
    private string activityDetailTimestamp = string.Empty;

    [ObservableProperty]
    private string activityDetailAmount = string.Empty;

    [ObservableProperty]
    private string activityDetailStatus = string.Empty;

    [ObservableProperty]
    private string activityDetailSummary = "Select a Coinbase account event to inspect its details.";

    [ObservableProperty]
    private string performanceRange = "24H";

    [ObservableProperty]
    private string performanceRangeSummary = "24H shell view selected. Live performance history arrives in later phases.";

    [ObservableProperty]
    private string performanceTopHoldingsSummary = "Connect Coinbase to see the current holdings mix.";

    [ObservableProperty]
    private string performanceActivityMixSummary = "Connect Coinbase to see recent account-event mix.";

    [ObservableProperty]
    private string performanceReadModelSummary = "Performance remains a truthful read-only snapshot until time-series analytics are added.";

    [ObservableProperty]
    private string sourceSearchText = string.Empty;

    [ObservableProperty]
    private string sourceStateFilter = "All States";

    [ObservableProperty]
    private string alertSeverityFilter = "All Severities";

    [ObservableProperty]
    private string alertDestinationFilter = "All Destinations";

    [ObservableProperty]
    private string advancedThresholdsSummary = "Advanced thresholds show what later execution controls will enforce. In Phase 2 they remain illustrative and profile-driven.";

    [ObservableProperty]
    private string advancedSpreadToleranceSummary = "0.40% spread tolerance";

    [ObservableProperty]
    private string advancedLiquidityMinimumSummary = "$250K minimum 24H liquidity";

    [ObservableProperty]
    private string advancedConfidenceCutoffSummary = "0.60 confidence cutoff";

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

    public string StatusBarIndicatorBrush =>
        CoinbaseConnectionSummary.Contains("Connected", StringComparison.OrdinalIgnoreCase)
            ? "#2FA56B"
            : CoinbaseConnectionSummary.Contains("Pending", StringComparison.OrdinalIgnoreCase) ||
              CoinbaseApiKeyStatus.StartsWith("Present", StringComparison.OrdinalIgnoreCase) &&
              CoinbaseApiSecretStatus.StartsWith("Present", StringComparison.OrdinalIgnoreCase)
                ? "#C28A19"
                : "#B85C5C";

    public string StatusBarSummary => CoinbaseConnectionSummary;

    public string StatusBarDetail => $"{CoinbaseRefreshSummary} · OpenAI: {OpenAiApiKeyStatus} · Version {AppVersion}";

    public bool CanEditRiskThresholds => SelectedRiskProfile == "Custom";

    public bool IsHighRiskProfile => SelectedRiskProfile == "High Risk";

    public bool IsMediumRiskProfile => SelectedRiskProfile == "Medium Risk";

    public bool IsLowRiskProfile => SelectedRiskProfile == "Low Risk";

    public bool IsCustomRiskProfile => SelectedRiskProfile == "Custom";

    public bool IsAllSourceView => SelectedSourceView == "All";

    public bool IsDirectSourceView => SelectedSourceView == "Direct Sources";

    public bool IsWatchersSourceView => SelectedSourceView == "Watchers";

    public bool HasSelectedSourceEntry => SelectedSourceEntry is not null;

    public string SourceCountSummary => $"{VisibleSourceEntries.Count} shown · {SourceEntries.Count(entry => entry.IsDirectSource)} direct · {SourceEntries.Count(entry => entry.IsWatcher)} watchers";

    public string SourceEditorSummary => SelectedSourceEntry is null
        ? "Select a source or watcher to edit its local settings."
        : $"Editing local settings for {SelectedSourceEntry.Name}.";

    public bool HasPolicySelection => _allPolicyRules.Any(rule => rule.IsSelected);

    public string AssetCountSummary => $"{AssetRows.Count} asset{(AssetRows.Count == 1 ? string.Empty : "s")} displayed";

    public string PolicyCountSummary => $"{VisiblePolicyRules.Count} approved polic{(VisiblePolicyRules.Count == 1 ? "y" : "ies")} displayed";

    public string ActivityEntryCountSummary => ActivityEntries.Count.ToString();

    public bool HasActivityEntries => ActivityEntries.Count > 0;

    public bool ShowNoActivityEntries => !HasActivityEntries;

    public string ActivityCountSummary => ActivityEntries.Count switch
    {
        0 => "No Coinbase account events match the current filters.",
        1 => "1 Coinbase account event shown",
        var count => $"{count} Coinbase account events shown",
    };

    public bool CanOpenUpdateActionUrl => !string.IsNullOrWhiteSpace(UpdateActionUrl);

    public bool IsRefreshingCoinbase => RefreshCoinbaseButtonText != "Refresh Coinbase" || RefreshUniverseButtonText != "Refresh Universe";

    public bool CanRefreshCoinbase => !IsRefreshingCoinbase;

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

    public IReadOnlyList<ActivitySourceContributionViewModel> SelectedActivitySources => SelectedActivityEntry?.Sources ?? [];

    public IReadOnlyList<string> AssetStateOptions { get; } = ["All States", "Approved", "Watchlist", "Observed"];

    public IReadOnlyList<string> AssetSortOptions { get; } = ["Trade Priority", "Asset", "State"];

    public IReadOnlyList<string> PolicyModeOptions { get; } = ["All Modes", "Allow Full Trade", "Do Not Buy", "Do Not Sell", "Do Not Trade"];

    public IReadOnlyList<string> SourceStateOptions { get; } = ["All States", "Active", "Observed", "Needs Review"];

    public IReadOnlyList<string> SourceWeightOptions { get; } = ["Default", "Baseline", "Review", "Low", "Medium", "High", "Required"];

    public IReadOnlyList<string> AlertSeverityOptions { get; } = ["All Severities", "Info", "Warning", "Error"];

    public IReadOnlyList<string> AlertDestinationOptions { get; } = ["All Destinations", "In-App", "In-App + Email", "In-App + SMS", "In-App + Email + SMS"];

    public string OpenAlertsSummary => $"{AlertEntries.Count} visible";

    public string MutedRulesSummary => $"{CountMutedAlertChannels()} muted";

    private void LoadFromSnapshot(SettingsSnapshot snapshot)
    {
        SettingsPath = snapshot.Paths.SettingsPath;
        SecretsPath = snapshot.Paths.SecretsPath;
        TemplatePath = snapshot.Paths.TemplatePath;
        CoinbaseJsonKeyFilePath = File.Exists(snapshot.Paths.CoinbaseApiKeyJsonPath)
            ? snapshot.Paths.CoinbaseApiKeyJsonPath
            : File.Exists(snapshot.Paths.CoinbaseApiKeyJsonTextPath)
                ? snapshot.Paths.CoinbaseApiKeyJsonTextPath
                : snapshot.Paths.CoinbaseApiKeyJsonPath;
        CoinbaseJsonKeyFileStatus = File.Exists(snapshot.Paths.CoinbaseApiKeyJsonPath) || File.Exists(snapshot.Paths.CoinbaseApiKeyJsonTextPath)
            ? "Detected"
            : "Not Found";
        CoinbaseJsonImportPath = CoinbaseJsonKeyFileStatus == "Detected" ? CoinbaseJsonKeyFilePath : string.Empty;

        ShellStatus = snapshot.Settings.General.ApplicationState;
        CurrentValuationCurrency = snapshot.Settings.General.ValuationCurrency;
        UsdcSecondaryEnabled = snapshot.Settings.General.AllowUsdcSecondary;
        ReleaseFeedUrl = snapshot.Settings.General.ReleaseFeedUrl;

        ApprovedCandidatesText = string.Join(", ", snapshot.Settings.Policy.ApprovedCandidates);
        WatchlistNote = snapshot.Settings.Policy.WatchlistNote;
        ProtectedBtcMode = snapshot.Settings.Policy.ProtectedBtcMode;
        ProtectedEthMode = snapshot.Settings.Policy.ProtectedEthMode;
        OperatorNotes = snapshot.Settings.Policy.OperatorNotes;
        if (_launchPolicyBaseline is null)
        {
            _launchPolicyBaseline = snapshot.Settings.Policy.AssetPolicies
                .Select(policy => new AssetPolicySettings { Asset = policy.Asset, Mode = policy.Mode, Notes = policy.Notes })
                .ToList();
            _launchProtectedBtcMode = snapshot.Settings.Policy.ProtectedBtcMode;
            _launchProtectedEthMode = snapshot.Settings.Policy.ProtectedEthMode;
        }

        SelectedRiskProfile = snapshot.Settings.Risk.ActiveProfile;
        CustomMaxPositionPct = snapshot.Settings.Risk.CustomMaxPositionPct.ToString("0.##");
        CustomDailyLossPct = snapshot.Settings.Risk.CustomDailyLossPct.ToString("0.##");
        CustomTurnoverPct = snapshot.Settings.Risk.CustomTurnoverPct.ToString("0.##");

        ConfigurationSummary = BuildConfigurationSummary(snapshot);
        CoinbaseApiKeyStatus = DescribeSecretStatus(snapshot.SecretStatuses, SecretNames.CoinbaseApiKey);
        CoinbaseApiSecretStatus = DescribeSecretStatus(snapshot.SecretStatuses, SecretNames.CoinbaseApiSecret);
        OpenAiApiKeyStatus = DescribeSecretStatus(snapshot.SecretStatuses, SecretNames.OpenAiApiKey);
        OpenAiApiKeyEditor = ResolveSecretEditorValue(snapshot.Paths.SecretsPath, SecretNames.OpenAiApiKey);
        ValidationSummary = string.Join(Environment.NewLine, snapshot.ValidationMessages);

        ValidationMessages.Clear();
        foreach (var message in snapshot.ValidationMessages)
        {
            ValidationMessages.Add(message);
        }

        _allAssetRows.Clear();
        _allAssetRows.AddRange(BuildAssetRows(snapshot.Settings.Policy));
        RefreshAssetRows();
        RefreshTopTradeAssets();

        SourceEntries.Clear();
        foreach (var entry in snapshot.Settings.Sources.Entries)
        {
            SourceEntries.Add(new SourceEntryViewModel(entry.Name, entry.Type, entry.State, entry.Scope, entry.Weight));
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
        PerformanceTopHoldingsSummary = "Connect Coinbase to see the current holdings mix.";
        PerformanceActivityMixSummary = "Connect Coinbase to see recent account-event mix.";
        PerformanceReadModelSummary = "Performance remains a truthful read-only snapshot until time-series analytics are added.";
        OverviewAppHealth = "Ready";
        OverviewCoinbaseStatus = "Not Connected";
        OverviewTotalValue = "Awaiting Coinbase connection";
        OverviewOpenPositions = "Awaiting Coinbase connection";
        OverviewUsdAvailable = "Awaiting Coinbase connection";
        OverviewRefreshSummary = "No Coinbase refresh has run yet.";
        AllocationSummary = "Connect Coinbase to see current allocation.";
        PortfolioTotalValue = "Awaiting Coinbase connection";
        PortfolioUnrealizedSummary = "Read-only current-state view";
        PortfolioCashStableSummary = "Awaiting Coinbase connection";
        PortfolioLargestExposure = "Awaiting Coinbase connection";
        PortfolioProtectedAssetsSummary = string.Join(", ", _allPolicyRules.Where(rule => !string.Equals(rule.Mode, "Allow Full Trade", StringComparison.OrdinalIgnoreCase)).Select(rule => rule.Asset));
        PortfolioCashShareSummary = "Awaiting Coinbase connection";
        PortfolioProtectedPolicyCount = "Awaiting approved assets";
        PortfolioRiskPostureSummary = "Awaiting Coinbase connection";
        PortfolioSelectedPositionSummary = "Select a live holding to inspect it in later phases.";
        PortfolioHistorySummary = "Historical portfolio tracking remains a later-phase feature.";
        InAppAlertsEnabled = true;
        EmailAlertsEnabled = true;
        SmsAlertsEnabled = false;
        AlertEmailAddress = "brad@example.com";
        AlertSmsNumber = "+1 555-0100";
        QuietHoursSummary = "Scheduling arrives in a later phase";
        LastAlertDeliveryTest = "Not Run";
        AllocationSlices.Clear();
        PortfolioHoldings.Clear();
        UpdateAdvancedThresholdsDisplay();

        if (_latestCoinbaseSnapshot is not null)
        {
            ApplyCoinbaseSnapshot(_latestCoinbaseSnapshot, false);
        }
        OnPropertyChanged(nameof(StatusBarDetail));
    }

    [RelayCommand]
    private async Task RefreshCoinbaseDataAsync()
    {
        await RefreshCoinbaseDataAsync(isStartup: false);
    }

    private async Task RefreshCoinbaseDataAsync(bool isStartup)
    {
        if (_coinbaseService is null)
        {
            CoinbaseConnectionSummary = "Unavailable";
            CoinbaseRefreshSummary = "Coinbase refresh service is not configured.";
            OnPropertyChanged(nameof(StatusBarDetail));
            return;
        }

        try
        {
            if (!isStartup)
            {
                RefreshCoinbaseButtonText = "Refreshing...";
                RefreshUniverseButtonText = "Refreshing...";
            }

            var snapshot = await _coinbaseService.RefreshAsync();
            ApplyCoinbaseSnapshot(snapshot, isStartup);
        }
        catch (Exception ex)
        {
            CoinbaseConnectionSummary = "Refresh Failed";
            CoinbaseRefreshSummary = $"Coinbase refresh failed: {ex.Message}";
            ShellStatus = "Refresh Failed";
            OnPropertyChanged(nameof(StatusBarDetail));
        }
        finally
        {
            RefreshCoinbaseButtonText = "Refresh Coinbase";
            RefreshUniverseButtonText = "Refresh Universe";
        }
    }

    [RelayCommand]
    private async Task SaveCoinbaseConnectionAsync()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SecretsPath)!);
            SaveCoinbaseJsonFile();
            LoadFromSnapshot(_settingsService.Load());
            ConnectionsActionMessage = "Coinbase connection settings saved.";

            if (_coinbaseService is not null)
            {
                await RefreshCoinbaseDataAsync(isStartup: false);
            }
        }
        catch (Exception ex)
        {
            ConnectionsActionMessage = $"Coinbase save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SaveOpenAiConnection()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SecretsPath)!);
            SaveOpenAiSecret();
            LoadFromSnapshot(_settingsService.Load());
            ConnectionsActionMessage = "OpenAI connection settings saved.";
        }
        catch (Exception ex)
        {
            ConnectionsActionMessage = $"OpenAI save failed: {ex.Message}";
        }
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

    private void ApplyCoinbaseSnapshot(CoinbaseShellSnapshot snapshot, bool isStartup)
    {
        _latestCoinbaseSnapshot = snapshot;
        CoinbaseConnectionSummary = snapshot.State switch
        {
            CoinbaseRefreshState.Connected => "Connected (Read-Only)",
            CoinbaseRefreshState.MissingCredentials => "Credentials Missing",
            CoinbaseRefreshState.IncompleteCredentials => "Credentials Incomplete",
            CoinbaseRefreshState.PermissionMismatch => "Permission Mismatch",
            CoinbaseRefreshState.TransportFailure => "Transport Failure",
            _ => "Refresh Failed",
        };

        CoinbaseRefreshSummary = snapshot.LastRefreshUtc is null
            ? snapshot.Messages.FirstOrDefault() ?? "No refresh data available."
            : $"{(isStartup ? "Startup" : "Manual")} refresh: {snapshot.LastRefreshUtc.Value.LocalDateTime:g}";

        if (snapshot.IsConnected)
        {
            ShellStatus = "Connected (Read-Only)";
            ShellMessage = "Coinbase read-only data is active. Trading remains inactive until later phases enable execution.";
            ReplaceAssetRowsWithCoinbaseData(snapshot);
            ReplacePolicyRulesFromApprovedUniverse();
            UpdateOverviewAndPortfolio(snapshot, isStartup);
            UpdateActivityFromCoinbase(snapshot);
        }
    }

    private void ReplaceAssetRowsWithCoinbaseData(CoinbaseShellSnapshot snapshot)
    {
        var approved = BuildSettingsFromEditor().Policy.ApprovedCandidates.ToHashSet(StringComparer.OrdinalIgnoreCase);

        _allAssetRows.Clear();
        foreach (var product in snapshot.Products
                     .Where(product => string.Equals(product.ProductType, "SPOT", StringComparison.OrdinalIgnoreCase))
                     .Where(product => string.Equals(product.QuoteCurrencyId, "USD", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(product.QuoteCurrencyId, "USDC", StringComparison.OrdinalIgnoreCase))
                     .GroupBy(product => product.BaseCurrencyId, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group
                         .OrderBy(product => string.Equals(product.QuoteCurrencyId, "USD", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                         .ThenBy(product => product.ProductId, StringComparer.OrdinalIgnoreCase)
                         .First())
                     .OrderBy(product => product.BaseCurrencyId, StringComparer.OrdinalIgnoreCase))
        {
            var state = approved.Contains(product.BaseCurrencyId)
                ? "Approved"
                : string.Equals(product.BaseCurrencyId, "DOGE", StringComparison.OrdinalIgnoreCase)
                    ? "Watchlist"
                    : "Observed";

            var priority = state switch
            {
                "Approved" when product.BaseCurrencyId is "BTC" or "ETH" => "High",
                "Approved" => "Medium",
                "Watchlist" => "Low",
                _ => "Observed",
            };

            _allAssetRows.Add(new AssetRowViewModel(product.BaseCurrencyId, state, priority, string.Empty));
        }

        RefreshAssetRows();
        RefreshTopTradeAssets();
    }

    private void ReplacePolicyRulesFromApprovedUniverse()
    {
        var settings = BuildSettingsFromEditor();
        var existingPolicies = settings.Policy.AssetPolicies.ToDictionary(assetPolicy => assetPolicy.Asset, StringComparer.OrdinalIgnoreCase);
        _allPolicyRules.Clear();

        foreach (var asset in _allAssetRows
                     .Where(asset => string.Equals(asset.State, "Approved", StringComparison.OrdinalIgnoreCase))
                     .Select(asset => asset.Asset)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(asset => asset, StringComparer.OrdinalIgnoreCase))
        {
            if (existingPolicies.TryGetValue(asset, out var assetPolicy))
            {
                _allPolicyRules.Add(new PolicyRuleViewModel(asset, assetPolicy.Mode, assetPolicy.Notes, false));
            }
            else
            {
                _allPolicyRules.Add(new PolicyRuleViewModel(asset, "Allow Full Trade", string.Empty, false));
            }
        }

        RefreshPolicyRules();
    }

    private void UpdateOverviewAndPortfolio(CoinbaseShellSnapshot snapshot, bool isStartup)
    {
        var accountValues = snapshot.Accounts
            .Select(account =>
            {
                var available = ParseAmount(account.AvailableValue);
                var hold = ParseAmount(account.HoldValue);
                var price = ResolveAccountPrice(snapshot.Products, account.Currency);
                return new
                {
                    account.Currency,
                    Total = available + hold,
                    UsdValue = (available + hold) * price,
                };
            })
            .Where(account => account.Total > 0m)
            .OrderByDescending(account => account.UsdValue)
            .ToList();

        var totalValue = accountValues.Sum(account => account.UsdValue);
        var cashStableValue = accountValues.Where(account => IsCashLike(account.Currency)).Sum(account => account.UsdValue);
        var openPositions = accountValues.Count(account => !IsCashLike(account.Currency) && account.UsdValue > 0.01m);
        var usdAvailable = accountValues
            .Where(account => string.Equals(account.Currency, "USD", StringComparison.OrdinalIgnoreCase))
            .Sum(account => account.Total);
        var largestHolding = accountValues.FirstOrDefault(account => !IsCashLike(account.Currency)) ?? accountValues.FirstOrDefault();

        OverviewCoinbaseStatus = CoinbaseConnectionSummary;
        OverviewTotalValue = FormatUsd(totalValue);
        OverviewOpenPositions = openPositions.ToString();
        OverviewUsdAvailable = FormatUsd(usdAvailable);
        OverviewRefreshSummary = snapshot.LastRefreshUtc is null
            ? "No Coinbase refresh has run yet."
            : $"{(isStartup ? "Startup" : "Manual")} refresh: {snapshot.LastRefreshUtc.Value.LocalDateTime:g} · {snapshot.Accounts.Count} accounts · {snapshot.Products.Count} products";

        PortfolioTotalValue = FormatUsd(totalValue);
        PortfolioUnrealizedSummary = "Current balances only";
        PortfolioCashStableSummary = FormatUsd(cashStableValue);
        PortfolioLargestExposure = largestHolding is null ? "No live holdings" : $"{FormatAssetLabel(largestHolding.Currency)} · {FormatUsd(largestHolding.UsdValue)}";
        PortfolioCashShareSummary = totalValue <= 0m ? "n/a" : $"{Math.Round(cashStableValue / totalValue * 100m, 1):0.#}% held as cash/stable";
        PortfolioProtectedAssetsSummary = string.Join(", ", _allPolicyRules
            .Where(rule => !string.Equals(rule.Mode, "Allow Full Trade", StringComparison.OrdinalIgnoreCase))
            .Select(rule => FormatAssetLabel(rule.Asset)));
        if (string.IsNullOrWhiteSpace(PortfolioProtectedAssetsSummary))
        {
            PortfolioProtectedAssetsSummary = "None";
        }
        PortfolioProtectedPolicyCount = _allPolicyRules.Count(rule => !string.Equals(rule.Mode, "Allow Full Trade", StringComparison.OrdinalIgnoreCase)) switch
        {
            0 => "0 constrained assets",
            1 => "1 constrained asset",
            var count => $"{count} constrained assets",
        };
        PortfolioRiskPostureSummary = openPositions switch
        {
            0 => "No deployed non-cash positions",
            1 => "Concentrated in a single deployed position",
            <= 3 => "Moderately concentrated current exposure",
            _ => "More distributed current exposure",
        };

        PortfolioSelectedPositionSummary = largestHolding is null
            ? "No live non-cash holdings are currently loaded."
            : $"{FormatAssetLabel(largestHolding.Currency)} is currently the largest non-cash exposure at {FormatUsd(largestHolding.UsdValue)}.";
        PortfolioHistorySummary = "Current balances are live. Historical portfolio time-series and realized-performance analytics remain later-phase work.";

        PortfolioHoldings.Clear();
        foreach (var holding in accountValues.Take(12))
        {
            var allocationPct = totalValue <= 0m ? 0m : Math.Round(holding.UsdValue / totalValue * 100m, 1);
            var mode = _allPolicyRules.FirstOrDefault(rule => string.Equals(rule.Asset, holding.Currency, StringComparison.OrdinalIgnoreCase))?.Mode
                       ?? (IsCashLike(holding.Currency) ? "Cash / Stable" : "Allow Full Trade");
            PortfolioHoldings.Add(new PortfolioHoldingViewModel(
                holding.Currency,
                holding.Total.ToString("0.########"),
                "n/a",
                FormatUsd(holding.UsdValue),
                $"{allocationPct:0.#}%",
                mode));
        }

        AllocationSlices.Clear();
        var visibleAllocations = accountValues.Where(account => account.UsdValue > 0.01m).Take(6).ToList();
        var runningPercent = 0d;
        for (var index = 0; index < visibleAllocations.Count; index++)
        {
            var holding = visibleAllocations[index];
            var percent = totalValue <= 0m ? 0d : Math.Round((double)(holding.UsdValue / totalValue * 100m), 1);
            AllocationSlices.Add(new AllocationSliceViewModel(
                holding.Currency,
                percent,
                FormatUsd(holding.UsdValue),
                AllocationPalette[index % AllocationPalette.Length])
            {
                PathData = BuildDonutSlicePath(runningPercent, percent),
            });
            runningPercent += percent;
        }

        AllocationSummary = totalValue <= 0m
            ? "No Coinbase balances are currently available."
            : $"{FormatUsd(totalValue)} total · {FormatUsd(cashStableValue)} cash/stable · {FormatUsd(totalValue - cashStableValue)} deployed.";

        var topHoldings = accountValues
            .Where(account => account.UsdValue > 0.01m)
            .Take(3)
            .Select(account => $"{FormatAssetLabel(account.Currency)} · {FormatUsd(account.UsdValue)}");
        PerformanceTopHoldingsSummary = topHoldings.Any()
            ? string.Join(" | ", topHoldings)
            : "No live holdings are currently available.";
    }

    private void UpdateActivityFromCoinbase(CoinbaseShellSnapshot snapshot)
    {
        _allActivityEntries.Clear();

        foreach (var transaction in snapshot.Transactions
                     .OrderByDescending(transaction => ParseDateTime(transaction.CreatedAt))
                     .Take(100))
        {
            var createdAt = ParseDateTime(transaction.CreatedAt);
            var asset = string.IsNullOrWhiteSpace(transaction.AmountCurrency) ? "Account" : transaction.AmountCurrency;
            var detail = string.IsNullOrWhiteSpace(transaction.Title) ? HumanizeWords(transaction.Type) : transaction.Title;
            var subtitle = string.IsNullOrWhiteSpace(transaction.Subtitle) ? "No additional Coinbase detail was returned." : transaction.Subtitle;

            _allActivityEntries.Add(new ActivityEntryViewModel(
                createdAt == DateTimeOffset.MinValue ? transaction.CreatedAt : createdAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
                asset,
                HumanizeTransactionAction(transaction),
                FormatTransactionAmount(transaction),
                string.IsNullOrWhiteSpace(transaction.Status) ? "Completed" : HumanizeWords(transaction.Status),
                detail,
                detail,
                subtitle,
                [new ActivitySourceContributionViewModel("Coinbase Account Activity", detail, "Read-Only")]));
        }

        RefreshActivityEntries();
        var groupedActions = _allActivityEntries
            .GroupBy(entry => entry.Action, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => $"{group.Key}: {group.Count()}");
        PerformanceActivityMixSummary = groupedActions.Any()
            ? string.Join(" · ", groupedActions)
            : "No Coinbase account events are currently available.";
    }

    private void SaveCoinbaseJsonFile()
    {
        if (string.IsNullOrWhiteSpace(CoinbaseJsonImportPath))
        {
            return;
        }

        var sourcePath = CoinbaseJsonImportPath.Trim();
        if (!File.Exists(sourcePath))
        {
            throw new InvalidOperationException("The specified Coinbase JSON key file path does not exist.");
        }

        var targetPath = Path.GetExtension(sourcePath).Equals(".txt", StringComparison.OrdinalIgnoreCase)
            ? VectorQuayPaths.Resolve(baseDirectory: AppContext.BaseDirectory).CoinbaseApiKeyJsonTextPath
            : VectorQuayPaths.Resolve(baseDirectory: AppContext.BaseDirectory).CoinbaseApiKeyJsonPath;

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath, true);
        CoinbaseJsonKeyFilePath = targetPath;
        CoinbaseJsonKeyFileStatus = "Detected";
    }

    public void SetCoinbaseJsonImportPath(string path)
    {
        CoinbaseJsonImportPath = path;
        ConnectionsActionMessage = $"Selected Coinbase JSON key file: {Path.GetFileName(path)}";
    }

    private void SaveOpenAiSecret()
    {
        var secrets = LoadSecretValues(SecretsPath);
        if (string.IsNullOrWhiteSpace(OpenAiApiKeyEditor))
        {
            secrets.Remove(SecretNames.OpenAiApiKey);
        }
        else
        {
            secrets[SecretNames.OpenAiApiKey] = OpenAiApiKeyEditor.Trim();
        }

        WriteSecretValues(SecretsPath, secrets);
    }

    private static Dictionary<string, string> LoadSecretValues(string secretsPath)
    {
        if (!File.Exists(secretsPath))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var parsed = SecretFileParser.ParseWithDiagnostics(File.ReadAllText(secretsPath));
        return new Dictionary<string, string>(parsed.Values, StringComparer.Ordinal);
    }

    private static void WriteSecretValues(string secretsPath, IReadOnlyDictionary<string, string> secrets)
    {
        var lines = secrets
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}");
        File.WriteAllLines(secretsPath, lines);
    }

    private static string ResolveSecretEditorValue(string secretsPath, string secretName)
    {
        var environmentValue = Environment.GetEnvironmentVariable(secretName);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return environmentValue;
        }

        if (!File.Exists(secretsPath))
        {
            return string.Empty;
        }

        var parsed = SecretFileParser.ParseWithDiagnostics(File.ReadAllText(secretsPath));
        return parsed.Values.TryGetValue(secretName, out var value) ? value : string.Empty;
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
            SelectedPolicyModeEditor = rule.Mode;
            SelectedPolicyNotesEditor = string.Empty;
        }
    }

    [RelayCommand]
    private void SaveSelectedPolicy()
    {
        var selectedRules = _allPolicyRules.Where(rule => rule.IsSelected).ToList();
        if (selectedRules.Count == 0)
        {
            SettingsActionMessage = "Check one or more policy rows before applying.";
            return;
        }

        if (!ProtectedAssetModes.Contains(SelectedPolicyModeEditor))
        {
            SettingsActionMessage = "Choose a valid policy mode before saving.";
            return;
        }

        foreach (var rule in selectedRules)
        {
            rule.Mode = SelectedPolicyModeEditor;
            rule.Notes = SelectedPolicyNotesEditor.Trim();
        }

        if (_allPolicyRules.FirstOrDefault(rule => string.Equals(rule.Asset, "BTC", StringComparison.OrdinalIgnoreCase)) is { } btcRule)
        {
            ProtectedBtcMode = btcRule.Mode;
        }

        if (_allPolicyRules.FirstOrDefault(rule => string.Equals(rule.Asset, "ETH", StringComparison.OrdinalIgnoreCase)) is { } ethRule)
        {
            ProtectedEthMode = ethRule.Mode;
        }

        try
        {
            var settings = BuildSettingsFromEditor();
            _settingsService.Save(settings);
            LoadFromSnapshot(_settingsService.Load());
            SettingsActionMessage = $"Applied {selectedRules.Count} policy update{(selectedRules.Count == 1 ? string.Empty : "s")}.";
            SelectedPolicyNotesEditor = string.Empty;
        }
        catch (Exception ex)
        {
            SettingsActionMessage = $"Policy save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void UndoSelectedPolicyChanges()
    {
        if (_launchPolicyBaseline is null)
        {
            SettingsActionMessage = "No session baseline is available to restore.";
            return;
        }

        foreach (var rule in _allPolicyRules)
        {
            var baseline = _launchPolicyBaseline.FirstOrDefault(policy => string.Equals(policy.Asset, rule.Asset, StringComparison.OrdinalIgnoreCase));
            if (baseline is null)
            {
                rule.Mode = "Allow Full Trade";
                rule.Notes = string.Empty;
            }
            else
            {
                rule.Mode = baseline.Mode;
                rule.Notes = baseline.Notes;
            }

            rule.IsSelected = false;
        }

        ProtectedBtcMode = _launchProtectedBtcMode ?? ProtectedBtcMode;
        ProtectedEthMode = _launchProtectedEthMode ?? ProtectedEthMode;
        AreAllPoliciesSelected = false;
        SelectedPolicyNotesEditor = string.Empty;
        SelectedPolicyModeEditor = "Allow Full Trade";

        var settings = BuildSettingsFromEditor();
        _settingsService.Save(settings);
        LoadFromSnapshot(_settingsService.Load());
        SettingsActionMessage = "Policy session changes were restored to the app-launch baseline.";
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
        SourceNameEditor = entry.Name;
        SourceScopeEditor = entry.Scope;
        SourceWeightEditor = entry.Weight;
        SourceActionMessage = $"Selected source: {entry.Name}.";
    }

    [RelayCommand]
    private void RunSourceAction(string? actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName))
        {
            return;
        }

        switch (actionName)
        {
            case "Apply Source":
                ApplySelectedSource();
                break;
            case "Remove Source":
                RemoveSelectedSource();
                break;
            case "View History":
                SourceActionMessage = SelectedSourceEntry is null
                    ? "Select a source first."
                    : $"{SelectedSourceEntry.Name} was loaded from local configuration. Historical source events are not tracked yet.";
                break;
            default:
                SourceActionMessage = "That source action is not available yet.";
                break;
        }
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

    public void AddOrUpdateSourceFromDialog(string name, string type, string scope, string weight, bool isNew, SourceEntryViewModel? existing)
    {
        var state = type == "Watcher" ? "Observed" : "Active";
        var entry = new SourceEntryViewModel(name.Trim(), type, state, scope.Trim(), string.IsNullOrWhiteSpace(weight) ? "Default" : weight.Trim());
        if (!isNew && existing is not null)
        {
            var index = SourceEntries.IndexOf(existing);
            if (index >= 0)
            {
                SourceEntries[index] = entry;
            }
        }
        else
        {
            SourceEntries.Add(entry);
        }

        PersistSources();
        if (SelectedSourceView == "Watchers" && !string.Equals(type, "Watcher", StringComparison.OrdinalIgnoreCase))
        {
            SelectedSourceView = "Direct Sources";
        }
        else if (SelectedSourceView == "Direct Sources" && string.Equals(type, "Watcher", StringComparison.OrdinalIgnoreCase))
        {
            SelectedSourceView = "Watchers";
        }

        SourceSearchText = string.Empty;
        SourceStateFilter = "All States";
        RefreshVisibleSourceEntries();
        SelectSourceEntry(entry);
        SourceActionMessage = isNew
            ? $"{entry.Name} was added to the local {(type == "Watcher" ? "watcher" : "source")} registry."
            : $"{entry.Name} was saved to the local {(type == "Watcher" ? "watcher" : "source")} registry.";
    }

    private void ApplySelectedSource()
    {
        if (SelectedSourceEntry is null)
        {
            SourceActionMessage = "Select a source first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SourceNameEditor))
        {
            SourceActionMessage = "Source name is required.";
            return;
        }

        AddOrUpdateSourceFromDialog(SourceNameEditor, SelectedSourceEntry.Type, SourceScopeEditor, SourceWeightEditor, false, SelectedSourceEntry);
    }

    private void RemoveSelectedSource()
    {
        if (SelectedSourceEntry is null)
        {
            SourceActionMessage = "Select a source first.";
            return;
        }

        var removedName = SelectedSourceEntry.Name;
        SourceEntries.Remove(SelectedSourceEntry);
        PersistSources();
        RefreshVisibleSourceEntries();
        SelectedSourceEntry = VisibleSourceEntries.FirstOrDefault();
        if (SelectedSourceEntry is not null)
        {
            SelectSourceEntry(SelectedSourceEntry);
        }
        else
        {
            SourceNameEditor = string.Empty;
            SourceScopeEditor = string.Empty;
            SourceWeightEditor = "Default";
        }

        SourceActionMessage = $"{removedName} removed from local source configuration.";
    }

    private void PersistSources()
    {
        var settings = BuildSettingsFromEditor();
        _settingsService.Save(settings);
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
            "7D" => "7D view selected. Current balances are live; rolling weekly return measurement arrives once snapshots are persisted.",
            "30D" => "30D view selected. This is a truthful read-only state summary for now, not realized performance.",
            "90D" => "90D view selected. Broader trend analytics remain a later-phase feature.",
            "1Y" => "1Y view selected. Long-horizon return tracking requires historical snapshots that are not stored yet.",
            "ALL" => "All-time view selected. The page remains a truthful snapshot until persistent performance history is added.",
            _ => "24H view selected. Current balances and recent Coinbase events are live; full return analytics still require time-series history.",
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
        UpdateAdvancedThresholdsDisplay();
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
                AssetPolicies = _allPolicyRules
                    .Select(rule => new AssetPolicySettings
                    {
                        Asset = rule.Asset,
                        Mode = rule.Mode,
                        Notes = rule.Notes,
                    })
                    .ToList(),
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
                        Weight = entry.Weight,
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
            yield return new AssetRowViewModel(asset, "Approved", priority, string.Empty);
        }

        yield return new AssetRowViewModel("DOGE", "Watchlist", "Low", string.Empty);
        yield return new AssetRowViewModel("New Listing", "Observed", "Pending", string.Empty);
    }

    private static IEnumerable<PolicyRuleViewModel> BuildPolicyRules(PolicySettings policy)
    {
        var policyLookup = policy.AssetPolicies
            .Where(assetPolicy => !string.IsNullOrWhiteSpace(assetPolicy.Asset))
            .ToDictionary(assetPolicy => assetPolicy.Asset, StringComparer.OrdinalIgnoreCase);

        foreach (var asset in policy.ApprovedCandidates
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(asset => asset, StringComparer.OrdinalIgnoreCase))
        {
            if (policyLookup.TryGetValue(asset, out var assetPolicy))
            {
                yield return new PolicyRuleViewModel(asset, assetPolicy.Mode, assetPolicy.Notes, false);
            }
            else
            {
                yield return new PolicyRuleViewModel(
                    asset,
                    asset switch
                    {
                        "BTC" => policy.ProtectedBtcMode,
                        "ETH" => policy.ProtectedEthMode,
                        _ => "Allow Full Trade",
                    },
                    string.Empty,
                    false);
            }
        }
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

    private void UpdateAdvancedThresholdsDisplay()
    {
        switch (SelectedRiskProfile)
        {
            case "High Risk":
                AdvancedThresholdsSummary = "High Risk keeps tighter monitoring but allows wider execution tolerance and faster turnover when later phases enable strategy logic.";
                AdvancedSpreadToleranceSummary = "0.85% spread tolerance";
                AdvancedLiquidityMinimumSummary = "$100K minimum 24H liquidity";
                AdvancedConfidenceCutoffSummary = "0.50 confidence cutoff";
                break;
            case "Low Risk":
                AdvancedThresholdsSummary = "Low Risk favors deeper liquidity, tighter spreads, and higher signal quality before any future execution is allowed.";
                AdvancedSpreadToleranceSummary = "0.25% spread tolerance";
                AdvancedLiquidityMinimumSummary = "$500K minimum 24H liquidity";
                AdvancedConfidenceCutoffSummary = "0.75 confidence cutoff";
                break;
            case "Custom":
                AdvancedThresholdsSummary = "Custom mode keeps these advanced thresholds illustrative for now. In later phases they will combine with your custom position and loss controls.";
                AdvancedSpreadToleranceSummary = $"Spread tolerance aligned to custom mode · {CustomDailyLossPct}% daily loss shell";
                AdvancedLiquidityMinimumSummary = $"Liquidity floor aligned to custom mode · {CustomMaxPositionPct}% position shell";
                AdvancedConfidenceCutoffSummary = $"Confidence cutoff aligned to custom mode · {CustomTurnoverPct}% turnover shell";
                break;
            default:
                AdvancedThresholdsSummary = "Medium Risk balances execution tolerance, liquidity quality, and signal strictness for a measured default operating posture.";
                AdvancedSpreadToleranceSummary = "0.40% spread tolerance";
                AdvancedLiquidityMinimumSummary = "$250K minimum 24H liquidity";
                AdvancedConfidenceCutoffSummary = "0.60 confidence cutoff";
                break;
        }
    }

    private static string FormatAssetLabel(string symbol)
    {
        var metadata = AssetMetadataCatalog.Resolve(symbol);
        return string.Equals(metadata.Name, metadata.Symbol, StringComparison.OrdinalIgnoreCase)
            ? metadata.Symbol
            : $"{metadata.Name} ({metadata.Symbol})";
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
                            asset.Asset.Contains(AssetSearchText, StringComparison.OrdinalIgnoreCase))
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

        OnPropertyChanged(nameof(AssetCountSummary));
    }

    private void RefreshTopTradeAssets()
    {
        TopTradeAssetItems.Clear();
        foreach (var asset in _allAssetRows
                     .Where(asset => string.Equals(asset.State, "Approved", StringComparison.OrdinalIgnoreCase))
                     .Take(10))
        {
            TopTradeAssetItems.Add(new TopTradeAssetViewModel(asset.AssetSymbol, asset.State, asset.Priority));
        }
    }

    private void RefreshPolicyRules()
    {
        var filtered = _allPolicyRules
            .Where(rule => string.IsNullOrWhiteSpace(PolicySearchText) ||
                           rule.Asset.Contains(PolicySearchText, StringComparison.OrdinalIgnoreCase) ||
                           rule.Mode.Contains(PolicySearchText, StringComparison.OrdinalIgnoreCase) ||
                           rule.Notes.Contains(PolicySearchText, StringComparison.OrdinalIgnoreCase))
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
        AreAllPoliciesSelected = VisiblePolicyRules.Count > 0 && VisiblePolicyRules.All(rule => rule.IsSelected);
        OnPropertyChanged(nameof(PolicyCountSummary));
        OnPropertyChanged(nameof(HasPolicySelection));
    }

    private void RefreshActivityEntries()
    {
        _isRefreshingActivityFilters = true;
        try
        {
        if (string.IsNullOrWhiteSpace(ActivityAssetFilter))
        {
            ActivityAssetFilter = "All Assets";
        }

        if (string.IsNullOrWhiteSpace(ActivityActionFilter))
        {
            ActivityActionFilter = "All Actions";
        }

        if (string.IsNullOrWhiteSpace(ActivityOutcomeFilter))
        {
            ActivityOutcomeFilter = "All Outcomes";
        }

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
        RecentOverviewActivityItems.Clear();
        foreach (var item in _allActivityEntries.Take(5))
        {
            RecentOverviewActivityItems.Add(new OverviewActivityRowViewModel(
                item.Timestamp,
                item.AssetPair,
                item.Action,
                item.Amount));
        }
        ReplaceOptions(ActivityAssetOptions, ["All Assets", .. _allActivityEntries.Select(entry => entry.AssetPair).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(asset => asset, StringComparer.OrdinalIgnoreCase)]);
        ReplaceOptions(ActivityActionOptions, ["All Actions", .. _allActivityEntries.Select(entry => entry.Action).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(action => action, StringComparer.OrdinalIgnoreCase)]);
        ReplaceOptions(ActivityOutcomeOptions, ["All Outcomes", .. _allActivityEntries.Select(entry => entry.Outcome).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(outcome => outcome, StringComparer.OrdinalIgnoreCase)]);
        if (!ActivityAssetOptions.Contains(ActivityAssetFilter))
        {
            ActivityAssetFilter = "All Assets";
        }

        if (!ActivityActionOptions.Contains(ActivityActionFilter))
        {
            ActivityActionFilter = "All Actions";
        }

        if (!ActivityOutcomeOptions.Contains(ActivityOutcomeFilter))
        {
            ActivityOutcomeFilter = "All Outcomes";
        }

        OnPropertyChanged(nameof(ActivityEntryCountSummary));
        OnPropertyChanged(nameof(HasActivityEntries));
        OnPropertyChanged(nameof(ShowNoActivityEntries));
        OnPropertyChanged(nameof(ActivityCountSummary));
        }
        finally
        {
            _isRefreshingActivityFilters = false;
        }
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
        OnPropertyChanged(nameof(SourceCountSummary));
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
        OnPropertyChanged(nameof(IsHighRiskProfile));
        OnPropertyChanged(nameof(IsMediumRiskProfile));
        OnPropertyChanged(nameof(IsLowRiskProfile));
        OnPropertyChanged(nameof(IsCustomRiskProfile));
        UpdateAdvancedThresholdsDisplay();
    }

    partial void OnCustomMaxPositionPctChanged(string value)
    {
        if (SelectedRiskProfile == "Custom")
        {
            UpdateAdvancedThresholdsDisplay();
        }
    }

    partial void OnCustomDailyLossPctChanged(string value)
    {
        if (SelectedRiskProfile == "Custom")
        {
            UpdateAdvancedThresholdsDisplay();
        }
    }

    partial void OnCustomTurnoverPctChanged(string value)
    {
        if (SelectedRiskProfile == "Custom")
        {
            UpdateAdvancedThresholdsDisplay();
        }
    }

    partial void OnAssetSearchTextChanged(string value) => RefreshAssetRows();

    partial void OnAssetStateFilterChanged(string value) => RefreshAssetRows();

    partial void OnAssetSortOrderChanged(string value) => RefreshAssetRows();

    partial void OnPolicySearchTextChanged(string value) => RefreshPolicyRules();

    partial void OnPolicyModeFilterChanged(string value) => RefreshPolicyRules();

    partial void OnActivityAssetFilterChanged(string value)
    {
        if (_isRefreshingActivityFilters)
        {
            return;
        }

        RefreshActivityEntries();
    }

    partial void OnActivityActionFilterChanged(string value)
    {
        if (_isRefreshingActivityFilters)
        {
            return;
        }

        RefreshActivityEntries();
    }

    partial void OnActivityOutcomeFilterChanged(string value)
    {
        if (_isRefreshingActivityFilters)
        {
            return;
        }

        RefreshActivityEntries();
    }

    partial void OnSelectedActivityEntryChanged(ActivityEntryViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedActivitySources));
        if (value is null)
        {
            ActivityDetailTitle = "No activity selected";
            ActivityDetailTimestamp = string.Empty;
            ActivityDetailAmount = string.Empty;
            ActivityDetailStatus = string.Empty;
            ActivityDetailSummary = "Select a Coinbase account event to inspect its details.";
            ActivitySelectionMessage = string.Empty;
            return;
        }

        ActivityDetailTitle = $"{value.AssetName} ({value.AssetSymbol}) · {value.Action}";
        ActivityDetailTimestamp = value.Timestamp;
        ActivityDetailAmount = value.Amount;
        ActivityDetailStatus = value.Outcome;
        ActivityDetailSummary = value.DecisionSummary;
        ActivitySelectionMessage = string.Empty;
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
        if (value is not null)
        {
            SourceNameEditor = value.Name;
            SourceScopeEditor = value.Scope;
            SourceWeightEditor = value.Weight;
        }
    }

    partial void OnShellStatusChanged(string value)
    {
        OnPropertyChanged(nameof(StatusBarIndicatorBrush));
        OnPropertyChanged(nameof(StatusBarSummary));
    }

    partial void OnCoinbaseApiKeyStatusChanged(string value)
    {
        OnPropertyChanged(nameof(StatusBarIndicatorBrush));
        OnPropertyChanged(nameof(StatusBarSummary));
    }

    partial void OnCoinbaseApiSecretStatusChanged(string value)
    {
        OnPropertyChanged(nameof(StatusBarIndicatorBrush));
        OnPropertyChanged(nameof(StatusBarSummary));
    }

    partial void OnCoinbaseConnectionSummaryChanged(string value)
    {
        OnPropertyChanged(nameof(StatusBarIndicatorBrush));
        OnPropertyChanged(nameof(StatusBarSummary));
        OnPropertyChanged(nameof(StatusBarDetail));
    }

    partial void OnRefreshCoinbaseButtonTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsRefreshingCoinbase));
        OnPropertyChanged(nameof(CanRefreshCoinbase));
    }

    partial void OnRefreshUniverseButtonTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsRefreshingCoinbase));
        OnPropertyChanged(nameof(CanRefreshCoinbase));
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

    partial void OnAreAllPoliciesSelectedChanged(bool value)
    {
        foreach (var rule in VisiblePolicyRules)
        {
            rule.IsSelected = value;
        }

        OnPropertyChanged(nameof(HasPolicySelection));
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

    private static void ReplaceOptions(ObservableCollection<string> target, IEnumerable<string> values)
    {
        var normalized = values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (target.SequenceEqual(normalized, StringComparer.Ordinal))
        {
            return;
        }

        target.Clear();
        foreach (var value in normalized)
        {
            target.Add(value);
        }
    }

    private static decimal ParseAmount(string? value) => decimal.TryParse(value, out var parsed) ? parsed : 0m;

    private static string FormatUsd(decimal value) => value.ToString("C2");

    private static decimal ResolveAccountPrice(IEnumerable<CoinbaseProduct> products, string currency)
    {
        if (IsCashLike(currency))
        {
            return 1m;
        }

        var product = products
            .Where(product => string.Equals(product.BaseCurrencyId, currency, StringComparison.OrdinalIgnoreCase))
            .Where(product => string.Equals(product.QuoteCurrencyId, "USD", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(product.QuoteCurrencyId, "USDC", StringComparison.OrdinalIgnoreCase))
            .OrderBy(product => string.Equals(product.QuoteCurrencyId, "USD", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .FirstOrDefault();

        return decimal.TryParse(product?.Price, out var parsed) ? parsed : 0m;
    }

    private static bool IsCashLike(string currency) =>
        string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currency, "USDC", StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset ParseDateTime(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;

    private static string HumanizeTransactionAction(CoinbaseTransaction transaction)
    {
        var title = $"{transaction.Title} {transaction.Type}".Trim();
        if (title.Contains("reward", StringComparison.OrdinalIgnoreCase))
        {
            return "Reward";
        }

        if (title.Contains("buy", StringComparison.OrdinalIgnoreCase))
        {
            return "Buy";
        }

        if (title.Contains("sell", StringComparison.OrdinalIgnoreCase))
        {
            return "Sell";
        }

        if (title.Contains("deposit", StringComparison.OrdinalIgnoreCase) || title.Contains("receive", StringComparison.OrdinalIgnoreCase))
        {
            return "Deposit";
        }

        if (title.Contains("withdraw", StringComparison.OrdinalIgnoreCase) || title.Contains("send", StringComparison.OrdinalIgnoreCase))
        {
            return "Withdrawal";
        }

        return HumanizeWords(transaction.Type);
    }

    private static string HumanizeWords(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown";
        }

        return string.Join(" ", value
            .Replace("_", " ", StringComparison.Ordinal)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));
    }

    private static string FormatTransactionAmount(CoinbaseTransaction transaction)
    {
        if (decimal.TryParse(transaction.NativeAmount, out var nativeValue) &&
            string.Equals(transaction.NativeCurrency, "USD", StringComparison.OrdinalIgnoreCase))
        {
            return nativeValue.ToString("C2");
        }

        if (decimal.TryParse(transaction.Amount, out var amountValue))
        {
            return $"{amountValue:0.########} {transaction.AmountCurrency}".Trim();
        }

        return transaction.Amount;
    }

    private static string BuildDonutSlicePath(double startPercent, double sweepPercent)
    {
        const double centerX = 82d;
        const double centerY = 82d;
        const double radius = 51d;

        if (sweepPercent <= 0d)
        {
            return string.Empty;
        }

        if (sweepPercent >= 99.9d)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "M {0},{1} A {2},{2} 0 1 1 {3},{1} A {2},{2} 0 1 1 {0},{1}",
                centerX + radius,
                centerY,
                radius,
                centerX - radius);
        }

        var startAngle = -90d + (startPercent / 100d * 360d);
        var endAngle = startAngle + (sweepPercent / 100d * 360d);
        var largeArc = sweepPercent >= 50d ? 1 : 0;
        var start = PolarPoint(centerX, centerY, radius, startAngle);
        var end = PolarPoint(centerX, centerY, radius, endAngle);

        return string.Format(
            CultureInfo.InvariantCulture,
            "M {0},{1} A {2},{2} 0 {3} 1 {4},{5}",
            start.X,
            start.Y,
            radius,
            largeArc,
            end.X,
            end.Y);
    }

    private static (double X, double Y) PolarPoint(double centerX, double centerY, double radius, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180d;
        return (centerX + radius * Math.Cos(radians), centerY + radius * Math.Sin(radians));
    }
}

public sealed class AssetRowViewModel(string asset, string state, string priority, string notes)
{
    private readonly AssetMetadata _metadata = AssetMetadataCatalog.Resolve(asset);

    public string Asset { get; } = asset;

    public string AssetSymbol => _metadata.Symbol;

    public string AssetName => _metadata.Name;

    public string AssetIconPath => _metadata.IconPath;

    public Bitmap? AssetIcon => AssetIconCache.Load(AssetIconPath);

    public bool HasAssetIcon => AssetIcon is not null;

    public bool ShowAssetFallback => !HasAssetIcon;

    public string AssetMonogram => AssetSymbol.Length >= 2 ? AssetSymbol[..2] : AssetSymbol;

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

public partial class PolicyRuleViewModel : ObservableObject
{
    public PolicyRuleViewModel(string asset, string mode, string notes, bool isSelected)
    {
        _metadata = AssetMetadataCatalog.Resolve(asset);
        Asset = asset;
        this.mode = mode;
        this.notes = notes;
        this.isSelected = isSelected;
    }

    private readonly AssetMetadata _metadata;

    public string Asset { get; }

    public string AssetSymbol => _metadata.Symbol;

    public string AssetName => _metadata.Name;

    public string AssetIconPath => _metadata.IconPath;

    public Bitmap? AssetIcon => AssetIconCache.Load(AssetIconPath);

    public bool HasAssetIcon => AssetIcon is not null;

    public bool ShowAssetFallback => !HasAssetIcon;

    public string AssetMonogram => AssetSymbol.Length >= 2 ? AssetSymbol[..2] : AssetSymbol;

    [ObservableProperty]
    private string mode;

    [ObservableProperty]
    private string notes;

    [ObservableProperty]
    private bool isSelected;
}

public sealed class AllocationSliceViewModel(string asset, double percent, string usdValue, string brush)
{
    private readonly AssetMetadata _metadata = AssetMetadataCatalog.Resolve(asset);

    public string Asset { get; } = asset;

    public string AssetSymbol => _metadata.Symbol;

    public string AssetName => _metadata.Name;

    public string AssetIconPath => _metadata.IconPath;

    public Bitmap? AssetIcon => AssetIconCache.Load(AssetIconPath);

    public bool HasAssetIcon => AssetIcon is not null;

    public bool ShowAssetFallback => !HasAssetIcon;

    public string AssetMonogram => AssetSymbol.Length >= 2 ? AssetSymbol[..2] : AssetSymbol;

    public double Percent { get; } = percent;

    public string PercentText => $"{Percent:0.#}%";

    public string UsdValue { get; } = usdValue;

    public string Brush { get; } = brush;

    public string PathData { get; init; } = string.Empty;

    public string TooltipText => $"{AssetName} ({AssetSymbol}) · {PercentText} · {UsdValue}";
}

public sealed class OverviewActivityRowViewModel(string timestamp, string asset, string action, string amount)
{
    private readonly AssetMetadata _metadata = AssetMetadataCatalog.Resolve(asset);

    public string Timestamp { get; } = timestamp;

    public string Asset { get; } = asset;

    public string AssetSymbol => _metadata.Symbol;

    public string AssetName => _metadata.Name;

    public string AssetIconPath => _metadata.IconPath;

    public Bitmap? AssetIcon => AssetIconCache.Load(AssetIconPath);

    public bool HasAssetIcon => AssetIcon is not null;

    public bool ShowAssetFallback => !HasAssetIcon;

    public string AssetMonogram => AssetSymbol.Length >= 2 ? AssetSymbol[..2] : AssetSymbol;

    public string Action { get; } = action;

    public string Amount { get; } = amount;
}

public sealed class PortfolioHoldingViewModel(string asset, string quantity, string avgCost, string value, string allocation, string notes)
{
    private readonly AssetMetadata _metadata = AssetMetadataCatalog.Resolve(asset);

    public string Asset { get; } = asset;

    public string AssetSymbol => _metadata.Symbol;

    public string AssetName => _metadata.Name;

    public string AssetIconPath => _metadata.IconPath;

    public Bitmap? AssetIcon => AssetIconCache.Load(AssetIconPath);

    public bool HasAssetIcon => AssetIcon is not null;

    public bool ShowAssetFallback => !HasAssetIcon;

    public string AssetMonogram => AssetSymbol.Length >= 2 ? AssetSymbol[..2] : AssetSymbol;

    public string Quantity { get; } = quantity;

    public string AvgCost { get; } = avgCost;

    public string Value { get; } = value;

    public string Allocation { get; } = allocation;

    public string Notes { get; } = notes;
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
    private readonly AssetMetadata _metadata = AssetMetadataCatalog.Resolve(assetPair);

    public string Timestamp { get; } = timestamp;

    public string AssetPair { get; } = assetPair;

    public string AssetSymbol => _metadata.Symbol;

    public string AssetName => _metadata.Name;

    public string AssetIconPath => _metadata.IconPath;

    public Bitmap? AssetIcon => AssetIconCache.Load(AssetIconPath);

    public bool HasAssetIcon => AssetIcon is not null;

    public bool ShowAssetFallback => !HasAssetIcon;

    public string AssetMonogram => AssetSymbol.Length >= 2 ? AssetSymbol[..2] : AssetSymbol;

    public string Action { get; } = action;

    public string Amount { get; } = amount;

    public string Outcome { get; } = outcome;

    public string Confidence { get; } = confidence;

    public string DecisionSummary { get; } = decisionSummary;

    public string PolicyRiskSummary { get; } = policyRiskSummary;

    public IReadOnlyList<ActivitySourceContributionViewModel> Sources { get; } = sources;
}

public sealed class TopTradeAssetViewModel(string asset, string state, string priority)
{
    private readonly AssetMetadata _metadata = AssetMetadataCatalog.Resolve(asset);

    public string Asset { get; } = asset;

    public string AssetSymbol => _metadata.Symbol;

    public string AssetName => _metadata.Name;

    public string AssetIconPath => _metadata.IconPath;

    public Bitmap? AssetIcon => AssetIconCache.Load(AssetIconPath);

    public bool HasAssetIcon => AssetIcon is not null;

    public bool ShowAssetFallback => !HasAssetIcon;

    public string AssetMonogram => AssetSymbol.Length >= 2 ? AssetSymbol[..2] : AssetSymbol;

    public string State { get; } = state;

    public string Priority { get; } = priority;
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

public sealed class SourceEntryViewModel(string name, string type, string state, string scope, string weight)
{
    public string Name { get; } = name;

    public string Type { get; } = type;

    public string State { get; } = state;

    public string Weight { get; } = weight;

    public string Scope { get; } = scope;

    public bool IsWatcher => Type.Contains("Watcher", StringComparison.OrdinalIgnoreCase);

    public bool IsDirectSource => !IsWatcher;
}

public sealed record ReleaseCheckResult(string Version, string Name, string ActionUrl);
