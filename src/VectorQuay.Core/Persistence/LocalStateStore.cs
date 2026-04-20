using System.Globalization;
using System.Text.Json;
using VectorQuay.Core.Coinbase;
using VectorQuay.Core.Configuration;

namespace VectorQuay.Core.Persistence;

public enum RecoveryTruthState
{
    NoRecoverableState,
    RestoredFromCache,
    CorruptedState,
}

public sealed record PersistedHoldingSnapshot(
    string Asset,
    decimal Quantity,
    decimal EstimatedValueUsd);

public sealed record PersistedShellSnapshot(
    DateTimeOffset CapturedAtUtc,
    CoinbaseRefreshState RefreshState,
    DateTimeOffset? LastRefreshUtc,
    string ConnectionSummary,
    decimal TotalPortfolioValueUsd,
    decimal AvailableUsd,
    decimal AvailableUsdc,
    IReadOnlyList<PersistedHoldingSnapshot> Holdings,
    IReadOnlyList<string> Messages);

public sealed record PersistedActivityEvent(
    string EventId,
    DateTimeOffset TimestampUtc,
    string EventType,
    string AssetOrSubject,
    string Status,
    string Summary,
    string Detail,
    string Origin);

public sealed record PersistedAlertEvent(
    string EventId,
    DateTimeOffset TimestampUtc,
    string Severity,
    string Destination,
    string Summary);

public sealed record PersistedPortfolioValuePoint(
    DateTimeOffset TimestampUtc,
    decimal TotalValueUsd);

public sealed record PersistedAuditEvent(
    string EventId,
    DateTimeOffset TimestampUtc,
    string EventType,
    string Origin,
    string Summary,
    string? CorrelationId,
    IReadOnlyDictionary<string, string?> Detail);

public sealed record PersistedSnapshotLoadResult(
    RecoveryTruthState State,
    PersistedShellSnapshot? Snapshot,
    string Message);

public interface ILocalStateStore
{
    void SaveCoinbaseSnapshot(CoinbaseShellSnapshot snapshot);
    PersistedSnapshotLoadResult LoadSnapshot();
    IReadOnlyList<PersistedActivityEvent> LoadRecentActivity();
    IReadOnlyList<PersistedAlertEvent> LoadRecentAlerts();
    IReadOnlyList<PersistedPortfolioValuePoint> LoadPortfolioValueHistory();
    IReadOnlyList<PersistedAuditEvent> LoadAuditEvents();
    void AppendAlert(PersistedAlertEvent alertEvent);
    void AppendAuditEvent(PersistedAuditEvent auditEvent);
}

public sealed class LocalStateStore : ILocalStateStore
{
    public const int ActivityRetentionCount = 250;
    public const int AlertRetentionCount = 250;
    public const int PortfolioValueRetentionCount = 1000;
    public const int AuditRetentionCount = 2000;
    public const string CurrentSchemaVersion = "phase3-v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private static readonly JsonSerializerOptions JsonLineOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private readonly VectorQuayPaths _paths;

    public LocalStateStore(VectorQuayPaths paths)
    {
        _paths = paths;
    }

    public void SaveCoinbaseSnapshot(CoinbaseShellSnapshot snapshot)
    {
        EnsureStorageLayout();

        var persisted = CreatePersistedSnapshot(snapshot);
        WriteJsonAtomic(_paths.LastSnapshotPath, persisted);
        File.WriteAllText(_paths.DataSchemaVersionPath, CurrentSchemaVersion);

        if (snapshot.Transactions.Count > 0)
        {
            var activity = snapshot.Transactions
                .Select(CreateActivityEvent)
                .ToArray();
            ReplaceJsonLines(_paths.ActivityHistoryPath, MergeById(LoadJsonLines<PersistedActivityEvent>(_paths.ActivityHistoryPath), activity, ActivityRetentionCount));
        }

        if (snapshot.IsConnected)
        {
            var history = LoadJsonLines<PersistedPortfolioValuePoint>(_paths.PortfolioValueHistoryPath)
                .Append(new PersistedPortfolioValuePoint(DateTimeOffset.UtcNow, persisted.TotalPortfolioValueUsd))
                .OrderBy(point => point.TimestampUtc)
                .TakeLast(PortfolioValueRetentionCount)
                .ToArray();
            ReplaceJsonLines(_paths.PortfolioValueHistoryPath, history);
        }

        AppendAuditEvent(new PersistedAuditEvent(
            EventId: $"refresh-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}",
            TimestampUtc: DateTimeOffset.UtcNow,
            EventType: "CoinbaseRefresh",
            Origin: "System",
            Summary: snapshot.IsConnected
                ? $"Coinbase refresh succeeded with {snapshot.Accounts.Count} accounts and {snapshot.Transactions.Count} transactions."
                : $"Coinbase refresh ended in state {snapshot.State}.",
            CorrelationId: snapshot.LastRefreshUtc?.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            Detail: RedactDetail(new Dictionary<string, string?>
            {
                ["refreshState"] = snapshot.State.ToString(),
                ["message"] = snapshot.Messages.FirstOrDefault(),
            })));
    }

    public PersistedSnapshotLoadResult LoadSnapshot()
    {
        if (!File.Exists(_paths.LastSnapshotPath))
        {
            return new PersistedSnapshotLoadResult(RecoveryTruthState.NoRecoverableState, null, "No persisted snapshot is available.");
        }

        if (File.Exists(_paths.DataSchemaVersionPath))
        {
            try
            {
                var schemaVersion = File.ReadAllText(_paths.DataSchemaVersionPath).Trim();
                if (!string.IsNullOrWhiteSpace(schemaVersion) &&
                    !string.Equals(schemaVersion, CurrentSchemaVersion, StringComparison.Ordinal))
                {
                    return new PersistedSnapshotLoadResult(RecoveryTruthState.CorruptedState, null, $"Persisted snapshot schema version '{schemaVersion}' is incompatible with '{CurrentSchemaVersion}'.");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new PersistedSnapshotLoadResult(RecoveryTruthState.CorruptedState, null, $"Persisted snapshot schema version could not be read safely ({ex.GetType().Name}).");
            }
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<PersistedShellSnapshot>(File.ReadAllText(_paths.LastSnapshotPath), JsonOptions);
            return snapshot is null
                ? new PersistedSnapshotLoadResult(RecoveryTruthState.CorruptedState, null, "Persisted snapshot could not be deserialized.")
                : new PersistedSnapshotLoadResult(RecoveryTruthState.RestoredFromCache, snapshot, "Persisted snapshot restored from local cache.");
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new PersistedSnapshotLoadResult(RecoveryTruthState.CorruptedState, null, $"Persisted snapshot could not be loaded safely ({ex.GetType().Name}).");
        }
    }

    public IReadOnlyList<PersistedActivityEvent> LoadRecentActivity() => LoadJsonLines<PersistedActivityEvent>(_paths.ActivityHistoryPath);

    public IReadOnlyList<PersistedAlertEvent> LoadRecentAlerts() => LoadJsonLines<PersistedAlertEvent>(_paths.AlertHistoryPath);

    public IReadOnlyList<PersistedPortfolioValuePoint> LoadPortfolioValueHistory() => LoadJsonLines<PersistedPortfolioValuePoint>(_paths.PortfolioValueHistoryPath);

    public IReadOnlyList<PersistedAuditEvent> LoadAuditEvents() => LoadJsonLines<PersistedAuditEvent>(_paths.AuditEventsPath);

    public void AppendAlert(PersistedAlertEvent alertEvent)
    {
        EnsureStorageLayout();
        var alerts = MergeById(LoadJsonLines<PersistedAlertEvent>(_paths.AlertHistoryPath), [alertEvent], AlertRetentionCount);
        ReplaceJsonLines(_paths.AlertHistoryPath, alerts);
    }

    public void AppendAuditEvent(PersistedAuditEvent auditEvent)
    {
        EnsureStorageLayout();
        var sanitized = auditEvent with { Detail = RedactDetail(auditEvent.Detail) };
        var events = MergeById(LoadJsonLines<PersistedAuditEvent>(_paths.AuditEventsPath), [sanitized], AuditRetentionCount);
        ReplaceJsonLines(_paths.AuditEventsPath, events);
    }

    private void EnsureStorageLayout()
    {
        Directory.CreateDirectory(_paths.StateDirectory);
        Directory.CreateDirectory(_paths.HistoryDirectory);
        Directory.CreateDirectory(_paths.AuditDirectory);
    }

    private static PersistedShellSnapshot CreatePersistedSnapshot(CoinbaseShellSnapshot snapshot)
    {
        var holdings = snapshot.Accounts
            .Select(account =>
            {
                var quantity = ParseDecimal(account.AvailableValue) + ParseDecimal(account.HoldValue);
                var estimatedValueUsd = EstimateUsdValue(account.Currency, quantity, snapshot.Products);
                return new PersistedHoldingSnapshot(account.Currency, quantity, estimatedValueUsd);
            })
            .Where(holding => holding.Quantity > 0m)
            .OrderByDescending(holding => holding.EstimatedValueUsd)
            .ToArray();

        return new PersistedShellSnapshot(
            CapturedAtUtc: DateTimeOffset.UtcNow,
            RefreshState: snapshot.State,
            LastRefreshUtc: snapshot.LastRefreshUtc,
            ConnectionSummary: snapshot.State.ToString(),
            TotalPortfolioValueUsd: holdings.Sum(holding => holding.EstimatedValueUsd),
            AvailableUsd: snapshot.Accounts
                .Where(account => string.Equals(account.Currency, "USD", StringComparison.OrdinalIgnoreCase))
                .Sum(account => ParseDecimal(account.AvailableValue)),
            AvailableUsdc: snapshot.Accounts
                .Where(account => string.Equals(account.Currency, "USDC", StringComparison.OrdinalIgnoreCase))
                .Sum(account => ParseDecimal(account.AvailableValue)),
            Holdings: holdings,
            Messages: snapshot.Messages.ToArray());
    }

    private static PersistedActivityEvent CreateActivityEvent(CoinbaseTransaction transaction)
    {
        var timestamp = DateTimeOffset.TryParse(transaction.CreatedAt, out var parsed)
            ? parsed
            : DateTimeOffset.UtcNow;
        var asset = !string.IsNullOrWhiteSpace(transaction.AmountCurrency)
            ? transaction.AmountCurrency
            : transaction.NativeCurrency;
        var summary = !string.IsNullOrWhiteSpace(transaction.Title)
            ? transaction.Title
            : transaction.Type;
        var detail = string.IsNullOrWhiteSpace(transaction.Subtitle) ? summary : transaction.Subtitle;

        return new PersistedActivityEvent(
            EventId: transaction.Id,
            TimestampUtc: timestamp,
            EventType: transaction.Type,
            AssetOrSubject: asset,
            Status: transaction.Status,
            Summary: summary,
            Detail: detail,
            Origin: "Coinbase");
    }

    private static decimal EstimateUsdValue(string asset, decimal quantity, IReadOnlyList<CoinbaseProduct> products)
    {
        if (quantity <= 0m)
        {
            return 0m;
        }

        if (string.Equals(asset, "USD", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(asset, "USDC", StringComparison.OrdinalIgnoreCase))
        {
            return quantity;
        }

        var directUsd = products.FirstOrDefault(product =>
            string.Equals(product.BaseCurrencyId, asset, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(product.QuoteCurrencyId, "USD", StringComparison.OrdinalIgnoreCase));
        if (directUsd is not null)
        {
            return quantity * ParseDecimal(directUsd.Price);
        }

        var directUsdc = products.FirstOrDefault(product =>
            string.Equals(product.BaseCurrencyId, asset, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(product.QuoteCurrencyId, "USDC", StringComparison.OrdinalIgnoreCase));
        if (directUsdc is not null)
        {
            return quantity * ParseDecimal(directUsdc.Price);
        }

        return 0m;
    }

    private static decimal ParseDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0m;
    }

    private static IReadOnlyList<T> LoadJsonLines<T>(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var items = new List<T>();
            foreach (var line in File.ReadLines(path).Where(line => !string.IsNullOrWhiteSpace(line)))
            {
                try
                {
                    var item = JsonSerializer.Deserialize<T>(line, JsonLineOptions);
                    if (item is not null)
                    {
                        items.Add(item);
                    }
                }
                catch (JsonException)
                {
                    continue;
                }
            }

            return items;
        }
        catch (Exception) when (typeof(T) != typeof(string))
        {
            return [];
        }
    }

    private static IReadOnlyList<T> MergeById<T>(IReadOnlyList<T> existing, IReadOnlyList<T> incoming, int limit) where T : class
    {
        var all = existing.Concat(incoming).ToList();
        var idProperty = typeof(T).GetProperty("EventId");
        if (idProperty is null)
        {
            return all.TakeLast(limit).ToArray();
        }

        return all
            .GroupBy(item => idProperty.GetValue(item)?.ToString() ?? Guid.NewGuid().ToString("N"), StringComparer.Ordinal)
            .Select(group => group.OrderBy(item => ExtractTimestamp(item)).Last())
            .OrderBy(item => ExtractTimestamp(item))
            .TakeLast(limit)
            .ToArray();
    }

    private static DateTimeOffset ExtractTimestamp<T>(T item)
    {
        var property = typeof(T).GetProperty("TimestampUtc");
        return property?.GetValue(item) is DateTimeOffset value ? value : DateTimeOffset.UtcNow;
    }

    private static IReadOnlyDictionary<string, string?> RedactDetail(IReadOnlyDictionary<string, string?> detail)
    {
        var sanitized = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in detail)
        {
            if (IsSensitiveKey(pair.Key) || LooksSensitiveValue(pair.Value))
            {
                sanitized[pair.Key] = "[REDACTED]";
            }
            else
            {
                sanitized[pair.Key] = pair.Value;
            }
        }

        return sanitized;
    }

    private static bool IsSensitiveKey(string key)
    {
        var normalized = key.Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return normalized.Contains("secret", StringComparison.Ordinal) ||
               normalized.Contains("token", StringComparison.Ordinal) ||
               normalized.Contains("apikey", StringComparison.Ordinal) ||
               normalized.Contains("authorization", StringComparison.Ordinal) ||
               normalized.Contains("bearer", StringComparison.Ordinal);
    }

    private static bool LooksSensitiveValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.StartsWith("sk-", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("BEGIN", StringComparison.OrdinalIgnoreCase);
    }

    private static void ReplaceJsonLines<T>(string path, IReadOnlyList<T> items)
    {
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(tempPath, items.Select(item => JsonSerializer.Serialize(item, JsonLineOptions)));
        File.Move(tempPath, path, true);
    }

    private static void WriteJsonAtomic<T>(string path, T payload)
    {
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(tempPath, JsonSerializer.Serialize(payload, JsonOptions));
        File.Move(tempPath, path, true);
    }
}
