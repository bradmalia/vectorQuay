namespace VectorQuay.Core.Configuration;

public sealed class SettingsSnapshot
{
    public required VectorQuayPaths Paths { get; init; }

    public required AppSettings Settings { get; init; }

    public required IReadOnlyDictionary<string, SecretStatus> SecretStatuses { get; init; }

    public required IReadOnlyList<string> ValidationMessages { get; init; }
}

public sealed class SecretStatus
{
    public required string Name { get; init; }

    public required SecretSource Source { get; init; }
}

public enum SecretSource
{
    Missing,
    SecretFile,
    Environment,
}
