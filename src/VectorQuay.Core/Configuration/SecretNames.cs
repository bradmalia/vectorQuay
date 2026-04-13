namespace VectorQuay.Core.Configuration;

public static class SecretNames
{
    public const string CoinbaseApiKey = "VECTORQUAY_COINBASE_API_KEY";
    public const string CoinbaseApiSecret = "VECTORQUAY_COINBASE_API_SECRET";
    public const string OpenAiApiKey = "VECTORQUAY_OPENAI_API_KEY";

    public static IReadOnlyList<string> All { get; } =
    [
        CoinbaseApiKey,
        CoinbaseApiSecret,
        OpenAiApiKey,
    ];
}
