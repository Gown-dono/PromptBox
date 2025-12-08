using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using PromptBox.Models;
using PromptBox.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
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
    private ObservableCollection<ContextItem> _contextItems = new();
    
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
        
        _contextItems.CollectionChanged += (s, e) => UpdateContextBadge();
        
        Loaded += async (s, e) => await InitializeAsync();
    }
    
    private void UpdateContextBadge()
    {
        ContextButtonBadge.Text = _contextItems.Count > 0 ? $"({_contextItems.Count})" : "";
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
            
            // Build prompt with context injection
            var fullPrompt = BuildPromptWithContext();
            
            try
            {
                await foreach (var chunk in _aiService.GenerateStreamAsync(fullPrompt, settings, _cancellationTokenSource.Token))
                {
                    _currentResponse += chunk;
                    await Dispatcher.InvokeAsync(() => ResponseViewer.Markdown = _currentResponse);
                }
                
                var contextInfo = _contextItems.Count > 0 ? $" (with {_contextItems.Count} context items)" : "";
                TokenCount.Text = $"Response length: {_currentResponse.Length} chars{contextInfo}";
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

    #region Context Injection
    
    private async void OpenContextDialog_Click(object sender, RoutedEventArgs e)
    {
        // Create the context items list for the dialog
        var contextListBox = new ListBox
        {
            MaxHeight = 200,
            MinHeight = 80,
            Background = Brushes.Transparent,
            ItemsSource = _contextItems
        };
        
        contextListBox.ItemTemplate = CreateContextItemTemplate();
        
        // Note input area
        var noteInput = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 60,
            FontFamily = new FontFamily("Consolas"),
            Foreground = (Brush)FindResource("MaterialDesignBody"),
            Margin = new Thickness(0, 0, 8, 0)
        };
        HintAssist.SetHint(noteInput, "Type a note here...");
        
        var addNoteFromInputBtn = new Button 
        { 
            Content = "Add", 
            Style = (Style)FindResource("MaterialDesignRaisedButton"),
            VerticalAlignment = VerticalAlignment.Bottom,
            Width = 60
        };
        
        addNoteFromInputBtn.Click += (s, args) =>
        {
            if (!string.IsNullOrWhiteSpace(noteInput.Text))
            {
                // Safely compute display name
                var lines = noteInput.Text.Split('\n');
                var firstLine = lines.Length > 0 ? lines[0].Trim() : string.Empty;
                string displayName;
                if (string.IsNullOrWhiteSpace(firstLine))
                {
                    displayName = "Note";
                }
                else if (firstLine.Length > 30)
                {
                    displayName = firstLine.Substring(0, 30) + "...";
                }
                else
                {
                    displayName = firstLine;
                }
                
                var item = new ContextItem
                {
                    Type = ContextItemType.Note,
                    DisplayName = displayName,
                    FullPath = "User note",
                    Content = noteInput.Text,
                    SizeText = $"{noteInput.Text.Length} chars"
                };
                _contextItems.Add(item);
                noteInput.Text = "";
                contextListBox.Items.Refresh();
                ShowMessage("✓ Added note");
            }
        };
        
        var notePanel = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        notePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        notePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(noteInput, 0);
        Grid.SetColumn(addNoteFromInputBtn, 1);
        notePanel.Children.Add(noteInput);
        notePanel.Children.Add(addNoteFromInputBtn);
        
        // Action buttons
        var addFilesBtn = new Button { Content = "Add Files", Style = (Style)FindResource("MaterialDesignRaisedButton"), Margin = new Thickness(0, 0, 4, 4) };
        var addFolderBtn = new Button { Content = "Add Folder", Style = (Style)FindResource("MaterialDesignRaisedButton"), Margin = new Thickness(0, 0, 4, 4) };
        var addClipboardBtn = new Button { Content = "From Clipboard", Style = (Style)FindResource("MaterialDesignRaisedButton"), Margin = new Thickness(0, 0, 4, 4) };
        var clearAllBtn = new Button { Content = "Clear All", Style = (Style)FindResource("MaterialDesignOutlinedButton"), Foreground = new SolidColorBrush(Color.FromRgb(211, 47, 47)), BorderBrush = new SolidColorBrush(Color.FromRgb(211, 47, 47)), Margin = new Thickness(0, 0, 4, 4) };
        
        addFilesBtn.Click += (s, args) => { AddFiles_Click(s, args); contextListBox.Items.Refresh(); };
        addFolderBtn.Click += (s, args) => { AddFolder_Click(s, args); };
        addClipboardBtn.Click += (s, args) => { AddClipboard_Click(s, args); contextListBox.Items.Refresh(); };
        clearAllBtn.Click += (s, args) => { ClearContext_Click(s, args); contextListBox.Items.Refresh(); };
        
        var buttonsPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        buttonsPanel.Children.Add(addFilesBtn);
        buttonsPanel.Children.Add(addFolderBtn);
        buttonsPanel.Children.Add(addClipboardBtn);
        buttonsPanel.Children.Add(clearAllBtn);
        
        var closeButton = new Button
        {
            Content = "Close",
            Style = (Style)FindResource("MaterialDesignRaisedButton"),
            Command = DialogHost.CloseDialogCommand,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        
        var mainPanel = new StackPanel { Margin = new Thickness(16), MinWidth = 500 };
        mainPanel.Children.Add(new TextBlock
        {
            Text = "Context Injection",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
            Foreground = (Brush)FindResource("MaterialDesignBody")
        });
        mainPanel.Children.Add(new TextBlock
        {
            Text = "Add files, folders, clipboard content, or notes to include as context in your prompt.",
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 16),
            Foreground = (Brush)FindResource("MaterialDesignBodyLight"),
            TextWrapping = TextWrapping.Wrap
        });
        mainPanel.Children.Add(buttonsPanel);
        mainPanel.Children.Add(new TextBlock
        {
            Text = "Add Note:",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8),
            Foreground = (Brush)FindResource("MaterialDesignBody")
        });
        mainPanel.Children.Add(notePanel);
        mainPanel.Children.Add(new TextBlock
        {
            Text = "Context Items:",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8),
            Foreground = (Brush)FindResource("MaterialDesignBody")
        });
        mainPanel.Children.Add(new Border
        {
            BorderBrush = (Brush)FindResource("MaterialDesignDivider"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = contextListBox,
            MinHeight = 80
        });
        mainPanel.Children.Add(closeButton);
        
        await DialogHost.Show(mainPanel, "PromptBuilderDialog");
        UpdateContextBadge();
    }
    
    private DataTemplate CreateContextItemTemplate()
    {
        var template = new DataTemplate();
        
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.BackgroundProperty, FindResource("PrimaryHueLightBrush"));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        factory.SetValue(Border.PaddingProperty, new Thickness(8, 4, 8, 4));
        factory.SetValue(Border.MarginProperty, new Thickness(0, 2, 0, 2));
        
        var gridFactory = new FrameworkElementFactory(typeof(Grid));
        
        var col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
        col1.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
        var col2 = new FrameworkElementFactory(typeof(ColumnDefinition));
        col2.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
        var col3 = new FrameworkElementFactory(typeof(ColumnDefinition));
        col3.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
        var col4 = new FrameworkElementFactory(typeof(ColumnDefinition));
        col4.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
        
        gridFactory.AppendChild(col1);
        gridFactory.AppendChild(col2);
        gridFactory.AppendChild(col3);
        gridFactory.AppendChild(col4);
        
        var iconFactory = new FrameworkElementFactory(typeof(PackIcon));
        iconFactory.SetBinding(PackIcon.KindProperty, new System.Windows.Data.Binding("Icon"));
        iconFactory.SetValue(Grid.ColumnProperty, 0);
        iconFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        iconFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
        
        var nameFactory = new FrameworkElementFactory(typeof(TextBlock));
        nameFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("DisplayName"));
        nameFactory.SetValue(Grid.ColumnProperty, 1);
        nameFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        nameFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        
        var sizeFactory = new FrameworkElementFactory(typeof(TextBlock));
        sizeFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("SizeText"));
        sizeFactory.SetValue(Grid.ColumnProperty, 2);
        sizeFactory.SetValue(TextBlock.FontSizeProperty, 10.0);
        sizeFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        sizeFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 8, 0));
        sizeFactory.SetValue(UIElement.OpacityProperty, 0.7);
        
        var removeFactory = new FrameworkElementFactory(typeof(Button));
        removeFactory.SetValue(Grid.ColumnProperty, 3);
        removeFactory.SetValue(Button.StyleProperty, FindResource("MaterialDesignIconButton"));
        removeFactory.SetValue(FrameworkElement.WidthProperty, 24.0);
        removeFactory.SetValue(FrameworkElement.HeightProperty, 24.0);
        removeFactory.SetValue(Control.PaddingProperty, new Thickness(0));
        removeFactory.SetBinding(FrameworkElement.TagProperty, new System.Windows.Data.Binding());
        removeFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(RemoveContextItem_Click));
        
        var removeIconFactory = new FrameworkElementFactory(typeof(PackIcon));
        removeIconFactory.SetValue(PackIcon.KindProperty, PackIconKind.Close);
        removeIconFactory.SetValue(FrameworkElement.WidthProperty, 14.0);
        removeIconFactory.SetValue(FrameworkElement.HeightProperty, 14.0);
        removeFactory.AppendChild(removeIconFactory);
        
        gridFactory.AppendChild(iconFactory);
        gridFactory.AppendChild(nameFactory);
        gridFactory.AppendChild(sizeFactory);
        gridFactory.AppendChild(removeFactory);
        
        factory.AppendChild(gridFactory);
        template.VisualTree = factory;
        
        return template;
    }
    
    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "All files (*.*)|*.*|Code files (*.cs;*.js;*.ts;*.py;*.java;*.cpp;*.h)|*.cs;*.js;*.ts;*.py;*.java;*.cpp;*.h|Text files (*.txt;*.md;*.json;*.xml;*.yaml)|*.txt;*.md;*.json;*.xml;*.yaml"
        };
        
        if (dialog.ShowDialog() == true)
        {
            foreach (var filePath in dialog.FileNames)
            {
                AddFileToContext(filePath);
            }
        }
    }
    
    private void AddFileToContext(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var content = File.ReadAllText(filePath);
            
            var item = new ContextItem
            {
                Type = ContextItemType.File,
                DisplayName = fileInfo.Name,
                FullPath = filePath,
                Content = content,
                SizeText = FormatFileSize(fileInfo.Length)
            };
            
            _contextItems.Add(item);
            ShowMessage($"✓ Added: {fileInfo.Name}");
        }
        catch (Exception ex)
        {
            ShowMessage($"❌ Error reading file: {ex.Message}");
        }
    }
    
    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        // Use OpenFileDialog to select any file, then extract its directory
        var fileDialog = new OpenFileDialog
        {
            Title = "Select any file in the folder you want to add",
            CheckFileExists = true
        };
        
        if (fileDialog.ShowDialog() == true)
        {
            var folderPath = Path.GetDirectoryName(fileDialog.FileName);
            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                AddFolderToContext(folderPath);
            }
            else
            {
                ShowMessage("❌ Could not determine folder path");
            }
        }
    }
    
    private void AddFolderToContext(string folderPath)
    {
        try
        {
            var dirInfo = new DirectoryInfo(folderPath);
            var sb = new StringBuilder();
            sb.AppendLine($"📁 Folder: {dirInfo.Name}");
            sb.AppendLine($"Path: {folderPath}");
            sb.AppendLine();
            sb.AppendLine("Files:");
            
            var files = dirInfo.GetFiles("*", SearchOption.AllDirectories)
                .Where(f => !f.FullName.Contains("\\bin\\") && 
                           !f.FullName.Contains("\\obj\\") &&
                           !f.FullName.Contains("\\.git\\") &&
                           !f.FullName.Contains("\\node_modules\\"))
                .Take(500)
                .ToList();
            
            foreach (var file in files)
            {
                var relativePath = file.FullName.Replace(folderPath, "").TrimStart('\\');
                sb.AppendLine($"  - {relativePath} ({FormatFileSize(file.Length)})");
            }
            
            if (files.Count >= 500)
            {
                sb.AppendLine($"  ... and more files (limited to 500)");
            }
            
            var item = new ContextItem
            {
                Type = ContextItemType.Folder,
                DisplayName = dirInfo.Name,
                FullPath = folderPath,
                Content = sb.ToString(),
                SizeText = $"{files.Count} files"
            };
            
            _contextItems.Add(item);
            ShowMessage($"✓ Added folder: {dirInfo.Name} ({files.Count} files)");
        }
        catch (Exception ex)
        {
            ShowMessage($"❌ Error reading folder: {ex.Message}");
        }
    }
    
    private void AddClipboard_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var item = new ContextItem
                    {
                        Type = ContextItemType.Clipboard,
                        DisplayName = "Clipboard content",
                        FullPath = "From clipboard",
                        Content = text,
                        SizeText = $"{text.Length} chars"
                    };
                    
                    _contextItems.Add(item);
                    ShowMessage("✓ Added clipboard content");
                }
                else
                {
                    ShowMessage("⚠️ Clipboard is empty");
                }
            }
            else
            {
                ShowMessage("⚠️ No text in clipboard");
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"❌ Error reading clipboard: {ex.Message}");
        }
    }
    
    private void RemoveContextItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ContextItem item)
        {
            _contextItems.Remove(item);
            ShowMessage($"✓ Removed: {item.DisplayName}");
        }
    }
    
    private void ClearContext_Click(object sender, RoutedEventArgs e)
    {
        _contextItems.Clear();
        ShowMessage("✓ Cleared all context items");
    }
    
    private string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
    
    private string BuildPromptWithContext()
    {
        if (_contextItems.Count == 0)
            return PromptInput.Text;
        
        var sb = new StringBuilder();
        sb.AppendLine(PromptInput.Text);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("## Context");
        sb.AppendLine();
        
        foreach (var item in _contextItems)
        {
            switch (item.Type)
            {
                case ContextItemType.File:
                    sb.AppendLine($"### File: {item.DisplayName}");
                    sb.AppendLine("```");
                    sb.AppendLine(item.Content);
                    sb.AppendLine("```");
                    break;
                    
                case ContextItemType.Folder:
                    sb.AppendLine($"### {item.Content}");
                    break;
                    
                case ContextItemType.Clipboard:
                    sb.AppendLine("### Clipboard Content:");
                    sb.AppendLine("```");
                    sb.AppendLine(item.Content);
                    sb.AppendLine("```");
                    break;
                    
                case ContextItemType.Note:
                    sb.AppendLine("### Note:");
                    sb.AppendLine(item.Content);
                    break;
            }
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    #endregion
    
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
