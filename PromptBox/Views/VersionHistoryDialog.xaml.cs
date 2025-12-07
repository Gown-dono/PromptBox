using MaterialDesignThemes.Wpf;
using PromptBox.Models;
using PromptBox.Services;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace PromptBox.Views;

/// <summary>
/// Dialog for viewing and restoring prompt version history
/// </summary>
public partial class VersionHistoryDialog : Window
{
    private readonly IVersioningService _versioningService;
    private readonly Prompt _currentPrompt;
    private List<PromptVersion> _versions = new();
    private PromptVersion? _selectedVersion;

    public PromptVersion? RestoredVersion { get; private set; }

    public VersionHistoryDialog(IVersioningService versioningService, Prompt currentPrompt)
    {
        _versioningService = versioningService;
        _currentPrompt = currentPrompt;
        InitializeComponent();
        PromptTitleText.Text = $"Prompt: {currentPrompt.Title}";
        LoadVersions();
    }

    private async void LoadVersions()
    {
        try
        {
            _versions = await _versioningService.GetVersionsAsync(_currentPrompt.Id);
            
            if (_versions.Any())
            {
                VersionList.ItemsSource = _versions;
                VersionList.SelectedIndex = 0;
                EmptyStateText.Visibility = Visibility.Collapsed;
            }
            else
            {
                EmptyStateText.Visibility = Visibility.Visible;
                ShowNoDiffMessage("Select a version to see changes");
            }
        }
        catch (System.Exception ex)
        {
            EmptyStateText.Text = $"Error loading versions: {ex.Message}";
            EmptyStateText.Visibility = Visibility.Visible;
        }
    }
    
    private void ShowNoDiffMessage(string message)
    {
        var document = new FlowDocument();
        var paragraph = new Paragraph(new Run(message))
        {
            Foreground = (Brush)FindResource("MaterialDesignBodyLight"),
            TextAlignment = TextAlignment.Center
        };
        document.Blocks.Add(paragraph);
        DiffViewer.Document = document;
    }

    private void VersionList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _selectedVersion = VersionList.SelectedItem as PromptVersion;
        RestoreButton.IsEnabled = _selectedVersion != null;
        
        if (_selectedVersion != null)
        {
            ShowDiff(_selectedVersion);
        }
    }

    private void ShowDiff(PromptVersion version)
    {
        // Check if content is identical
        if (_currentPrompt.Content == version.Content)
        {
            ShowNoDiffMessage("Content is identical to current version.\nOnly metadata (title, category, tags) may differ.");
            return;
        }
        
        // Compare current -> old version to show what restoring would change
        // "+" = lines that will be added (exist in old version)
        // "-" = lines that will be removed (exist in current)
        var diff = _versioningService.GetDiff(_currentPrompt.Content, version.Content);
        var document = new FlowDocument();
        var paragraph = new Paragraph
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Margin = new Thickness(0)
        };

        var lines = diff.Split('\n');
        foreach (var line in lines)
        {
            Run run;
            if (line.StartsWith("+ "))
            {
                // Lines that exist in the old version (will be restored)
                run = new Run(line + "\n")
                {
                    Background = new SolidColorBrush(Color.FromArgb(40, 46, 125, 50)),
                    Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50))
                };
            }
            else if (line.StartsWith("- "))
            {
                // Lines that exist in current (will be removed if restored)
                run = new Run(line + "\n")
                {
                    Background = new SolidColorBrush(Color.FromArgb(40, 198, 40, 40)),
                    Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40))
                };
            }
            else
            {
                run = new Run(line + "\n")
                {
                    Foreground = (Brush)FindResource("MaterialDesignBody")
                };
            }
            paragraph.Inlines.Add(run);
        }

        document.Blocks.Add(paragraph);
        DiffViewer.Document = document;
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedVersion == null) return;

        var titleText = new TextBlock
        {
            Text = $"Restore to Version {_selectedVersion.VersionNumber}?",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var messageText = new TextBlock
        {
            Text = "This will replace the current content with the selected version.",
            Margin = new Thickness(0, 0, 0, 16)
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Style = Application.Current.FindResource("MaterialDesignFlatButton") as Style,
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = false,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var restoreButton = new Button
        {
            Content = "Restore",
            Style = Application.Current.FindResource("MaterialDesignRaisedButton") as Style,
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = true
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(restoreButton);

        var mainPanel = new StackPanel
        {
            Margin = new Thickness(16)
        };
        mainPanel.Children.Add(titleText);
        mainPanel.Children.Add(messageText);
        mainPanel.Children.Add(buttonPanel);

        var result = await DialogHost.Show(mainPanel, "VersionHistoryDialog");

        if (result is bool confirmed && confirmed)
        {
            RestoredVersion = _selectedVersion;
            DialogResult = true;
            Close();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
