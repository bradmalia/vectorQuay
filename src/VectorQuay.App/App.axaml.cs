using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VectorQuay.App.ViewModels;
using VectorQuay.App.Views;
using VectorQuay.Core.Coinbase;
using VectorQuay.Core.Configuration;

namespace VectorQuay.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsService = SettingsService.CreateForCurrentUser(AppContext.BaseDirectory);
            var coinbaseService = new CoinbaseShellDataService(VectorQuayPaths.Resolve(baseDirectory: AppContext.BaseDirectory));

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(settingsService, coinbaseService, true),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
