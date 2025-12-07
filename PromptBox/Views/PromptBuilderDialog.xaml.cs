using MaterialDesignThemes.Wpf;
using PromptBox.Models;
using PromptBox.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PromptBox.Views;

public partial class PromptBuilderDialog : Window
{
    private readonly IAIService _aiService;
    private readonly ISecureStorageService _secureStorage;
    private readonly IPromptSuggestionService _suggestionService;
    private CancellationTokenSource? _cancellationTokenSource;
    private List<AIModel> _availableModels = new();
    private string _currentResponse = string.Empty;
    private List<PromptStarter> _promptStarters = new();
    private bool _isProcessing;
    
    private void ShowMessage(string message)
    {
        StatusText.Text = message;
    }
    
    private async Task<bool> ShowConfirmationAsync(string message, string title)
    {
        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
            Foreground = (Brush)FindResource("MaterialDesignBody")
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Style = (Style)FindResource("MaterialDesignFlatButton"),
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = false,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var confirmButton = new Button
        {
            Content = "OK",
            Style = (Style)FindResource("MaterialDesignRaisedButton"),
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = true
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(confirmButton);

        var mainPanel = new StackPanel { Margin = new Thickness(16), MinWidth = 300 };
        mainPanel.Children.Add(new TextBlock 
        { 
            Text = title, 
            FontSize = 16, 
            FontWeight = FontWeights.SemiBold, 
            Margin = new Thickness(0, 0, 0, 12),
            Foreground = (Brush)FindResource("MaterialDesignBody")
        });
        mainPanel.Children.Add(messageText);
        mainPanel.Children.Add(buttonPanel);

        var result = await DialogHost.Show(mainPanel, "PromptBuilderDialog");
        return result is bool confirmed && confirmed;
    }
    
    public string? ResultPrompt { get; private set; }
    public string? InitialPrompt { get; set; }

    public PromptBuilderDialog(IAIService aiService, ISecureStorageService secureStorage, IPromptSuggestionService? suggestionService = null)
    {
        InitializeComponent();
        _aiService = aiService;
        _secureStorage = secureStorage;
        _suggestionService = suggestionService ?? new PromptSuggestionService();
        
        _promptStarters = _suggestionService.GetPromptStarters();
        
        TemperatureSlider.ValueChanged += (s, e) => 
            TemperatureValue.Text = TemperatureSlider.Value.ToString("F1");
        
        MaxTokensSlider.ValueChanged += (s, e) => 
            MaxTokensValue.Text = ((int)MaxTokensSlider.Value).ToString();
        
        Loaded += async (s, e) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadAvailableModels();
        
        if (!string.IsNullOrEmpty(InitialPrompt))
        {
            PromptInput.Text = InitialPrompt;
        }
    }

    private async Task LoadAvailableModels()
    {
        _availableModels = await _aiService.GetAvailableModelsAsync();
        
        var hasConfiguredModels = _availableModels.Count > 0;
        
        if (!hasConfiguredModels)
        {
            StatusText.Text = "⚠️ No API keys configured. Click Settings to add API keys.";
            // Add placeholder models for display
            _availableModels = new List<AIModel>(AIProviders.AvailableModels);
        }
        else
        {
            StatusText.Text = "Ready";
        }
        
        // Always update button states based on whether we have configured API keys
        TestPromptButton.IsEnabled = hasConfiguredModels;
        AnalyzeButton.IsEnabled = hasConfiguredModels;
        GetSuggestionsButton.IsEnabled = hasConfiguredModels;
        
        ModelComboBox.ItemsSource = _availableModels;
        if (_availableModels.Count > 0)
            ModelComboBox.SelectedIndex = 0;
    }

    private AIGenerationSettings GetCurrentSettings()
    {
        var selectedModel = ModelComboBox.SelectedItem as AIModel;
        return new AIGenerationSettings
        {
            ModelId = selectedModel?.Id ?? "gpt-4o-mini",
            Temperature = TemperatureSlider.Value,
            MaxOutputTokens = (int)MaxTokensSlider.Value
        };
    }

    private void ModelComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var model = ModelComboBox.SelectedItem as AIModel;
        if (model != null)
        {
            StatusText.Text = $"Selected: {model.Name} ({model.Provider}) - {model.Description}";
        }
    }

    private async void TestPrompt_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PromptInput.Text))
        {
            ShowMessage("⚠️ Please enter a prompt to test");
            return;
        }

        var availableModels = await _aiService.GetAvailableModelsAsync();
        if (availableModels.Count == 0)
        {
            ShowMessage("⚠️ No API keys configured. Click Settings to add API keys.");
            return;
        }

        await RunWithLoadingAsync(async () =>
        {
            _currentResponse = string.Empty;
            ResponseViewer.Markdown = "";
            
            // Dispose previous CancellationTokenSource if exists
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            var settings = GetCurrentSettings();
            
            try
            {
                await foreach (var chunk in _aiService.GenerateStreamAsync(PromptInput.Text, settings, _cancellationTokenSource.Token))
                {
                    _currentResponse += chunk;
                    await Dispatcher.InvokeAsync(() => ResponseViewer.Markdown = _currentResponse);
                }
                
                TokenCount.Text = $"Response length: {_currentResponse.Length} chars";
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "Generation stopped";
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        });
    }

    private async void AnalyzePrompt_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PromptInput.Text))
        {
            ShowMessage("⚠️ Please enter a prompt to analyze");
            return;
        }

        var availableModels = await _aiService.GetAvailableModelsAsync();
        if (availableModels.Count == 0)
        {
            ShowMessage("⚠️ No API keys configured. Click Settings to add API keys.");
            return;
        }

        await RunWithLoadingAsync(async () =>
        {
            var settings = GetCurrentSettings();
            var analysis = await _aiService.AnalyzePromptAsync(PromptInput.Text, settings);
            
            await Dispatcher.InvokeAsync(() =>
            {
                AnalysisPanel.Visibility = Visibility.Visible;
                QualityScore.Text = $"Quality: {analysis.QualityScore}/100";
                
                var color = analysis.QualityScore switch
                {
                    >= 80 => Color.FromRgb(76, 175, 80),   // Green
                    >= 60 => Color.FromRgb(255, 193, 7),   // Yellow
                    >= 40 => Color.FromRgb(255, 152, 0),   // Orange
                    _ => Color.FromRgb(244, 67, 54)        // Red
                };
                QualityBadge.Background = new SolidColorBrush(color);
                
                AnalysisSummary.Text = analysis.Summary;
                ImprovementsList.ItemsSource = analysis.Improvements;
            });
        });
    }

    private async void EnhanceClarity_Click(object sender, RoutedEventArgs e) => await EnhancePrompt("clarity");
    private async void EnhanceDetail_Click(object sender, RoutedEventArgs e) => await EnhancePrompt("detail");
    private async void EnhanceConcise_Click(object sender, RoutedEventArgs e) => await EnhancePrompt("concise");
    private async void EnhanceProfessional_Click(object sender, RoutedEventArgs e) => await EnhancePrompt("professional");
    private async void EnhanceStructured_Click(object sender, RoutedEventArgs e) => await EnhancePrompt("structured");

    private async Task EnhancePrompt(string enhancementType)
    {
        if (string.IsNullOrWhiteSpace(PromptInput.Text))
        {
            ShowMessage("⚠️ Please enter a prompt to enhance");
            return;
        }

        var availableModels = await _aiService.GetAvailableModelsAsync();
        if (availableModels.Count == 0)
        {
            ShowMessage("⚠️ No API keys configured. Click Settings to add API keys.");
            return;
        }

        await RunWithLoadingAsync(async () =>
        {
            var settings = GetCurrentSettings();
            var enhanced = await _aiService.EnhancePromptAsync(PromptInput.Text, enhancementType, settings);
            
            await Dispatcher.InvokeAsync(() =>
            {
                PromptInput.Text = enhanced;
                StatusText.Text = $"✓ Prompt enhanced ({enhancementType})";
            });
        });
    }

    private async void GenerateVariations_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PromptInput.Text))
        {
            ShowMessage("⚠️ Please enter a prompt to generate variations");
            return;
        }

        var availableModels = await _aiService.GetAvailableModelsAsync();
        if (availableModels.Count == 0)
        {
            ShowMessage("⚠️ No API keys configured. Click Settings to add API keys.");
            return;
        }

        await RunWithLoadingAsync(async () =>
        {
            var settings = GetCurrentSettings();
            var variations = await _aiService.GenerateVariationsAsync(PromptInput.Text, 3, settings);
            
            await Dispatcher.InvokeAsync(() =>
            {
                VariationsPanel.Visibility = Visibility.Visible;
                VariationsList.ItemsSource = variations;
                StatusText.Text = $"✓ Generated {variations.Count} variations";
            });
        });
    }

    private async void VariationsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (VariationsList.SelectedItem is string variation)
        {
            var confirmed = await ShowConfirmationAsync("Use this variation as your prompt?", "Use Variation");
            
            if (confirmed)
            {
                PromptInput.Text = variation;
                VariationsPanel.Visibility = Visibility.Collapsed;
                ShowMessage("✓ Variation applied");
            }
            
            VariationsList.SelectedItem = null;
        }
    }

    private void InsertVariable_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is string variable)
        {
            var caretIndex = PromptInput.CaretIndex;
            var variableText = $"{{{{{variable}}}}}";
            PromptInput.Text = PromptInput.Text.Insert(caretIndex, variableText);
            PromptInput.CaretIndex = caretIndex + variableText.Length;
            PromptInput.Focus();
        }
    }

    private async void StarterTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is string starterName)
        {
            var starter = _promptStarters.FirstOrDefault(s => s.Name == starterName);
            if (starter != null)
            {
                if (!string.IsNullOrWhiteSpace(PromptInput.Text))
                {
                    var confirmed = await ShowConfirmationAsync("Replace current prompt with this template?", "Use Template");
                    if (!confirmed)
                        return;
                }
                
                PromptInput.Text = starter.Template;
                ShowMessage($"✓ Loaded template: {starter.Name}");
            }
        }
    }

    private void PromptInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        // Hide analysis and suggestions when prompt changes
        AnalysisPanel.Visibility = Visibility.Collapsed;
        SuggestionsList.Visibility = Visibility.Collapsed;
    }

    private async void GetAISuggestions_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PromptInput.Text))
        {
            ShowMessage("⚠️ Please enter a prompt to get suggestions");
            return;
        }

        var availableModels = await _aiService.GetAvailableModelsAsync();
        if (availableModels.Count == 0)
        {
            ShowMessage("⚠️ No API keys configured. Click Settings to add API keys.");
            return;
        }

        await RunWithLoadingAsync(async () =>
        {
            var settings = GetCurrentSettings();
            var (suggestions, error) = await _aiService.GetSmartSuggestionsWithErrorAsync(PromptInput.Text, settings);
            
            await Dispatcher.InvokeAsync(() =>
            {
                if (!string.IsNullOrEmpty(error))
                {
                    StatusText.Text = $"❌ {error}";
                    SuggestionsList.Visibility = Visibility.Collapsed;
                }
                else if (suggestions.Count > 0)
                {
                    SuggestionsList.ItemsSource = suggestions;
                    SuggestionsList.Visibility = Visibility.Visible;
                    StatusText.Text = $"✓ Generated {suggestions.Count} suggestions";
                }
                else
                {
                    StatusText.Text = "⚠️ No suggestions generated";
                }
            });
        });
    }

    private void SuggestionItem_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is string suggestion)
        {
            ShowSuggestionPopup(suggestion);
        }
    }

    private void ViewFullSuggestion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string suggestion)
        {
            ShowSuggestionPopup(suggestion);
        }
    }

    private void ApplySuggestionFromMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string suggestion)
        {
            PromptInput.Text = suggestion;
            SuggestionsList.Visibility = Visibility.Collapsed;
            ShowMessage("✓ Suggestion applied");
        }
    }

    private void CopySuggestion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string suggestion)
        {
            Clipboard.SetText(suggestion);
            ShowMessage("✓ Suggestion copied to clipboard");
        }
    }

    private async void ShowSuggestionPopup(string suggestion)
    {
        var scrollViewer = new ScrollViewer
        {
            MaxHeight = 400,
            MaxWidth = 600,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var suggestionText = new TextBox
        {
            Text = suggestion,
            TextWrapping = TextWrapping.Wrap,
            IsReadOnly = true,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = (Brush)FindResource("MaterialDesignBody"),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Padding = new Thickness(8)
        };
        scrollViewer.Content = suggestionText;

        var applyButton = new Button
        {
            Content = "Apply",
            Style = (Style)FindResource("MaterialDesignRaisedButton"),
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = "apply",
            Margin = new Thickness(0, 0, 8, 0)
        };

        var copyButton = new Button
        {
            Content = "Copy",
            Style = (Style)FindResource("MaterialDesignFlatButton"),
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = "copy",
            Margin = new Thickness(0, 0, 8, 0)
        };

        var closeButton = new Button
        {
            Content = "Close",
            Style = (Style)FindResource("MaterialDesignFlatButton"),
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = "close"
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        buttonPanel.Children.Add(applyButton);
        buttonPanel.Children.Add(copyButton);
        buttonPanel.Children.Add(closeButton);

        var mainPanel = new StackPanel { Margin = new Thickness(16), MinWidth = 400 };
        mainPanel.Children.Add(new TextBlock
        {
            Text = "Full Suggestion",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12),
            Foreground = (Brush)FindResource("MaterialDesignBody")
        });
        mainPanel.Children.Add(new Border
        {
            Background = (Brush)FindResource("MaterialDesignCardBackground"),
            CornerRadius = new CornerRadius(4),
            BorderBrush = (Brush)FindResource("MaterialDesignDivider"),
            BorderThickness = new Thickness(1),
            Child = scrollViewer
        });
        mainPanel.Children.Add(buttonPanel);

        var result = await DialogHost.Show(mainPanel, "PromptBuilderDialog");
        
        if (result is string action)
        {
            switch (action)
            {
                case "apply":
                    PromptInput.Text = suggestion;
                    SuggestionsList.Visibility = Visibility.Collapsed;
                    ShowMessage("✓ Suggestion applied");
                    break;
                case "copy":
                    Clipboard.SetText(suggestion);
                    ShowMessage("✓ Suggestion copied to clipboard");
                    break;
            }
        }
    }

    private void StopGeneration_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _cancellationTokenSource?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Token source already disposed, ignore
        }
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AISettingsDialog(_secureStorage, _aiService)
        {
            Owner = this
        };
        dialog.ShowDialog();
        
        // Reload available models after settings change
        await LoadAvailableModels();
    }

    private void CopyResponse_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_currentResponse))
        {
            Clipboard.SetText(_currentResponse);
            StatusText.Text = "✓ Response copied to clipboard";
        }
    }

    private void UseAsPrompt_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_currentResponse))
        {
            PromptInput.Text = _currentResponse;
            _currentResponse = string.Empty;
            ResponseViewer.Markdown = "";
            StatusText.Text = "✓ Response moved to prompt input";
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PromptInput.Text))
        {
            ShowMessage("⚠️ Please enter a prompt to save");
            return;
        }

        ResultPrompt = PromptInput.Text;
        DialogResult = true;
        Close();
    }

    private async Task RunWithLoadingAsync(Func<Task> action)
    {
        // Prevent concurrent operations
        if (_isProcessing)
        {
            ShowMessage("⚠️ Please wait for the current operation to complete");
            return;
        }
        
        _isProcessing = true;
        LoadingIndicator.Visibility = Visibility.Visible;
        StopButton.Visibility = Visibility.Visible;
        TestPromptButton.IsEnabled = false;
        AnalyzeButton.IsEnabled = false;
        GetSuggestionsButton.IsEnabled = false;
        StatusText.Text = "Processing...";

        try
        {
            await action();
            StatusText.Text = "✓ Complete";
        }
        catch (Exception ex)
        {
            ShowMessage($"❌ Error: {ex.Message}");
        }
        finally
        {
            _isProcessing = false;
            LoadingIndicator.Visibility = Visibility.Collapsed;
            StopButton.Visibility = Visibility.Collapsed;
            
            // Re-enable buttons only if we have configured API keys
            var hasConfiguredModels = (await _aiService.GetAvailableModelsAsync()).Count > 0;
            TestPromptButton.IsEnabled = hasConfiguredModels;
            AnalyzeButton.IsEnabled = hasConfiguredModels;
            GetSuggestionsButton.IsEnabled = hasConfiguredModels;
        }
    }
}
