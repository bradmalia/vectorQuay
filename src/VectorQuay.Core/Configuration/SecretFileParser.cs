namespace VectorQuay.Core.Configuration;

public static class SecretFileParser
{
    public static Dictionary<string, string> ParseFile(string path)
    {
        return ParseWithDiagnostics(File.ReadAllText(path)).Values;
    }

    public static Dictionary<string, string> Parse(string content)
    {
        return ParseWithDiagnostics(content).Values;
    }

    public static SecretFileParseResult ParseWithDiagnostics(string content)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var invalidLines = new List<string>();

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                invalidLines.Add(line);
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(key))
            {
                invalidLines.Add(line);
                continue;
            }

            values[key] = value;
        }

        return new SecretFileParseResult
        {
            Values = values,
            InvalidLines = invalidLines,
        };
    }
}

public sealed class SecretFileParseResult
{
    public required Dictionary<string, string> Values { get; init; }

    public required IReadOnlyList<string> InvalidLines { get; init; }
}
