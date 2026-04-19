using System.Text.Json;

namespace VectorQuay.Core.Coinbase;

public static class CoinbaseApiKeyFileParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static CoinbaseCredentials Parse(string json)
    {
        var parsed = JsonSerializer.Deserialize<CoinbaseApiKeyFileDocument>(json, JsonOptions)
            ?? throw new InvalidOperationException("Coinbase API key file did not deserialize.");

        if (string.IsNullOrWhiteSpace(parsed.Name))
        {
            throw new InvalidOperationException("Coinbase API key file did not include a valid name.");
        }

        if (string.IsNullOrWhiteSpace(parsed.PrivateKey))
        {
            throw new InvalidOperationException("Coinbase API key file did not include a valid privateKey.");
        }

        return new CoinbaseCredentials(parsed.Name.Trim(), parsed.PrivateKey.Trim());
    }

    private sealed class CoinbaseApiKeyFileDocument
    {
        public string? Name { get; set; }
        public string? PrivateKey { get; set; }
    }
}
