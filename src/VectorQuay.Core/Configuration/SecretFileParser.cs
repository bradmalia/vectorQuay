namespace VectorQuay.Core.Configuration;

public static class SecretFileParser
{
    public static Dictionary<string, string> ParseFile(string path)
    {
        return Parse(File.ReadAllText(path));
    }

    public static Dictionary<string, string> Parse(string content)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

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
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            values[key] = value;
        }

        return values;
    }
}
