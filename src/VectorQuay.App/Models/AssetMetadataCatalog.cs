using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media.Imaging;

namespace VectorQuay.App.Models;

public static class AssetMetadataCatalog
{
    private const string AssetBasePath = "Assets/Crypto/";

    private static readonly Dictionary<string, AssetMetadata> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BTC"] = new("BTC", "Bitcoin", $"{AssetBasePath}btc.png"),
        ["ETH"] = new("ETH", "Ethereum", $"{AssetBasePath}eth.png"),
        ["ADA"] = new("ADA", "Cardano", $"{AssetBasePath}ada.png"),
        ["BCH"] = new("BCH", "Bitcoin Cash", $"{AssetBasePath}bch.png"),
        ["LINK"] = new("LINK", "Chainlink", $"{AssetBasePath}link.png"),
        ["LTC"] = new("LTC", "Litecoin", $"{AssetBasePath}ltc.png"),
        ["SOL"] = new("SOL", "Solana", $"{AssetBasePath}sol.png"),
        ["UNI"] = new("UNI", "Uniswap", $"{AssetBasePath}uni.png"),
        ["XRP"] = new("XRP", "XRP", $"{AssetBasePath}xrp.png"),
        ["DOGE"] = new("DOGE", "Dogecoin", $"{AssetBasePath}doge.png"),
        ["USDC"] = new("USDC", "USD Coin", $"{AssetBasePath}usdc.png"),
        ["USD"] = new("USD", "US Dollar", $"{AssetBasePath}usd.png"),
        ["AAVE"] = new("AAVE", "Aave", string.Empty),
        ["ACH"] = new("ACH", "Alchemy Pay", string.Empty),
        ["ACS"] = new("ACS", "Access Protocol", string.Empty),
        ["ACX"] = new("ACX", "Across Protocol", string.Empty),
        ["AERO"] = new("AERO", "Aerodrome Finance", string.Empty),
        ["AGLD"] = new("AGLD", "Adventure Gold", string.Empty),
        ["AIOZ"] = new("AIOZ", "AIOZ Network", string.Empty),
        ["AKT"] = new("AKT", "Akash Network", string.Empty),
        ["ALCX"] = new("ALCX", "Alchemix", string.Empty),
        ["ETC"] = new("ETC", "Ethereum Classic", string.Empty),
        ["AVAX"] = new("AVAX", "Avalanche", string.Empty),
        ["MATIC"] = new("MATIC", "Polygon", string.Empty),
        ["SHIB"] = new("SHIB", "Shiba Inu", string.Empty),
        ["XLM"] = new("XLM", "Stellar", string.Empty),
    };

    public static AssetMetadata Resolve(string? symbol)
    {
        var normalized = string.IsNullOrWhiteSpace(symbol) ? "N/A" : symbol.Trim().ToUpperInvariant();
        if (normalized.Contains('-', StringComparison.Ordinal))
        {
            normalized = normalized.Split('-', 2, StringSplitOptions.RemoveEmptyEntries)[0];
        }

        if (Known.TryGetValue(normalized, out var metadata))
        {
            return metadata;
        }

        return new AssetMetadata(normalized, normalized, string.Empty);
    }
}

public sealed record AssetMetadata(string Symbol, string Name, string IconPath);

public static class AssetIconCache
{
    private static readonly Dictionary<string, Bitmap?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Bitmap? Load(string? iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return null;
        }

        if (Cache.TryGetValue(iconPath, out var bitmap))
        {
            return bitmap;
        }

        try
        {
            var fullPath = Path.Combine(AppContext.BaseDirectory, iconPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                bitmap = null;
            }
            else
            {
                bitmap = new Bitmap(fullPath);
            }
        }
        catch
        {
            bitmap = null;
        }

        Cache[iconPath] = bitmap;
        return bitmap;
    }
}
