using System.Text.Json;
using VectorQuay.App.ViewModels;
using VectorQuay.Core.Configuration;

namespace VectorQuay.Core.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public void Resolve_UsesXdgConfigHomeWhenProvided()
    {
        var paths = VectorQuayPaths.Resolve(
            homeDirectory: "/tmp/home",
            xdgConfigHome: "/tmp/xdg",
            baseDirectory: "/tmp/repo");

        Assert.Equal("/tmp/xdg/VectorQuay", paths.ConfigDirectory);
        Assert.Equal("/tmp/xdg/VectorQuay/settings.json", paths.SettingsPath);
        Assert.Equal("/tmp/xdg/VectorQuay/secrets.env", paths.SecretsPath);
    }

    [Fact]
    public void SecretFileParser_ParsesKeyValuePairsAndIgnoresComments()
    {
        const string content = """
            # comment
            VECTORQUAY_COINBASE_API_KEY=abc123
            VECTORQUAY_OPENAI_API_KEY = xyz789

            INVALID_LINE
            """;

        var parsed = SecretFileParser.Parse(content);

        Assert.Equal("abc123", parsed[SecretNames.CoinbaseApiKey]);
        Assert.Equal("xyz789", parsed[SecretNames.OpenAiApiKey]);
        Assert.False(parsed.ContainsKey("INVALID_LINE"));
    }

    [Fact]
    public void Save_WritesJsonAndLoad_ReturnsLocalSettings()
    {
        var paths = CreateTestPaths();

        var service = new SettingsService(paths);
        var settings = AppSettings.CreateDefault();
        settings.Policy.OperatorNotes = "Round-trip check";

        service.Save(settings);
        var snapshot = service.Load();

        Assert.Equal("Round-trip check", snapshot.Settings.Policy.OperatorNotes);
        Assert.Contains(snapshot.ValidationMessages, message => message.Contains("not configured yet", StringComparison.Ordinal));
    }

    [Fact]
    public void Load_PrefersEnvironmentSecretsOverSecretFile()
    {
        var paths = CreateTestPaths();
        File.WriteAllText(paths.SecretsPath, $"{SecretNames.CoinbaseApiKey}=from-file{Environment.NewLine}");
        Environment.SetEnvironmentVariable(SecretNames.CoinbaseApiKey, "from-env");

        try
        {
            var service = new SettingsService(paths);
            var snapshot = service.Load();

            Assert.Equal(SecretSource.Environment, snapshot.SecretStatuses[SecretNames.CoinbaseApiKey].Source);
            Assert.Contains(snapshot.ValidationMessages, message => message.Contains($"{SecretNames.CoinbaseApiKey} is available via Environment", StringComparison.Ordinal));
        }
        finally
        {
            Environment.SetEnvironmentVariable(SecretNames.CoinbaseApiKey, null);
        }
    }

    [Fact]
    public void Validate_ReturnsBlockingMessagesForInvalidPolicyAndThresholds()
    {
        var service = new SettingsService(CreateTestPaths());
        var settings = AppSettings.CreateDefault();
        settings.Policy.ProtectedBtcMode = "Invalid";
        settings.Risk.CustomDailyLossPct = 0m;

        var messages = service.Validate(settings);

        Assert.Contains(messages, message => message.Contains("BTC protected mode", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("custom threshold values must be positive numbers", StringComparison.Ordinal));
    }

    [Fact]
    public void Load_NormalizesBlankReleaseFeedUrlToDefault()
    {
        var paths = CreateTestPaths();
        var settings = AppSettings.CreateDefault();
        settings.General.ReleaseFeedUrl = string.Empty;
        File.WriteAllText(paths.SettingsPath, JsonSerializer.Serialize(settings));

        var service = new SettingsService(paths);
        var snapshot = service.Load();

        Assert.Equal(AppSettings.DefaultReleaseFeedUrl, snapshot.Settings.General.ReleaseFeedUrl);
    }

    [Fact]
    public void RiskProfile_SwitchingAwayFromCustomRequiresConfirmationBeforeOverwrite()
    {
        var viewModel = new MainWindowViewModel(new SettingsService(CreateTestPaths()))
        {
            SelectedRiskProfile = "Custom",
            CustomMaxPositionPct = "99",
            CustomDailyLossPct = "7",
            CustomTurnoverPct = "55",
        };

        viewModel.ApplyRiskProfileCommand.Execute("High Risk");
        Assert.Equal("Custom", viewModel.SelectedRiskProfile);
        Assert.Contains("confirm", viewModel.RiskProfileMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(viewModel.CanEditRiskThresholds);

        viewModel.ApplyRiskProfileCommand.Execute("High Risk");
        Assert.Equal("High Risk", viewModel.SelectedRiskProfile);
        Assert.False(viewModel.CanEditRiskThresholds);
        Assert.Equal("18", viewModel.CustomMaxPositionPct);
    }

    [Fact]
    public void RiskProfile_CustomIsOnlyEditableModeAndResetRestoresDefaults()
    {
        var viewModel = new MainWindowViewModel(new SettingsService(CreateTestPaths()));

        Assert.False(viewModel.CanEditRiskThresholds);

        viewModel.ApplyRiskProfileCommand.Execute("Custom");
        Assert.True(viewModel.CanEditRiskThresholds);

        viewModel.CustomMaxPositionPct = "21";
        viewModel.CustomDailyLossPct = "6";
        viewModel.CustomTurnoverPct = "44";
        viewModel.ResetRiskDefaultsCommand.Execute(null);

        Assert.Equal("Medium Risk", viewModel.SelectedRiskProfile);
        Assert.False(viewModel.CanEditRiskThresholds);
        Assert.Equal("12", viewModel.CustomMaxPositionPct);
        Assert.Equal("3", viewModel.CustomDailyLossPct);
        Assert.Equal("25", viewModel.CustomTurnoverPct);
    }

    [Fact]
    public void ValidateSettings_ProducesBlockingClassificationForInvalidEditorState()
    {
        var viewModel = new MainWindowViewModel(new SettingsService(CreateTestPaths()))
        {
            ProtectedBtcMode = "Invalid",
        };

        viewModel.ValidateSettingsCommand.Execute(null);

        Assert.Contains("blocking issues", viewModel.SettingsActionMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BTC protected mode", viewModel.ValidationSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_FallsBackGracefullyWhenSettingsJsonIsMalformed()
    {
        var paths = CreateTestPaths();
        File.WriteAllText(paths.SettingsPath, "{ this is not valid json");

        var service = new SettingsService(paths);
        var snapshot = service.Load();

        Assert.Equal("Pre-Integration", snapshot.Settings.General.ApplicationState);
        Assert.Contains(snapshot.ValidationMessages, message => message.Contains("local settings could not be parsed safely", StringComparison.Ordinal));
    }

    [Fact]
    public void Load_AddsWarningWhenSecretsFileContainsMalformedLines()
    {
        var paths = CreateTestPaths();
        File.WriteAllText(paths.SecretsPath, """
            VECTORQUAY_COINBASE_API_KEY=abc123
            MALFORMED_LINE
            =missing_key
            """);

        var service = new SettingsService(paths);
        var snapshot = service.Load();

        Assert.Equal(SecretSource.SecretFile, snapshot.SecretStatuses[SecretNames.CoinbaseApiKey].Source);
        Assert.Contains(snapshot.ValidationMessages, message => message.Contains("malformed line", StringComparison.Ordinal));
    }

    [Fact]
    public void ParseReleaseDocument_ThrowsForInvalidPayload()
    {
        using var document = JsonDocument.Parse("""{"name":"missing tag"}""");

        var exception = Assert.Throws<InvalidOperationException>(() => MainWindowViewModel.ParseReleaseDocument(document));

        Assert.Contains("tag_name", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseReleaseDocument_ReturnsExpectedReleaseDetails()
    {
        using var document = JsonDocument.Parse("""{"tag_name":"v1.0.0","name":"v1.0.0","html_url":"https://example.test/release"}""");

        var result = MainWindowViewModel.ParseReleaseDocument(document);

        Assert.Equal("v1.0.0", result.Version);
        Assert.Equal("v1.0.0", result.Name);
        Assert.Equal("https://example.test/release", result.ActionUrl);
    }

    private static VectorQuayPaths CreateTestPaths()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vectorquay-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempRoot, "config", "templates"));
        Directory.CreateDirectory(Path.Combine(tempRoot, ".config", "VectorQuay"));

        var templatePath = Path.Combine(tempRoot, "config", "templates", "appsettings.template.json");
        File.WriteAllText(templatePath, JsonSerializer.Serialize(AppSettings.CreateDefault()));

        return VectorQuayPaths.Resolve(
            homeDirectory: tempRoot,
            xdgConfigHome: Path.Combine(tempRoot, ".config"),
            baseDirectory: tempRoot);
    }
}
