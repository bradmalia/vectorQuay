using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using VectorQuay.App.Models;
using VectorQuay.App.ViewModels;
using VectorQuay.App.Views;
using VectorQuay.Core.Coinbase;
using VectorQuay.Core.Configuration;
using VectorQuay.Core.Persistence;

namespace VectorQuay.App;

public partial class App : Application
{
    private static readonly Lazy<IServiceProvider> _serviceProvider = new(ConfigureServices);

    public static IServiceProvider Services => _serviceProvider.Value;

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Paths and configuration (singleton, computed once)
        var paths = VectorQuayPaths.Resolve(baseDirectory: AppContext.BaseDirectory);
        services.AddSingleton(paths);

        var settingsService = SettingsService.CreateForCurrentUser(AppContext.BaseDirectory);
        services.AddSingleton(settingsService);

        // HttpClient factory for pooled, leak-free HTTP clients
        services.AddHttpClient();

        // Core services (all resolved through DI)
        // Register IAuthenticatedCoinbaseReadOnlyClient with injected HttpClientFactory
        services.AddSingleton<IAuthenticatedCoinbaseReadOnlyClient, CoinbaseReadOnlyClient>();
        
        // Wire ICoinbaseReadOnlyService through DI (needs authenticated client)
        services.AddSingleton<ICoinbaseReadOnlyService>(sp =>
            new CoinbaseShellDataService(paths, sp.GetRequiredService<IAuthenticatedCoinbaseReadOnlyClient>()));
        
        services.AddSingleton<ILocalStateStore>(sp =>
            new LocalStateStore(paths));

        // Asset metadata catalog as injectable instance with IHttpClientFactory
        // Register AssetMetadataCatalog with IHttpClientFactory for icon downloads
        services.AddSingleton<AssetMetadataCatalog>(sp => new AssetMetadataCatalog(sp.GetRequiredService<IHttpClientFactory>()));

        var sp = services.BuildServiceProvider();
        
        // Synchronize static forwarder so call sites (e.g., MainWindowViewModel) 
        // don't fall back to a raw fallback instance with null HttpClientFactory.
        AssetMetadataCatalog.Instance = sp.GetRequiredService<AssetMetadataCatalog>();

        return sp;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var sp = Services;
            var settingsService = sp.GetRequiredService<SettingsService>();
            var coinbaseService = sp.GetRequiredService<ICoinbaseReadOnlyService>();
            var localStateStore = sp.GetRequiredService<ILocalStateStore>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    settingsService, 
                    coinbaseService, 
                    localStateStore, 
                    true,
                    httpClientFactory),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
