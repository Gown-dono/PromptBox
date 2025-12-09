using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using PromptBox.Models;
using PromptBox.Services;
using PromptBox.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace PromptBox.Views;

public partial class PromptComparisonDialog : Window
{
    private readonly IPromptComparisonService _comparisonService;
    private readonly IDatabaseService _databaseService;
    private readonly IAIService _aiService;
    private readonly IExportService _exportService;
    private readonly IVersioningService? _versioningService;

    private ObservableCollection<PromptVariationViewModel> _variations = new();
    private ObservableCollection<ComparisonResultViewModel> _results = new();
    private List<SelectableModel> _allModels = new();
    private PromptComparisonSession? _currentSession;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isRunning;
    private bool _winnerSaved;

    public PromptComparisonDialog(
        IPromptComparisonService comparisonService,
        IDatabaseService databaseService,
        IAIService aiService,
        IExportService exportService,
        IVersioningService? versioningService = null)
    {
        InitializeComponent();
        _comparisonService = comparisonService;
        _databaseService = databaseService;
        _aiService = aiService;
        _exportService = exportService;
        _versioningService = versioningService;

        VariationsListBox.ItemsSource = _variations;
        ResultsDataGrid.ItemsSource = _results;

        Loaded += async (s, e) => await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var models = await _aiService.GetAvailableModelsAsync();
            _allModels = models.Select(m => new SelectableModel { Model = m, IsSelected = false }).ToList();
            ModelsListBox.ItemsSource = _allModels;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading models: {ex.Message}";
        }
    }

    private void UpdateVariationCount()
    {
        VariationCountText.Text = $"{_variations.Count}/4";
        var canAdd = _variations.Count < 4;
        AddFromPromptButton.IsEnabled = canAdd;
        AddManualButton.IsEnabled = canAdd;
    }

    private void VariationsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var hasSelection = VariationsListBox.SelectedItem != null;
        EditVariationButton.IsEnabled = hasSelection;
        RemoveVariationButton.IsEnabled = hasSelection;
    }

    private void ModelsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Update selection state
    }

    private async void AddFromPrompt_Click(object sender, RoutedEventArgs e)
    {
        if (_variations.Count >= 4)
        {
            StatusText.Text = "Maximum 4 variations allowed";
            return;
        }

        var prompts = await _databaseService.GetAllPromptsAsync();
        if (!prompts.Any())
        {
            StatusText.Text = "No prompts available. Create some prompts first.";
            return;
        }

        var listBox = new ListBox
        {
            ItemsSource = prompts,
            DisplayMemberPath = "Title",
            MaxHeight = 300,
            MinWidth = 300
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "Select a Prompt", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(listBox);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttonPanel.Children.Add(new Button { Content = "Cancel", Style = FindResource("MaterialDesignFlatButton") as Style, Command = DialogHost.CloseDialogCommand, CommandParameter = false, Margin = new Thickness(0, 0, 8, 0) });
        buttonPanel.Children.Add(new Button { Content = "Add", Style = FindResource("MaterialDesignRaisedButton") as Style, Command = DialogHost.CloseDialogCommand, CommandParameter = true });
        panel.Children.Add(buttonPanel);

        var result = await DialogHost.Show(panel, "ComparisonDialog");
        if (result is true && listBox.SelectedItem is Prompt selectedPrompt)
        {
            var variation = new PromptVariationViewModel(
                $"Variation {_variations.Count + 1}: {selectedPrompt.Title}",
                selectedPrompt.Content,
                $"From prompt: {selectedPrompt.Title}");
            _variations.Add(variation);
            UpdateVariationCount();
            UpdateDiffComboBoxes();
        }
    }

    private async void AddManual_Click(object sender, RoutedEventArgs e)
    {
        if (_variations.Count >= 4)
        {
            StatusText.Text = "Maximum 4 variations allowed";
            return;
        }

        var nameBox = new TextBox { Margin = new Thickness(0, 0, 0, 8) };
        HintAssist.SetHint(nameBox, "Variation Name");

        var contentBox = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 150, Margin = new Thickness(0, 0, 0, 8) };
        HintAssist.SetHint(contentBox, "Prompt Content");

        var descBox = new TextBox { Margin = new Thickness(0, 0, 0, 8) };
        HintAssist.SetHint(descBox, "Description (optional)");

        var panel = new StackPanel { Margin = new Thickness(16), MinWidth = 400 };
        panel.Children.Add(new TextBlock { Text = "Add Manual Variation", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(nameBox);
        panel.Children.Add(contentBox);
        panel.Children.Add(descBox);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        buttonPanel.Children.Add(new Button { Content = "Cancel", Style = FindResource("MaterialDesignFlatButton") as Style, Command = DialogHost.CloseDialogCommand, CommandParameter = false, Margin = new Thickness(0, 0, 8, 0) });
        buttonPanel.Children.Add(new Button { Content = "Add", Style = FindResource("MaterialDesignRaisedButton") as Style, Command = DialogHost.CloseDialogCommand, CommandParameter = true });
        panel.Children.Add(buttonPanel);

        var result = await DialogHost.Show(panel, "ComparisonDialog");
        if (result is true && !string.IsNullOrWhiteSpace(nameBox.Text) && !string.IsNullOrWhiteSpace(contentBox.Text))
        {
            var variation = new PromptVariationViewModel(nameBox.Text, contentBox.Text, descBox.Text);
            _variations.Add(variation);
            UpdateVariationCount();
            UpdateDiffComboBoxes();
        }
    }

    private async void EditVariation_Click(object sender, RoutedEventArgs e)
    {
        if (VariationsListBox.SelectedItem is not PromptVariationViewModel selected) return;

        var nameBox = new TextBox { Text = selected.Name, Margin = new Thickness(0, 0, 0, 8) };
        HintAssist.SetHint(nameBox, "Variation Name");

        var contentBox = new TextBox { Text = selected.Content, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 150, Margin = new Thickness(0, 0, 0, 8) };
        HintAssist.SetHint(contentBox, "Prompt Content");

        var descBox = new TextBox { Text = selected.Description, Margin = new Thickness(0, 0, 0, 8) };
        HintAssist.SetHint(descBox, "Description (optional)");

        var panel = new StackPanel { Margin = new Thickness(16), MinWidth = 400 };
        panel.Children.Add(new TextBlock { Text = "Edit Variation", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(nameBox);
        panel.Children.Add(contentBox);
        panel.Children.Add(descBox);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        buttonPanel.Children.Add(new Button { Content = "Cancel", Style = FindResource("MaterialDesignFlatButton") as Style, Command = DialogHost.CloseDialogCommand, CommandParameter = false, Margin = new Thickness(0, 0, 8, 0) });
        buttonPanel.Children.Add(new Button { Content = "Save", Style = FindResource("MaterialDesignRaisedButton") as Style, Command = DialogHost.CloseDialogCommand, CommandParameter = true });
        panel.Children.Add(buttonPanel);

        var result = await DialogHost.Show(panel, "ComparisonDialog");
        if (result is true && !string.IsNullOrWhiteSpace(nameBox.Text) && !string.IsNullOrWhiteSpace(contentBox.Text))
        {
            selected.Name = nameBox.Text;
            selected.Content = contentBox.Text;
            selected.Description = descBox.Text;
            VariationsListBox.Items.Refresh();
            UpdateDiffComboBoxes();
        }
    }

    private void RemoveVariation_Click(object sender, RoutedEventArgs e)
    {
        if (VariationsListBox.SelectedItem is PromptVariationViewModel selected)
        {
            _variations.Remove(selected);
            UpdateVariationCount();
            UpdateDiffComboBoxes();
        }
    }

    private void TemperatureSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TemperatureValue != null)
            TemperatureValue.Text = $"{e.NewValue:F1}";
    }

    private void MaxTokensSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MaxTokensValue != null)
            MaxTokensValue.Text = $"{(int)e.NewValue}";
    }

    private async void StartComparison_Click(object sender, RoutedEventArgs e)
    {
        // Validation
        if (_variations.Count < 2)
        {
            StatusText.Text = "Please add at least 2 variations to compare";
            return;
        }

        var selectedModels = _allModels.Where(m => m.IsSelected).ToList();
        if (!selectedModels.Any())
        {
            StatusText.Text = "Please select at least one model";
            return;
        }

        if (string.IsNullOrWhiteSpace(SharedInputTextBox.Text))
        {
            StatusText.Text = "Please enter shared input (bottom-left) - this is the text all variations will be tested against";
            SharedInputTextBox.Focus();
            return;
        }

        // Create session
        _currentSession = new PromptComparisonSession
        {
            Name = string.IsNullOrWhiteSpace(ComparisonNameTextBox.Text)
                ? $"Comparison {DateTime.Now:yyyy-MM-dd HH:mm}"
                : ComparisonNameTextBox.Text,
            Description = ComparisonDescriptionTextBox.Text,
            SharedInput = SharedInputTextBox.Text,
            PromptVariations = _variations.Select(v => v.Variation).ToList(),
            ModelIds = selectedModels.Select(m => m.Model.Id).ToList(),
            Temperature = TemperatureSlider.Value,
            MaxTokens = (int)MaxTokensSlider.Value
        };

        // Update UI state
        _isRunning = true;
        StartButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        ExportButton.IsEnabled = false;
        SaveWinnerButton.IsEnabled = false;
        _results.Clear();

        var totalOps = _currentSession.PromptVariations.Count * _currentSession.ModelIds.Count;
        TotalCountText.Text = totalOps.ToString();
        CompletedCountText.Text = "0";
        FailedCountText.Text = "0";

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var completed = 0;
            var failed = 0;

            await foreach (var progress in _comparisonService.ExecuteComparisonAsync(_currentSession, _cancellationTokenSource.Token))
            {
                ProgressStatusText.Text = $"Testing {progress.CurrentVariation} with {progress.CurrentModel}...";
                ProgressPercentText.Text = $"{progress.PercentComplete:F0}%";
                ComparisonProgressBar.Value = progress.PercentComplete;

                if (progress.LastResult != null)
                {
                    var vm = new ComparisonResultViewModel(progress.LastResult);
                    _results.Add(vm);

                    if (progress.LastResult.Success) completed++;
                    else failed++;

                    CompletedCountText.Text = completed.ToString();
                    FailedCountText.Text = failed.ToString();
                    UpdateStatistics();
                }
            }

            // Refresh results with rankings
            _results.Clear();
            foreach (var result in _currentSession.Results)
            {
                _results.Add(new ComparisonResultViewModel(result));
            }

            // Automatically determine winner (top-ranked successful result)
            var topResult = _currentSession.Results
                .Where(r => r.Success)
                .OrderBy(r => r.Ranking)
                .FirstOrDefault();
            if (topResult != null)
            {
                _currentSession.WinnerVariationName = topResult.VariationName;
                _currentSession.WinnerModelId = topResult.ModelId;
            }

            // Save session
            await _databaseService.SavePromptComparisonSessionAsync(_currentSession);

            StatusText.Text = $"Comparison complete! {completed} successful, {failed} failed.";
            ExportButton.IsEnabled = true;
            SaveWinnerButton.IsEnabled = _results.Any(r => r.IsSuccess);

            GenerateSideBySideView();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Comparison cancelled";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            _isRunning = false;
            StartButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private void CancelComparison_Click(object sender, RoutedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        StatusText.Text = "Cancelling...";
    }

    private void UpdateStatistics()
    {
        if (!_results.Any()) return;

        var successResults = _results.Where(r => r.IsSuccess).ToList();
        if (successResults.Any())
        {
            AvgQualityText.Text = $"{successResults.Average(r => r.Result.QualityScore):F1}";
            var avgDuration = TimeSpan.FromTicks((long)successResults.Average(r => r.Result.Duration.Ticks));
            AvgDurationText.Text = avgDuration.TotalSeconds >= 1
                ? $"{avgDuration.TotalSeconds:F1}s"
                : $"{avgDuration.TotalMilliseconds:F0}ms";
        }
        else
        {
            AvgDurationText.Text = "--";
        }

        TotalTokensText.Text = _results.Sum(r => r.Result.TokensUsed).ToString("N0");
    }

    private void GenerateSideBySideView()
    {
        SideBySideGrid.Children.Clear();
        SideBySideGrid.RowDefinitions.Clear();
        SideBySideGrid.ColumnDefinitions.Clear();

        if (_currentSession == null || !_results.Any()) return;

        var variations = _currentSession.PromptVariations;
        var models = _currentSession.ModelIds;

        // Create columns for each variation
        SideBySideGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // Model column
        foreach (var _ in variations)
        {
            SideBySideGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        // Header row
        SideBySideGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int i = 0; i < variations.Count; i++)
        {
            var header = new TextBlock
            {
                Text = variations[i].Name,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(8),
                TextWrapping = TextWrapping.Wrap,
                Foreground = FindResource("MaterialDesignBody") as Brush
            };
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, i + 1);
            SideBySideGrid.Children.Add(header);
        }

        // Data rows for each model
        for (int modelIdx = 0; modelIdx < models.Count; modelIdx++)
        {
            SideBySideGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(200) });

            var modelId = models[modelIdx];
            var modelResult = _results.FirstOrDefault(r => r.ModelId == modelId);
            var modelLabel = new TextBlock
            {
                Text = modelResult?.ModelName ?? modelId,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(8),
                VerticalAlignment = VerticalAlignment.Top,
                Foreground = FindResource("MaterialDesignBody") as Brush
            };
            Grid.SetRow(modelLabel, modelIdx + 1);
            Grid.SetColumn(modelLabel, 0);
            SideBySideGrid.Children.Add(modelLabel);

            for (int varIdx = 0; varIdx < variations.Count; varIdx++)
            {
                var result = _results.FirstOrDefault(r =>
                    r.VariationName == variations[varIdx].Name && r.ModelId == modelId);

                var border = new Border
                {
                    BorderBrush = result?.Ranking == 1
                        ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
                        : FindResource("MaterialDesignDivider") as Brush,
                    BorderThickness = new Thickness(result?.Ranking == 1 ? 2 : 1),
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(4),
                    Padding = new Thickness(8)
                };

                var content = new StackPanel();
                if (result != null)
                {
                    content.Children.Add(new TextBlock
                    {
                        Text = $"Rank: {result.RankingIcon} | Quality: {result.QualityScoreFormatted}",
                        FontSize = 11,
                        Foreground = FindResource("MaterialDesignBodyLight") as Brush,
                        Margin = new Thickness(0, 0, 0, 4)
                    });

                    var outputScroll = new ScrollViewer { MaxHeight = 150, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                    outputScroll.Content = new TextBlock
                    {
                        Text = result.FullOutput,
                        TextWrapping = TextWrapping.Wrap,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 11,
                        Foreground = FindResource("MaterialDesignBody") as Brush
                    };
                    content.Children.Add(outputScroll);
                }
                else
                {
                    content.Children.Add(new TextBlock
                    {
                        Text = "No result",
                        Foreground = FindResource("MaterialDesignBodyLight") as Brush
                    });
                }

                border.Child = content;
                Grid.SetRow(border, modelIdx + 1);
                Grid.SetColumn(border, varIdx + 1);
                SideBySideGrid.Children.Add(border);
            }
        }
    }

    private void UpdateDiffComboBoxes()
    {
        DiffVariation1ComboBox.ItemsSource = _variations.ToList();
        DiffVariation2ComboBox.ItemsSource = _variations.ToList();

        if (_variations.Count >= 2)
        {
            DiffVariation1ComboBox.SelectedIndex = 0;
            DiffVariation2ComboBox.SelectedIndex = 1;
        }
    }

    private void DiffComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DiffVariation1ComboBox.SelectedItem is PromptVariationViewModel var1 &&
            DiffVariation2ComboBox.SelectedItem is PromptVariationViewModel var2)
        {
            ShowDiffBetweenVariations(var1.Content, var2.Content);
        }
    }

    private void ShowDiffBetweenVariations(string content1, string content2)
    {
        DiffRichTextBox.Document.Blocks.Clear();

        if (_versioningService == null)
        {
            // Fallback if versioning service not available
            var fallbackParagraph = new Paragraph();
            fallbackParagraph.Inlines.Add(new Run("Diff service not available") { Foreground = FindResource("MaterialDesignBodyLight") as Brush });
            DiffRichTextBox.Document.Blocks.Add(fallbackParagraph);
            return;
        }

        // Use the existing diff infrastructure from IVersioningService
        var diff = _versioningService.GetDiff(content1, content2);
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
                // Lines added in variation 2
                run = new Run(line + "\n")
                {
                    Background = new SolidColorBrush(Color.FromArgb(40, 46, 125, 50)),
                    Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50))
                };
            }
            else if (line.StartsWith("- "))
            {
                // Lines removed from variation 1
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
                    Foreground = FindResource("MaterialDesignBody") as Brush
                };
            }
            paragraph.Inlines.Add(run);
        }

        DiffRichTextBox.Document.Blocks.Add(paragraph);
    }

    private void ResultsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Could update detail view here
    }

    private async void ViewFullOutput_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsDataGrid.SelectedItem is not ComparisonResultViewModel selected) return;

        var textBox = new TextBox
        {
            Text = selected.FullOutput,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 400,
            MinWidth = 500,
            FontFamily = new FontFamily("Consolas")
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text = $"Output: {selected.VariationName} - {selected.ModelName}",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        });
        panel.Children.Add(textBox);
        panel.Children.Add(new Button
        {
            Content = "Close",
            Style = FindResource("MaterialDesignFlatButton") as Style,
            Command = DialogHost.CloseDialogCommand,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        });

        await DialogHost.Show(panel, "ComparisonDialog");
    }

    private void CopyOutput_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsDataGrid.SelectedItem is ComparisonResultViewModel selected)
        {
            Clipboard.SetText(selected.FullOutput);
            StatusText.Text = "Output copied to clipboard";
        }
    }

    private async void ExportReport_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSession == null) return;

        var dialog = new SaveFileDialog
        {
            Filter = "Markdown files (*.md)|*.md",
            FileName = $"comparison_report_{DateTime.Now:yyyyMMdd_HHmmss}.md"
        };

        if (dialog.ShowDialog() == true)
        {
            await _exportService.ExportPromptComparisonReportAsync(_currentSession, _currentSession.Results, dialog.FileName);
            StatusText.Text = "Report exported successfully";
        }
    }

    private async void SaveWinner_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSession == null || !_results.Any()) return;

        var winner = _results.OrderBy(r => r.Ranking).FirstOrDefault(r => r.IsSuccess);
        if (winner == null)
        {
            StatusText.Text = "No successful results to save as winner";
            return;
        }

        var winnerVariation = _currentSession.PromptVariations.FirstOrDefault(v => v.Name == winner.VariationName);
        if (winnerVariation == null) return;

        var nameBox = new TextBox { Text = $"Winner: {winner.VariationName}", Margin = new Thickness(0, 0, 0, 8) };
        HintAssist.SetHint(nameBox, "Prompt Title");

        var categoryBox = new TextBox { Text = "Comparison Winner", Margin = new Thickness(0, 0, 0, 8) };
        HintAssist.SetHint(categoryBox, "Category");

        var tagsBox = new TextBox { Text = "comparison, winner", Margin = new Thickness(0, 0, 0, 8) };
        HintAssist.SetHint(tagsBox, "Tags (comma-separated)");

        var panel = new StackPanel { Margin = new Thickness(16), MinWidth = 400 };
        panel.Children.Add(new TextBlock
        {
            Text = "Save Winner as New Prompt",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"Winner: {winner.VariationName} with {winner.ModelName}\nQuality Score: {winner.QualityScoreFormatted}",
            Foreground = FindResource("MaterialDesignBodyLight") as Brush,
            Margin = new Thickness(0, 0, 0, 12)
        });
        panel.Children.Add(nameBox);
        panel.Children.Add(categoryBox);
        panel.Children.Add(tagsBox);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttonPanel.Children.Add(new Button { Content = "Cancel", Style = FindResource("MaterialDesignFlatButton") as Style, Command = DialogHost.CloseDialogCommand, CommandParameter = false, Margin = new Thickness(0, 0, 8, 0) });
        buttonPanel.Children.Add(new Button { Content = "Save", Style = FindResource("MaterialDesignRaisedButton") as Style, Command = DialogHost.CloseDialogCommand, CommandParameter = true });
        panel.Children.Add(buttonPanel);

        var result = await DialogHost.Show(panel, "ComparisonDialog");
        if (result is true && !string.IsNullOrWhiteSpace(nameBox.Text))
        {
            var newPrompt = new Prompt
            {
                Title = nameBox.Text,
                Content = winnerVariation.Content,
                Category = categoryBox.Text,
                Tags = tagsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList()
            };

            await _databaseService.SavePromptAsync(newPrompt);

            // Update session with winner info
            _currentSession.WinnerVariationName = winner.VariationName;
            _currentSession.WinnerModelId = winner.ModelId;
            await _databaseService.SavePromptComparisonSessionAsync(_currentSession);

            _winnerSaved = true;
            StatusText.Text = "Winner saved as new prompt!";
        }
    }

    private async void ViewHistory_Click(object sender, RoutedEventArgs e)
    {
        PromptComparisonSession? sessionToDelete = null;

        while (true)
        {
            var sessions = await _databaseService.GetAllPromptComparisonSessionsAsync();
            if (!sessions.Any())
            {
                StatusText.Text = "No comparison history found";
                return;
            }

            var listBox = new ListBox { MaxHeight = 300, MinWidth = 400 };
            listBox.ItemTemplate = CreateHistoryItemTemplate();
            listBox.ItemsSource = sessions;

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = "Comparison History", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
            panel.Children.Add(listBox);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            var deleteButton = new Button { Content = "Delete", Style = FindResource("MaterialDesignOutlinedButton") as Style, Margin = new Thickness(0, 0, 8, 0) };
            buttonPanel.Children.Add(deleteButton);
            buttonPanel.Children.Add(new Button { Content = "Close", Style = FindResource("MaterialDesignFlatButton") as Style, Command = DialogHost.CloseDialogCommand, CommandParameter = "close" });
            panel.Children.Add(buttonPanel);

            // Handle delete button - close dialog and return selected session
            deleteButton.Click += (s, args) =>
            {
                sessionToDelete = listBox.SelectedItem as PromptComparisonSession;
                DialogHost.CloseDialogCommand.Execute("delete", deleteButton);
            };

            var result = await DialogHost.Show(panel, "ComparisonDialog");

            // If delete was clicked and a session was selected
            if (result?.ToString() == "delete" && sessionToDelete != null)
            {
                // Show confirmation dialog
                var confirmed = await ShowConfirmationAsync(
                    $"Are you sure you want to delete the comparison session '{sessionToDelete.Name}'?\n\nThis action cannot be undone.",
                    "Confirm Delete");

                if (confirmed)
                {
                    await _databaseService.DeletePromptComparisonSessionAsync(sessionToDelete.Id);
                    StatusText.Text = $"Deleted comparison session '{sessionToDelete.Name}'";
                }

                sessionToDelete = null;
                // Loop back to show history dialog again
                continue;
            }

            // Close was clicked or dialog was dismissed
            break;
        }
    }

    private async Task<bool> ShowConfirmationAsync(string message, string title)
    {
        var messageText = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) };

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttonPanel.Children.Add(new Button { Content = "Cancel", Style = (Style)FindResource("MaterialDesignFlatButton"), Command = DialogHost.CloseDialogCommand, CommandParameter = false, Margin = new Thickness(0, 0, 8, 0) });
        buttonPanel.Children.Add(new Button { Content = "Delete", Style = (Style)FindResource("MaterialDesignRaisedButton"), Command = DialogHost.CloseDialogCommand, CommandParameter = true });

        var mainPanel = new StackPanel { Margin = new Thickness(16), MinWidth = 300 };
        mainPanel.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
        mainPanel.Children.Add(messageText);
        mainPanel.Children.Add(buttonPanel);

        var result = await DialogHost.Show(mainPanel, "ComparisonDialog");
        return result is bool confirmed && confirmed;
    }

    private DataTemplate CreateHistoryItemTemplate()
    {
        var template = new DataTemplate();
        var factory = new FrameworkElementFactory(typeof(StackPanel));
        factory.SetValue(StackPanel.MarginProperty, new Thickness(8));

        var nameFactory = new FrameworkElementFactory(typeof(TextBlock));
        nameFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
        nameFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        factory.AppendChild(nameFactory);

        var dateFactory = new FrameworkElementFactory(typeof(TextBlock));
        dateFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("CreatedDate") { StringFormat = "Created: {0:MMM dd, yyyy HH:mm}" });
        dateFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
        factory.AppendChild(dateFactory);

        var winnerFactory = new FrameworkElementFactory(typeof(TextBlock));
        winnerFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("WinnerVariationName") { StringFormat = "Winner: {0}" });
        winnerFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
        factory.AppendChild(winnerFactory);

        template.VisualTree = factory;
        return template;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            _cancellationTokenSource?.Cancel();
        }
        DialogResult = _winnerSaved;
        Close();
    }
}
