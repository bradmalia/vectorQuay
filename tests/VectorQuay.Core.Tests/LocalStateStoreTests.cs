using System.Text.Json;
using VectorQuay.Core.Coinbase;
using VectorQuay.Core.Configuration;
using VectorQuay.Core.Persistence;

namespace VectorQuay.Core.Tests;

public sealed class LocalStateStoreTests
{
    [Fact]
    public void SaveCoinbaseSnapshot_RoundTripsSnapshotAndHistory()
    {
        var paths = CreateTestPaths();
        var store = new LocalStateStore(paths);
        var snapshot = CreateConnectedSnapshot();

        store.SaveCoinbaseSnapshot(snapshot);

        var restored = store.LoadSnapshot();

        Assert.Equal(RecoveryTruthState.RestoredFromCache, restored.State);
        Assert.NotNull(restored.Snapshot);
        Assert.Equal(CoinbaseRefreshState.Connected, restored.Snapshot!.RefreshState);
        Assert.Equal(2, restored.Snapshot.Holdings.Count);
        Assert.NotEmpty(store.LoadRecentActivity());
        Assert.NotEmpty(store.LoadPortfolioValueHistory());
        Assert.NotEmpty(store.LoadAuditEvents());
        Assert.Equal(LocalStateStore.CurrentSchemaVersion, File.ReadAllText(paths.DataSchemaVersionPath));
    }

    [Fact]
    public void LoadSnapshot_ReturnsCorruptedStateForMalformedSnapshot()
    {
        var paths = CreateTestPaths();
        Directory.CreateDirectory(Path.GetDirectoryName(paths.LastSnapshotPath)!);
        File.WriteAllText(paths.LastSnapshotPath, "{ definitely-not-json");

        var store = new LocalStateStore(paths);
        var restored = store.LoadSnapshot();

        Assert.Equal(RecoveryTruthState.CorruptedState, restored.State);
        Assert.Null(restored.Snapshot);
        Assert.Contains("could not be loaded safely", restored.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadSnapshot_ReturnsCorruptedStateForIncompatibleSchemaVersion()
    {
        var paths = CreateTestPaths();
        var store = new LocalStateStore(paths);
        store.SaveCoinbaseSnapshot(CreateConnectedSnapshot());
        File.WriteAllText(paths.DataSchemaVersionPath, "phase3-v999");

        var restored = store.LoadSnapshot();

        Assert.Equal(RecoveryTruthState.CorruptedState, restored.State);
        Assert.Null(restored.Snapshot);
        Assert.Contains("incompatible", restored.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppendAuditEvent_RedactsSensitiveFields()
    {
        var paths = CreateTestPaths();
        var store = new LocalStateStore(paths);

        store.AppendAuditEvent(new PersistedAuditEvent(
            EventId: "evt-1",
            TimestampUtc: DateTimeOffset.UtcNow,
            EventType: "ConfigurationUpdate",
            Origin: "Operator",
            Summary: "Saved connection settings",
            CorrelationId: null,
            Detail: new Dictionary<string, string?>
            {
                ["apiKey"] = "sk-secret-value",
                ["authorization"] = "Bearer abc123",
                ["operatorNote"] = "safe text",
            }));

        var events = store.LoadAuditEvents();

        Assert.Single(events);
        Assert.Equal("[REDACTED]", events[0].Detail["apiKey"]);
        Assert.Equal("[REDACTED]", events[0].Detail["authorization"]);
        Assert.Equal("safe text", events[0].Detail["operatorNote"]);
    }

    [Fact]
    public void AppendAlert_AndAudit_RetainOnlyBoundedHistory()
    {
        var paths = CreateTestPaths();
        var store = new LocalStateStore(paths);

        for (var index = 0; index < LocalStateStore.AlertRetentionCount + 10; index++)
        {
            store.AppendAlert(new PersistedAlertEvent(
                EventId: $"alert-{index}",
                TimestampUtc: DateTimeOffset.UtcNow.AddMinutes(index),
                Severity: "Info",
                Destination: "In-App",
                Summary: $"Alert {index}"));
        }

        for (var index = 0; index < LocalStateStore.AuditRetentionCount + 10; index++)
        {
            store.AppendAuditEvent(new PersistedAuditEvent(
                EventId: $"audit-{index}",
                TimestampUtc: DateTimeOffset.UtcNow.AddMinutes(index),
                EventType: "TestEvent",
                Origin: "Test",
                Summary: $"Audit {index}",
                CorrelationId: null,
                Detail: new Dictionary<string, string?>()));
        }

        var alerts = store.LoadRecentAlerts();
        var audit = store.LoadAuditEvents();

        Assert.Equal(LocalStateStore.AlertRetentionCount, alerts.Count);
        Assert.Equal($"Alert {LocalStateStore.AlertRetentionCount + 9}", alerts[^1].Summary);
        Assert.Equal(LocalStateStore.AuditRetentionCount, audit.Count);
        Assert.Equal($"Audit {LocalStateStore.AuditRetentionCount + 9}", audit[^1].Summary);
    }

    [Fact]
    public void LoadRecentAlerts_SkipsMalformedJsonLinesInsteadOfDiscardingWholeHistory()
    {
        var paths = CreateTestPaths();
        Directory.CreateDirectory(Path.GetDirectoryName(paths.AlertHistoryPath)!);
        File.WriteAllLines(paths.AlertHistoryPath,
        [
            JsonSerializer.Serialize(new PersistedAlertEvent("alert-1", DateTimeOffset.UtcNow, "Info", "In-App", "First alert")),
            "{ malformed",
            JsonSerializer.Serialize(new PersistedAlertEvent("alert-2", DateTimeOffset.UtcNow.AddMinutes(1), "Error", "In-App", "Second alert")),
        ]);

        var store = new LocalStateStore(paths);
        var alerts = store.LoadRecentAlerts();

        Assert.Equal(2, alerts.Count);
        Assert.Equal("First alert", alerts[0].Summary);
        Assert.Equal("Second alert", alerts[1].Summary);
    }

    private static CoinbaseShellSnapshot CreateConnectedSnapshot()
    {
        return CoinbaseShellSnapshot.Connected(
            new CoinbaseKeyPermissions(true, false, false, "portfolio-1", "consumer"),
            [
                new CoinbaseAccount("acc-btc", "BTC Wallet", "BTC", "0.15", "BTC", "0.00", "BTC", "ACCOUNT_TYPE_UNSPECIFIED", true, true),
                new CoinbaseAccount("acc-usd", "USD Wallet", "USD", "125.50", "USD", "0.00", "USD", "ACCOUNT_TYPE_UNSPECIFIED", true, true),
            ],
            [
                new CoinbaseProduct("BTC-USD", "BTC", "USD", "online", false, "SPOT", "80000"),
            ],
            [],
            [
                new CoinbaseTransaction("txn-1", "wallet-1", "buy", "completed", "2026-04-19T10:00:00Z", "2026-04-19T10:00:00Z", "50.00", "USD", "50.00", "USD", "Buy", "BTC buy"),
            ],
            ["Loaded account state."]);
    }

    private static VectorQuayPaths CreateTestPaths()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vectorquay-state-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempRoot, "config", "templates"));
        Directory.CreateDirectory(Path.Combine(tempRoot, ".config", "VectorQuay"));

        var templatePath = Path.Combine(tempRoot, "config", "templates", "appsettings.template.json");
        File.WriteAllText(templatePath, JsonSerializer.Serialize(AppSettings.CreateDefault()));

        return VectorQuayPaths.Resolve(
            homeDirectory: tempRoot,
            xdgConfigHome: Path.Combine(tempRoot, ".config"),
            baseDirectory: tempRoot);
    }
}
