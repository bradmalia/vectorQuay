using VectorQuay.Core.Configuration;

namespace VectorQuay.Core.Coinbase;

public sealed record CoinbaseCredentials(string ApiKey, string ApiSecret);

public enum CoinbaseCredentialState
{
    Missing,
    Incomplete,
    Present,
}

public sealed record CoinbaseCredentialResolution(
    CoinbaseCredentialState State,
    CoinbaseCredentials? Credentials,
    IReadOnlyList<string> Messages);

public sealed class CoinbaseCredentialsResolver
{
    private readonly VectorQuayPaths _paths;

    public CoinbaseCredentialsResolver(VectorQuayPaths paths)
    {
        _paths = paths;
    }

    public CoinbaseCredentialResolution Resolve()
    {
        var messages = new List<string>();
        var jsonCredentials = LoadCoinbaseJsonCredentials(messages);
        if (jsonCredentials is not null)
        {
            messages.Add("Coinbase credentials loaded from the preferred CDP JSON key file.");
            return new CoinbaseCredentialResolution(CoinbaseCredentialState.Present, jsonCredentials, messages);
        }

        var secrets = LoadSecrets(messages);

        var apiKey = ResolveSecretValue(SecretNames.CoinbaseApiKey, secrets);
        var apiSecret = ResolveSecretValue(SecretNames.CoinbaseApiSecret, secrets);

        if (string.IsNullOrWhiteSpace(apiKey) && string.IsNullOrWhiteSpace(apiSecret))
        {
            messages.Add("Coinbase credentials are missing.");
            return new CoinbaseCredentialResolution(CoinbaseCredentialState.Missing, null, messages);
        }

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
        {
            messages.Add("Coinbase credentials are incomplete. Both API key and API secret are required.");
            return new CoinbaseCredentialResolution(CoinbaseCredentialState.Incomplete, null, messages);
        }

        messages.Add("Coinbase credentials are present through the approved secret contract.");
        return new CoinbaseCredentialResolution(
            CoinbaseCredentialState.Present,
            new CoinbaseCredentials(apiKey, apiSecret),
            messages);
    }

    private Dictionary<string, string> LoadSecrets(List<string> messages)
    {
        if (!File.Exists(_paths.SecretsPath))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            var parsed = SecretFileParser.ParseWithDiagnostics(File.ReadAllText(_paths.SecretsPath));
            if (parsed.InvalidLines.Count > 0)
            {
                messages.Add($"Coinbase credential resolver ignored {parsed.InvalidLines.Count} malformed secrets.env line(s).");
            }

            return parsed.Values;
        }
        catch (Exception ex)
        {
            messages.Add($"Coinbase credential resolver could not read secrets.env safely ({ex.GetType().Name}).");
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private CoinbaseCredentials? LoadCoinbaseJsonCredentials(List<string> messages)
    {
        var candidates = new[] { _paths.CoinbaseApiKeyJsonPath, _paths.CoinbaseApiKeyJsonTextPath };
        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                return CoinbaseApiKeyFileParser.Parse(File.ReadAllText(candidate));
            }
            catch (Exception ex)
            {
                messages.Add($"Coinbase JSON key file at {candidate} could not be parsed safely ({ex.GetType().Name}).");
            }
        }

        return null;
    }

    private static string ResolveSecretValue(string name, IReadOnlyDictionary<string, string> secrets)
    {
        var environmentValue = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return environmentValue.Trim();
        }

        return secrets.TryGetValue(name, out var fileValue)
            ? fileValue.Trim()
            : string.Empty;
    }
}
