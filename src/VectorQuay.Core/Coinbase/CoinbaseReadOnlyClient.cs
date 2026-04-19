using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VectorQuay.Core.Coinbase;

public interface IAuthenticatedCoinbaseReadOnlyClient
{
    Task<CoinbaseKeyPermissions> GetKeyPermissionsAsync(CoinbaseCredentials credentials, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CoinbaseAccount>> ListAccountsAsync(CoinbaseCredentials credentials, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CoinbaseProduct>> ListProductsAsync(CoinbaseCredentials credentials, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CoinbaseFill>> ListFillsAsync(CoinbaseCredentials credentials, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CoinbaseWalletAccount>> ListWalletAccountsAsync(CoinbaseCredentials credentials, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CoinbaseTransaction>> ListTransactionsAsync(CoinbaseCredentials credentials, string walletAccountId, CancellationToken cancellationToken = default);
}

public sealed class CoinbaseReadOnlyClient : IAuthenticatedCoinbaseReadOnlyClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly CoinbaseJwtFactory _jwtFactory;

    public CoinbaseReadOnlyClient(HttpClient httpClient, CoinbaseJwtFactory? jwtFactory = null)
    {
        _httpClient = httpClient;
        _jwtFactory = jwtFactory ?? new CoinbaseJwtFactory();

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri("https://api.coinbase.com/");
        }
    }

    public async Task<CoinbaseKeyPermissions> GetKeyPermissionsAsync(CoinbaseCredentials credentials, CancellationToken cancellationToken = default)
    {
        return await SendAsync<CoinbaseKeyPermissionsResponse, CoinbaseKeyPermissions>(
            credentials,
            HttpMethod.Get,
            "/api/v3/brokerage/key_permissions",
            response => new CoinbaseKeyPermissions(
                response.CanView,
                response.CanTrade,
                response.CanTransfer,
                response.PortfolioUuid ?? string.Empty,
                response.PortfolioType ?? string.Empty),
            cancellationToken);
    }

    public async Task<IReadOnlyList<CoinbaseAccount>> ListAccountsAsync(CoinbaseCredentials credentials, CancellationToken cancellationToken = default)
    {
        return await SendAsync<CoinbaseAccountsResponse, IReadOnlyList<CoinbaseAccount>>(
            credentials,
            HttpMethod.Get,
            "/api/v3/brokerage/accounts",
            response => response.Accounts.Select(MapAccount).ToArray(),
            cancellationToken);
    }

    public async Task<IReadOnlyList<CoinbaseProduct>> ListProductsAsync(CoinbaseCredentials credentials, CancellationToken cancellationToken = default)
    {
        return await SendAsync<CoinbaseProductsResponse, IReadOnlyList<CoinbaseProduct>>(
            credentials,
            HttpMethod.Get,
            "/api/v3/brokerage/products",
            response => response.Products.Select(MapProduct).ToArray(),
            cancellationToken);
    }

    public async Task<IReadOnlyList<CoinbaseFill>> ListFillsAsync(CoinbaseCredentials credentials, CancellationToken cancellationToken = default)
    {
        return await SendAsync<CoinbaseFillsResponse, IReadOnlyList<CoinbaseFill>>(
            credentials,
            HttpMethod.Get,
            "/api/v3/brokerage/orders/historical/fills?limit=50",
            response => response.Fills.Select(MapFill).ToArray(),
            cancellationToken);
    }

    public async Task<IReadOnlyList<CoinbaseWalletAccount>> ListWalletAccountsAsync(CoinbaseCredentials credentials, CancellationToken cancellationToken = default)
    {
        return await SendAsync<CoinbaseWalletAccountsResponse, IReadOnlyList<CoinbaseWalletAccount>>(
            credentials,
            HttpMethod.Get,
            "/v2/accounts",
            response => response.Data.Select(MapWalletAccount).ToArray(),
            cancellationToken);
    }

    public async Task<IReadOnlyList<CoinbaseTransaction>> ListTransactionsAsync(CoinbaseCredentials credentials, string walletAccountId, CancellationToken cancellationToken = default)
    {
        return await SendAsync<CoinbaseTransactionsResponse, IReadOnlyList<CoinbaseTransaction>>(
            credentials,
            HttpMethod.Get,
            $"/v2/accounts/{walletAccountId}/transactions",
            response => response.Data.Select(item => MapTransaction(walletAccountId, item)).ToArray(),
            cancellationToken);
    }

    private async Task<T> SendAsync<TResponse, T>(
        CoinbaseCredentials credentials,
        HttpMethod method,
        string requestPath,
        Func<TResponse, T> map,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, requestPath);
        var jwt = _jwtFactory.CreateBearerToken(credentials, method, requestPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<TResponse>(stream, JsonOptions, cancellationToken);
        if (payload is null)
        {
            throw new InvalidOperationException($"Coinbase returned an empty payload for {requestPath}.");
        }

        return map(payload);
    }

    private static CoinbaseAccount MapAccount(CoinbaseAccountItem item)
    {
        return new CoinbaseAccount(
            item.Uuid ?? string.Empty,
            item.Name ?? string.Empty,
            item.Currency ?? string.Empty,
            item.AvailableBalance?.Value ?? "0",
            item.AvailableBalance?.Currency ?? item.Currency ?? string.Empty,
            item.Hold?.Value ?? "0",
            item.Hold?.Currency ?? item.Currency ?? string.Empty,
            item.Type ?? string.Empty,
            item.Active,
            item.Ready);
    }

    private static CoinbaseProduct MapProduct(CoinbaseProductItem item)
    {
        return new CoinbaseProduct(
            item.ProductId ?? string.Empty,
            item.BaseCurrencyId ?? string.Empty,
            item.QuoteCurrencyId ?? string.Empty,
            item.Status ?? string.Empty,
            item.TradingDisabled,
            item.ProductType ?? string.Empty,
            item.Price ?? "0");
    }

    private static CoinbaseFill MapFill(CoinbaseFillItem item)
    {
        return new CoinbaseFill(
            item.EntryId ?? string.Empty,
            item.TradeId ?? string.Empty,
            item.OrderId ?? string.Empty,
            item.TradeTime ?? string.Empty,
            item.TradeType ?? string.Empty,
            item.Side ?? string.Empty,
            item.ProductId ?? string.Empty,
            item.Price ?? "0",
            item.Size ?? "0",
            item.Commission ?? "0",
            item.LiquidityIndicator ?? string.Empty,
            item.SizeInQuote,
            item.FillSource ?? string.Empty);
    }

    private static CoinbaseWalletAccount MapWalletAccount(CoinbaseWalletAccountItem item)
    {
        return new CoinbaseWalletAccount(
            item.Id ?? string.Empty,
            item.Name ?? string.Empty,
            item.Type ?? string.Empty,
            item.Primary,
            item.Currency?.Code ?? string.Empty,
            item.Balance?.Amount ?? "0");
    }

    private static CoinbaseTransaction MapTransaction(string walletAccountId, CoinbaseTransactionItem item)
    {
        return new CoinbaseTransaction(
            item.Id ?? string.Empty,
            walletAccountId,
            item.Type ?? string.Empty,
            item.Status ?? string.Empty,
            item.CreatedAt ?? string.Empty,
            item.UpdatedAt ?? string.Empty,
            item.Amount?.Amount ?? "0",
            item.Amount?.Currency ?? string.Empty,
            item.NativeAmount?.Amount ?? "0",
            item.NativeAmount?.Currency ?? string.Empty,
            item.Details?.Title ?? string.Empty,
            item.Details?.Subtitle ?? string.Empty);
    }
}

public sealed record CoinbaseKeyPermissions(
    bool CanView,
    bool CanTrade,
    bool CanTransfer,
    string PortfolioUuid,
    string PortfolioType);

public sealed record CoinbaseAccount(
    string Uuid,
    string Name,
    string Currency,
    string AvailableValue,
    string AvailableCurrency,
    string HoldValue,
    string HoldCurrency,
    string Type,
    bool Active,
    bool Ready);

public sealed record CoinbaseProduct(
    string ProductId,
    string BaseCurrencyId,
    string QuoteCurrencyId,
    string Status,
    bool TradingDisabled,
    string ProductType,
    string Price);

public sealed record CoinbaseFill(
    string EntryId,
    string TradeId,
    string OrderId,
    string TradeTime,
    string TradeType,
    string Side,
    string ProductId,
    string Price,
    string Size,
    string Commission,
    string LiquidityIndicator,
    bool SizeInQuote,
    string FillSource);

public sealed record CoinbaseWalletAccount(
    string Id,
    string Name,
    string Type,
    bool Primary,
    string CurrencyCode,
    string BalanceAmount);

public sealed record CoinbaseTransaction(
    string Id,
    string WalletAccountId,
    string Type,
    string Status,
    string CreatedAt,
    string UpdatedAt,
    string Amount,
    string AmountCurrency,
    string NativeAmount,
    string NativeCurrency,
    string Title,
    string Subtitle);

internal sealed class CoinbaseKeyPermissionsResponse
{
    [JsonPropertyName("can_view")]
    public bool CanView { get; set; }

    [JsonPropertyName("can_trade")]
    public bool CanTrade { get; set; }

    [JsonPropertyName("can_transfer")]
    public bool CanTransfer { get; set; }

    [JsonPropertyName("portfolio_uuid")]
    public string? PortfolioUuid { get; set; }

    [JsonPropertyName("portfolio_type")]
    public string? PortfolioType { get; set; }
}

internal sealed class CoinbaseAccountsResponse
{
    public CoinbaseAccountItem[] Accounts { get; set; } = [];
}

internal sealed class CoinbaseAccountItem
{
    public string? Uuid { get; set; }
    public string? Name { get; set; }
    public string? Currency { get; set; }

    [JsonPropertyName("available_balance")]
    public CoinbaseMoney? AvailableBalance { get; set; }

    public CoinbaseMoney? Hold { get; set; }
    public string? Type { get; set; }
    public bool Active { get; set; }
    public bool Ready { get; set; }
}

internal sealed class CoinbaseProductsResponse
{
    public CoinbaseProductItem[] Products { get; set; } = [];
}

internal sealed class CoinbaseFillsResponse
{
    public CoinbaseFillItem[] Fills { get; set; } = [];
}

internal sealed class CoinbaseWalletAccountsResponse
{
    public CoinbaseWalletAccountItem[] Data { get; set; } = [];
}

internal sealed class CoinbaseTransactionsResponse
{
    public CoinbaseTransactionItem[] Data { get; set; } = [];
}

internal sealed class CoinbaseProductItem
{
    [JsonPropertyName("product_id")]
    public string? ProductId { get; set; }

    [JsonPropertyName("base_currency_id")]
    public string? BaseCurrencyId { get; set; }

    [JsonPropertyName("quote_currency_id")]
    public string? QuoteCurrencyId { get; set; }

    public string? Status { get; set; }

    [JsonPropertyName("trading_disabled")]
    public bool TradingDisabled { get; set; }

    [JsonPropertyName("product_type")]
    public string? ProductType { get; set; }

    public string? Price { get; set; }
}

internal sealed class CoinbaseFillItem
{
    [JsonPropertyName("entry_id")]
    public string? EntryId { get; set; }

    [JsonPropertyName("trade_id")]
    public string? TradeId { get; set; }

    [JsonPropertyName("order_id")]
    public string? OrderId { get; set; }

    [JsonPropertyName("trade_time")]
    public string? TradeTime { get; set; }

    [JsonPropertyName("trade_type")]
    public string? TradeType { get; set; }

    public string? Side { get; set; }

    [JsonPropertyName("product_id")]
    public string? ProductId { get; set; }

    public string? Price { get; set; }

    public string? Size { get; set; }

    public string? Commission { get; set; }

    [JsonPropertyName("liquidity_indicator")]
    public string? LiquidityIndicator { get; set; }

    [JsonPropertyName("size_in_quote")]
    public bool SizeInQuote { get; set; }

    [JsonPropertyName("fillSource")]
    public string? FillSource { get; set; }
}

internal sealed class CoinbaseMoney
{
    public string? Value { get; set; }
    public string? Currency { get; set; }
}

internal sealed class CoinbaseWalletMoney
{
    public string? Amount { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }
}

internal sealed class CoinbaseWalletCurrency
{
    public string? Code { get; set; }
}

internal sealed class CoinbaseWalletAccountItem
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public bool Primary { get; set; }
    public CoinbaseWalletCurrency? Currency { get; set; }
    public CoinbaseWalletMoney? Balance { get; set; }
}

internal sealed class CoinbaseTransactionItem
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public string? Status { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }

    public CoinbaseWalletMoney? Amount { get; set; }

    [JsonPropertyName("native_amount")]
    public CoinbaseWalletMoney? NativeAmount { get; set; }

    public CoinbaseTransactionDetails? Details { get; set; }
}

internal sealed class CoinbaseTransactionDetails
{
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
}
