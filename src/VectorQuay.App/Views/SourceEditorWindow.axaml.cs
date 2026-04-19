using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace VectorQuay.App.Views;

public partial class SourceEditorWindow : Window
{
    private readonly string _type;

    public SourceEditorWindow()
        : this("Direct Source")
    {
    }

    public SourceEditorWindow(string type)
    {
        InitializeComponent();
        _type = type;
        Title = type == "Watcher" ? "Add Watcher" : "Add Source";
        TitleText.Text = Title;
        SaveButton.Content = type == "Watcher" ? "Save Watcher" : "Save Source";
    }

    public SourceEditorResult? Result { get; private set; }

    private void OnGenerateDraft(object? sender, RoutedEventArgs e)
    {
        var prompt = PromptBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            HelperText.Text = "Enter a prompt first so the draft has something to infer from.";
            return;
        }

        NameBox.Text = BuildName(prompt, _type);
        ScopeBox.Text = prompt;
        WeightBox.SelectedIndex = _type == "Watcher" ? 1 : 0;
        HelperText.Text = "Draft generated locally from your prompt. Refine the fields if needed, then save.";
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            HelperText.Text = "Name is required.";
            return;
        }

        Result = new SourceEditorResult(
            NameBox.Text.Trim(),
            _type,
            ScopeBox.Text?.Trim() ?? string.Empty,
            ((WeightBox.SelectedItem as ComboBoxItem)?.Content?.ToString()) ?? "Default");
        Close();
    }

    private static string BuildName(string prompt, string type)
    {
        if (type == "Watcher")
        {
            if (prompt.Contains("reddit", StringComparison.OrdinalIgnoreCase) || prompt.Contains("subreddit", StringComparison.OrdinalIgnoreCase))
            {
                return "New Reddit Watcher";
            }

            if (prompt.Contains("news", StringComparison.OrdinalIgnoreCase))
            {
                return "New News Watcher";
            }

            return "New Watcher";
        }

        if (prompt.Contains("robinhood", StringComparison.OrdinalIgnoreCase))
        {
            return "Robinhood Trade Data";
        }

        if (prompt.Contains("coinbase", StringComparison.OrdinalIgnoreCase))
        {
            return "Coinbase Data Feed";
        }

        return "New Source";
    }
}

public sealed record SourceEditorResult(string Name, string Type, string Scope, string Weight);
