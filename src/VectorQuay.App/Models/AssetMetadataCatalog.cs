using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace VectorQuay.App.Models;

public static class AssetMetadataCatalog
{
    private const string AssetBasePath = "Assets/Crypto/";
    private const string RemoteIconBaseUrl = "https://raw.githubusercontent.com/spothq/cryptocurrency-icons/master/128/color/";

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
            return string.IsNullOrWhiteSpace(metadata.IconPath)
                ? metadata with { IconPath = GetCachedIconPathOrEmpty(normalized) }
                : metadata;
        }

        return new AssetMetadata(normalized, normalized, GetCachedIconPathOrEmpty(normalized));
    }

    public static async Task<int> EnsureCachedIconsAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default)
    {
        var normalizedSymbols = symbols
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Select(symbol => Normalize(symbol!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedSymbols.Count == 0)
        {
            return 0;
        }

        var cacheDirectory = GetCacheDirectory();
        Directory.CreateDirectory(cacheDirectory);
        using var client = new HttpClient();
        var downloaded = 0;

        foreach (var symbol in normalizedSymbols)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Known.TryGetValue(symbol, out var metadata) && !string.IsNullOrWhiteSpace(metadata.IconPath))
            {
                continue;
            }

            var targetPath = Path.Combine(cacheDirectory, $"{symbol.ToLowerInvariant()}.png");
            if (File.Exists(targetPath))
            {
                continue;
            }

            try
            {
                using var response = await client.GetAsync($"{RemoteIconBaseUrl}{symbol.ToLowerInvariant()}.png", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = File.Create(targetPath);
                await stream.CopyToAsync(fileStream, cancellationToken);
                downloaded++;
            }
            catch
            {
                // Keep icon loading best-effort only.
            }
        }

        if (downloaded > 0)
        {
            AssetIconCache.Clear();
        }

        return downloaded;
    }

    private static string Normalize(string symbol)
    {
        var normalized = symbol.Trim().ToUpperInvariant();
        if (normalized.Contains('-', StringComparison.Ordinal))
        {
            normalized = normalized.Split('-', 2, StringSplitOptions.RemoveEmptyEntries)[0];
        }

        return normalized;
    }

    private static string GetCachedIconPathOrEmpty(string normalizedSymbol)
    {
        var path = Path.Combine(GetCacheDirectory(), $"{normalizedSymbol.ToLowerInvariant()}.png");
        return File.Exists(path) ? path : string.Empty;
    }

    private static string GetCacheDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", "VectorQuay", "asset-icons");
    }
}

public sealed record AssetMetadata(string Symbol, string Name, string IconPath);

public static class AssetIconCache
{
    private static readonly Dictionary<string, Bitmap?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static void Clear() => Cache.Clear();

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
            var fullPath = Path.IsPathRooted(iconPath)
                ? iconPath
                : Path.Combine(AppContext.BaseDirectory, iconPath.Replace('/', Path.DirectorySeparatorChar));
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
