using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VectorQuay.Core.Coinbase;

public sealed class CoinbaseJwtFactory
{
    private const string RequestHost = "api.coinbase.com";

    public string CreateBearerToken(CoinbaseCredentials credentials, HttpMethod method, string requestPath)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(method);

        if (string.IsNullOrWhiteSpace(requestPath) || requestPath[0] != '/')
        {
            throw new ArgumentException("Request path must be an absolute Coinbase API path beginning with '/'.", nameof(requestPath));
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var uri = $"{method.Method.ToUpperInvariant()} {RequestHost}{requestPath}";

        var header = new Dictionary<string, object?>
        {
            ["alg"] = "ES256",
            ["kid"] = credentials.ApiKey,
            ["nonce"] = CreateNonce(),
            ["typ"] = "JWT",
        };

        var payload = new Dictionary<string, object?>
        {
            ["sub"] = credentials.ApiKey,
            ["iss"] = "cdp",
            ["aud"] = new[] { "cdp_service" },
            ["nbf"] = now,
            ["exp"] = now + 120,
            ["uri"] = uri,
        };

        var encodedHeader = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        var encodedPayload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{encodedHeader}.{encodedPayload}";

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(credentials.ApiSecret);
        var signature = ecdsa.SignData(
            Encoding.UTF8.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"{signingInput}.{Base64UrlEncode(signature)}";
    }

    private static string CreateNonce()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
