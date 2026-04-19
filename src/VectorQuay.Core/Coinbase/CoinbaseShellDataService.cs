using VectorQuay.Core.Configuration;

namespace VectorQuay.Core.Coinbase;

public interface ICoinbaseReadOnlyService
{
    Task<CoinbaseShellSnapshot> RefreshAsync(CancellationToken cancellationToken = default);
}

public sealed class CoinbaseShellDataService : ICoinbaseReadOnlyService
{
    private readonly CoinbaseCredentialsResolver _credentialsResolver;
    private readonly IAuthenticatedCoinbaseReadOnlyClient _client;

    public CoinbaseShellDataService(VectorQuayPaths paths, IAuthenticatedCoinbaseReadOnlyClient? client = null)
    {
        _credentialsResolver = new CoinbaseCredentialsResolver(paths);
        _client = client ?? new CoinbaseReadOnlyClient(new HttpClient());
    }

    public async Task<CoinbaseShellSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var credentialResolution = _credentialsResolver.Resolve();
        if (credentialResolution.State != CoinbaseCredentialState.Present || credentialResolution.Credentials is null)
        {
            return CoinbaseShellSnapshot.NotReady(credentialResolution.State, credentialResolution.Messages);
        }

        try
        {
            var permissions = await _client.GetKeyPermissionsAsync(credentialResolution.Credentials, cancellationToken);
            var accounts = await _client.ListAccountsAsync(credentialResolution.Credentials, cancellationToken);
            var products = await _client.ListProductsAsync(credentialResolution.Credentials, cancellationToken);
            var fills = await TryListFillsAsync(credentialResolution.Credentials, cancellationToken);
            var walletAccounts = await TryListWalletAccountsAsync(credentialResolution.Credentials, cancellationToken);
            var transactions = await TryListTransactionsAsync(credentialResolution.Credentials, walletAccounts, cancellationToken);

            var messages = new List<string>(credentialResolution.Messages);
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
        catch (HttpRequestException ex)
        {
            return CoinbaseShellSnapshot.Failed(CoinbaseRefreshState.TransportFailure, [$"Coinbase request failed: {ex.Message}"]);
        }
        catch (Exception ex)
        {
            return CoinbaseShellSnapshot.Failed(CoinbaseRefreshState.RefreshFailed, [$"Coinbase refresh failed: {ex.Message}"]);
        }
    }

    private async Task<IReadOnlyList<CoinbaseFill>> TryListFillsAsync(CoinbaseCredentials credentials, CancellationToken cancellationToken)
    {
        try
        {
            return await _client.ListFillsAsync(credentials, cancellationToken);
        }
        catch
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<CoinbaseWalletAccount>> TryListWalletAccountsAsync(CoinbaseCredentials credentials, CancellationToken cancellationToken)
    {
        try
        {
            return await _client.ListWalletAccountsAsync(credentials, cancellationToken);
        }
        catch
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<CoinbaseTransaction>> TryListTransactionsAsync(
        CoinbaseCredentials credentials,
        IReadOnlyList<CoinbaseWalletAccount> walletAccounts,
        CancellationToken cancellationToken)
    {
        if (walletAccounts.Count == 0)
        {
            return [];
        }

        var transactions = new List<CoinbaseTransaction>();
        foreach (var walletAccount in walletAccounts.Take(8))
        {
            try
            {
                var accountTransactions = await _client.ListTransactionsAsync(credentials, walletAccount.Id, cancellationToken);
                transactions.AddRange(accountTransactions);
            }
            catch
            {
                // Preserve the read-only shell even if one wallet transaction feed is unavailable.
            }
        }

        return transactions
            .GroupBy(transaction => transaction.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderByDescending(transaction => ParseTimestamp(transaction.CreatedAt))
            .Take(100)
            .ToArray();
    }

    private static DateTimeOffset ParseTimestamp(string? value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;
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
