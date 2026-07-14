using VectorQuay.Core.Configuration;
using VectorQuay.Core.Primitives;

namespace VectorQuay.Core.Coinbase;

public interface ICoinbaseReadOnlyService
{
    Task<CoinbaseShellSnapshot> RefreshAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductPricePoint>> GetProductPriceHistoryAsync(string productId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);
}

public sealed class CoinbaseShellDataService : ICoinbaseReadOnlyService
{
    private readonly CoinbaseCredentialsResolver _credentialsResolver;
    private readonly IAuthenticatedCoinbaseReadOnlyClient _readOnlyClient;

    public CoinbaseShellDataService(
        VectorQuayPaths paths,
        IAuthenticatedCoinbaseReadOnlyClient readOnlyClient)
    {
        _credentialsResolver = new CoinbaseCredentialsResolver(paths);
        _readOnlyClient = readOnlyClient ?? throw new ArgumentNullException(nameof(readOnlyClient));
    }

    public async Task<CoinbaseShellSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var credentialResolution = _credentialsResolver.Resolve();
        if (credentialResolution.State != CoinbaseCredentialState.Present || credentialResolution.Credentials is null)
        {
            return CoinbaseShellSnapshot.NotReady(credentialResolution.State, credentialResolution.Messages);
        }

        var credentials = credentialResolution.Credentials;
        IReadOnlyList<CoinbaseAccount> accounts;
        IReadOnlyList<CoinbaseProduct> products;
        IReadOnlyList<CoinbaseFill> fills;
        IReadOnlyList<CoinbaseWalletAccount> walletAccounts;
        IReadOnlyList<CoinbaseTransaction> transactions;
        CoinbaseKeyPermissions permissions;
        List<string> errors = new();

        // 1. Key permissions (fatal if missing)
        try
        {
            permissions = await _readOnlyClient.GetKeyPermissionsAsync(credentials, cancellationToken);
        }
        catch (Exception ex)
        {
            return CoinbaseShellSnapshot.Failed(CoinbaseRefreshState.TransportFailure, [ex.Message]);
        }

        // 2. Accounts
        try
        {
            accounts = await _readOnlyClient.ListAccountsAsync(credentials, cancellationToken);
        }
        catch (Exception ex)
        {
            accounts = [];
            errors.Add($"Failed to list accounts: {ex.Message}");
        }

        // 3. Products
        try
        {
            products = await _readOnlyClient.ListProductsAsync(credentials, cancellationToken);
        }
        catch (Exception ex)
        {
            products = [];
            errors.Add($"Failed to list products: {ex.Message}");
        }

        // 4. Fills (best-effort)
        try
        {
            var fillsResult = await TryListFillsAsync(credentials, cancellationToken);
            fills = fillsResult.IsSuccess ? fillsResult.Value! : [];
            if (!fillsResult.IsSuccess) errors.AddRange(fillsResult.Errors);
        }
        catch (Exception ex)
        {
            fills = [];
            errors.Add($"Failed to list fills: {ex.Message}");
        }

        // 5. Wallet accounts (best-effort)
        try
        {
            var waResult = await TryListWalletAccountsAsync(credentials, cancellationToken);
            walletAccounts = waResult.IsSuccess ? waResult.Value! : [];
            if (!waResult.IsSuccess) errors.AddRange(waResult.Errors);
        }
        catch (Exception ex)
        {
            walletAccounts = [];
            errors.Add($"Failed to list wallet accounts: {ex.Message}");
        }

        // 6. Transactions (best-effort, depends on wallet accounts)
        try
        {
            var txResult = await TryListTransactionsAsync(credentials, walletAccounts, cancellationToken);
            transactions = txResult.IsSuccess ? txResult.Value! : [];
            if (!txResult.IsSuccess) errors.AddRange(txResult.Errors);
        }
        catch (Exception ex)
        {
            transactions = [];
            errors.Add($"Failed to list transactions: {ex.Message}");
        }

        var messages = new List<string>(credentialResolution.Messages);
        messages.AddRange(errors);

        if (!permissions.CanView)
        {
            messages.Add("Coinbase key permissions do not allow account viewing.");
            return CoinbaseShellSnapshot.Failed(CoinbaseRefreshState.PermissionMismatch, messages);
        }

        if (permissions.CanTrade || permissions.CanTransfer)
        {
            messages.Add("Warning: Coinbase key exposes capabilities beyond the approved read-only Phase 2 posture.");
        }

        messages.Add($"Loaded {accounts.Count} accounts, {products.Count} products, {fills.Count} recent fills, and {transactions.Count} wallet transactions from Coinbase.");
        return CoinbaseShellSnapshot.Connected(permissions, accounts, products, fills, transactions, messages);
    }

    public async Task<IReadOnlyList<ProductPricePoint>> GetProductPriceHistoryAsync(string productId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
    {
        return await _readOnlyClient.GetProductPriceHistoryAsync(productId, start, end, cancellationToken);
    }

    private async Task<ServiceResult<IReadOnlyList<CoinbaseFill>>> TryListFillsAsync(
        CoinbaseCredentials credentials, CancellationToken cancellationToken)
    {
        try
        {
            var fills = await _readOnlyClient.ListFillsAsync(credentials, cancellationToken);
            return ServiceResult<IReadOnlyList<CoinbaseFill>>.Success(fills);
        }
        catch (Exception ex)
        {
            return ServiceResult<IReadOnlyList<CoinbaseFill>>.Failure([ex.Message]);
        }
    }

    private async Task<ServiceResult<IReadOnlyList<CoinbaseWalletAccount>>> TryListWalletAccountsAsync(
        CoinbaseCredentials credentials, CancellationToken cancellationToken)
    {
        try
        {
            var accounts = await _readOnlyClient.ListWalletAccountsAsync(credentials, cancellationToken);
            return ServiceResult<IReadOnlyList<CoinbaseWalletAccount>>.Success(accounts);
        }
        catch (Exception ex)
        {
            return ServiceResult<IReadOnlyList<CoinbaseWalletAccount>>.Failure([ex.Message]);
        }
    }

    private async Task<ServiceResult<IReadOnlyList<CoinbaseTransaction>>> TryListTransactionsAsync(
        CoinbaseCredentials credentials,
        IReadOnlyList<CoinbaseWalletAccount> walletAccounts,
        CancellationToken cancellationToken)
    {
        if (walletAccounts is null || walletAccounts.Count == 0)
        {
            return ServiceResult<IReadOnlyList<CoinbaseTransaction>>.Success([]);
        }

        var allTransactions = new List<CoinbaseTransaction>();
        foreach (var account in walletAccounts.Take(10)) // limit to avoid excessive calls
        {
            try
            {
                var txs = await _readOnlyClient.ListTransactionsAsync(credentials, account.Id ?? string.Empty, cancellationToken);
                if (txs is not null) allTransactions.AddRange(txs);
            }
            catch
            {
                // Silently skip individual account failures; aggregate at outer level
            }
        }

        return ServiceResult<IReadOnlyList<CoinbaseTransaction>>.Success(allTransactions);
    }
}

public enum CoinbaseRefreshState
{
    MissingCredentials,
    IncompleteCredentials,
    Connected,
    PermissionMismatch,
    TransportFailure,
    RefreshFailed,
}

public sealed record CoinbaseShellSnapshot(
    CoinbaseRefreshState State,
    DateTimeOffset? LastRefreshUtc,
    CoinbaseKeyPermissions? Permissions,
    IReadOnlyList<CoinbaseAccount> Accounts,
    IReadOnlyList<CoinbaseProduct> Products,
    IReadOnlyList<CoinbaseFill> Fills,
    IReadOnlyList<CoinbaseTransaction> Transactions,
    IReadOnlyList<string> Messages)
{
    public bool IsConnected => State == CoinbaseRefreshState.Connected;

    public static CoinbaseShellSnapshot NotReady(CoinbaseCredentialState state, IReadOnlyList<string> messages)
    {
        return new CoinbaseShellSnapshot(
            state == CoinbaseCredentialState.Incomplete ? CoinbaseRefreshState.IncompleteCredentials : CoinbaseRefreshState.MissingCredentials,
            null,
            null,
            [],
            [],
            [],
            [],
            messages);
    }

    public static CoinbaseShellSnapshot Connected(
        CoinbaseKeyPermissions permissions,
        IReadOnlyList<CoinbaseAccount> accounts,
        IReadOnlyList<CoinbaseProduct> products,
        IReadOnlyList<CoinbaseFill> fills,
        IReadOnlyList<CoinbaseTransaction> transactions,
        IReadOnlyList<string> messages)
    {
        return new CoinbaseShellSnapshot(CoinbaseRefreshState.Connected, DateTimeOffset.UtcNow, permissions, accounts, products, fills, transactions, messages);
    }

    public static CoinbaseShellSnapshot Failed(CoinbaseRefreshState state, IReadOnlyList<string> messages)
    {
        return new CoinbaseShellSnapshot(state, null, null, [], [], [], [], messages);
    }
}
