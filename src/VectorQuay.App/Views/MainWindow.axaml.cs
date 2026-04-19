using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using VectorQuay.App.ViewModels;

namespace VectorQuay.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnBrowseCoinbaseJson(object? sender, RoutedEventArgs e)
    {
        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Select Coinbase JSON Key File",
            FileTypeFilter =
            [
                new FilePickerFileType("Coinbase JSON")
                {
                    Patterns = ["*.json", "*.json.txt", "*.txt"]
                }
            ]
        });

        var file = result.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetCoinbaseJsonImportPath(file.Path.LocalPath);
        }
    }

    private async void OnAddSource(object? sender, RoutedEventArgs e)
    {
        await OpenSourceEditor("Direct Source");
    }

    private async void OnAddWatcher(object? sender, RoutedEventArgs e)
    {
        await OpenSourceEditor("Watcher");
    }

    private async Task OpenSourceEditor(string type)
    {
        var dialog = new SourceEditorWindow(type);
        await dialog.ShowDialog(this);
        if (dialog.Result is null)
        {
            return;
        }

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.AddOrUpdateSourceFromDialog(dialog.Result.Name, dialog.Result.Type, dialog.Result.Scope, dialog.Result.Weight, true, null);
        }
    }
}
