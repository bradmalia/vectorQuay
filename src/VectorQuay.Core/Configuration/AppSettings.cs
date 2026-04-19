namespace VectorQuay.Core.Configuration;

public sealed class AppSettings
{
    public const string DefaultReleaseFeedUrl = "https://api.github.com/repos/bradmalia/vectorQuay/releases/latest";

    public GeneralSettings General { get; set; } = new();

    public PolicySettings Policy { get; set; } = new();

    public RiskSettings Risk { get; set; } = new();

    public SourceSettings Sources { get; set; } = new();

    public static AppSettings CreateDefault()
    {
        return new AppSettings
        {
            General = new GeneralSettings
            {
                ApplicationState = "Pre-Integration",
                ValuationCurrency = "USD",
                AllowUsdcSecondary = true,
                ReleaseFeedUrl = DefaultReleaseFeedUrl,
            },
            Policy = new PolicySettings
            {
                ApprovedCandidates =
                [
                    "BTC",
                    "ETH",
                    "SOL",
                    "XRP",
                    "ADA",
                    "LINK",
                    "LTC",
                    "XLM",
                    "BCH",
                    "UNI",
                ],
                WatchlistNote = "DOGE remains on the conditional watchlist. It is visible for review but not part of the default approved live set.",
                ProtectedBtcMode = "Do Not Sell",
                ProtectedEthMode = "Do Not Trade",
                OperatorNotes = "No trade remains a valid outcome when no candidate passes screening.",
                AssetPolicies =
                [
                    new AssetPolicySettings { Asset = "BTC", Mode = "Do Not Sell", Notes = "USD preferred" },
                    new AssetPolicySettings { Asset = "ETH", Mode = "Do Not Trade", Notes = "Initial holding" },
                    new AssetPolicySettings { Asset = "SOL", Mode = "Allow Full Trade", Notes = string.Empty },
                    new AssetPolicySettings { Asset = "XRP", Mode = "Allow Full Trade", Notes = string.Empty },
                    new AssetPolicySettings { Asset = "ADA", Mode = "Allow Full Trade", Notes = string.Empty },
                    new AssetPolicySettings { Asset = "LINK", Mode = "Allow Full Trade", Notes = string.Empty },
                    new AssetPolicySettings { Asset = "LTC", Mode = "Allow Full Trade", Notes = string.Empty },
                    new AssetPolicySettings { Asset = "XLM", Mode = "Allow Full Trade", Notes = string.Empty },
                    new AssetPolicySettings { Asset = "BCH", Mode = "Allow Full Trade", Notes = string.Empty },
                    new AssetPolicySettings { Asset = "UNI", Mode = "Allow Full Trade", Notes = string.Empty },
                ],
            },
            Risk = new RiskSettings
            {
                ActiveProfile = "Medium Risk",
                CustomMaxPositionPct = 12m,
                CustomDailyLossPct = 3m,
                CustomTurnoverPct = 25m,
            },
            Sources = new SourceSettings
            {
                Entries =
                [
                    new SourceEntrySettings
                    {
                        Name = "Coinbase Advanced Trade",
                        Type = "Direct Source",
                        State = "Active",
                        Weight = "Default",
                        Scope = "Approved exchange surface for later read-only integration",
                    },
                    new SourceEntrySettings
                    {
                        Name = "Crypto News Watcher",
                        Type = "Watcher",
                        State = "Observed",
                        Weight = "Baseline",
                        Scope = "Reserved AI-assisted watcher workflow for future signal expansion",
                    },
                    new SourceEntrySettings
                    {
                        Name = "r/CryptoCurrency Monitor",
                        Type = "Watcher",
                        State = "Needs Review",
                        Weight = "Review",
                        Scope = "Placeholder for future Reddit/topic watcher onboarding",
                    },
                ],
            },
        };
    }
}

public sealed class GeneralSettings
{
    public string ApplicationState { get; set; } = "Pre-Integration";

    public string ValuationCurrency { get; set; } = "USD";

    public bool AllowUsdcSecondary { get; set; } = true;

    public string ReleaseFeedUrl { get; set; } = AppSettings.DefaultReleaseFeedUrl;
}

public sealed class PolicySettings
{
    public List<string> ApprovedCandidates { get; set; } = [];

    public string WatchlistNote { get; set; } = string.Empty;

    public string ProtectedBtcMode { get; set; } = "Do Not Sell";

    public string ProtectedEthMode { get; set; } = "Do Not Trade";

    public string OperatorNotes { get; set; } = string.Empty;

    public List<AssetPolicySettings> AssetPolicies { get; set; } = [];
}

public sealed class AssetPolicySettings
{
    public string Asset { get; set; } = string.Empty;

    public string Mode { get; set; } = "Allow Full Trade";

    public string Notes { get; set; } = string.Empty;
}

public sealed class RiskSettings
{
    public string ActiveProfile { get; set; } = "Medium Risk";

    public decimal CustomMaxPositionPct { get; set; } = 12m;

    public decimal CustomDailyLossPct { get; set; } = 3m;

    public decimal CustomTurnoverPct { get; set; } = 25m;
}

public sealed class SourceSettings
{
    public List<SourceEntrySettings> Entries { get; set; } = [];
}

public sealed class SourceEntrySettings
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string Weight { get; set; } = "Default";

    public string Scope { get; set; } = string.Empty;
}
