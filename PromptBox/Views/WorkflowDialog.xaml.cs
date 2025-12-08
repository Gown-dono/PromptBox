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
using System.Windows.Media;

namespace PromptBox.Views;

/// <summary>
/// Dialog for running multi-step prompt workflows
/// </summary>
public partial class WorkflowDialog : Window
{
    private readonly IWorkflowService _workflowService;
    private readonly IAIService _aiService;
    private readonly ISecureStorageService _secureStorage;
    
    private List<Workflow> _workflows = new();
    private List<AIModel> _availableModels = new();
    private Workflow? _selectedWorkflow;
    private CancellationTokenSource? _cancellationTokenSource;
    private List<WorkflowStepResult> _currentResults = new();
    private List<Border> _stepIndicatorBorders = new();
    
    public string? ResultPrompt { get; private set; }

    public WorkflowDialog(
        IWorkflowService workflowService,
        IAIService aiService,
        ISecureStorageService secureStorage)
    {
        _workflowService = workflowService;
        _aiService = aiService;
        _secureStorage = secureStorage;
        
        InitializeComponent();
        Loaded += WorkflowDialog_Loaded;
    }

    private async void WorkflowDialog_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadWorkflowsAsync();
        await LoadModelsAsync();
    }

    private async Task LoadWorkflowsAsync()
    {
        try
        {
            _workflows = await _workflowService.GetAllWorkflowsAsync();
            WorkflowList.ItemsSource = _workflows;
            
            if (_workflows.Any())
                WorkflowList.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading workflows: {ex.Message}";
        }
    }

    private async Task LoadModelsAsync()
    {
        try
        {
            _availableModels = await _aiService.GetAvailableModelsAsync();
            
            if (_availableModels.Any())
            {
                ModelSelector.ItemsSource = _availableModels;
                ModelSelector.DisplayMemberPath = "Name";
                ModelSelector.SelectedValuePath = "Id";
                ModelSelector.SelectedIndex = 0;
            }
            else
            {
                ModelSelector.ItemsSource = new[] { "No API keys configured" };
                ModelSelector.SelectedIndex = 0;
                RunButton.IsEnabled = false;
                StatusText.Text = "Please configure API keys in Settings to run workflows";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading models: {ex.Message}";
        }
    }

    private void WorkflowList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedWorkflow = WorkflowList.SelectedItem as Workflow;
        
        if (_selectedWorkflow != null)
        {
            UpdateStepIndicators();
            ResultTabs.Items.Clear();
            _currentResults.Clear();
            WorkflowProgress.Value = 0;
            ProgressText.Text = "";
            StatusText.Text = $"Selected: {_selectedWorkflow.Name} ({_selectedWorkflow.Steps.Count} steps)";
            
            // Enable/disable edit and delete buttons based on whether it's a custom workflow
            var isCustom = !_selectedWorkflow.IsBuiltIn;
            EditWorkflowButton.IsEnabled = isCustom;
            DeleteWorkflowButton.IsEnabled = isCustom;
        }
        else
        {
            EditWorkflowButton.IsEnabled = false;
            DeleteWorkflowButton.IsEnabled = false;
        }
    }

    private void UpdateStepIndicators()
    {
        StepIndicators.Items.Clear();
        _stepIndicatorBorders.Clear();
        
        if (_selectedWorkflow == null) return;

        foreach (var step in _selectedWorkflow.Steps)
        {
            var border = new Border
            {
                Background = (Brush)FindResource("MaterialDesignDivider"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 8, 8)
            };

            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            var icon = new PackIcon
            {
                Kind = PackIconKind.CircleOutline,
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                Foreground = (Brush)FindResource("MaterialDesignBodyLight")
            };
            
            var text = new TextBlock
            {
                Text = $"{step.Order}. {step.Name}",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("MaterialDesignBody")
            };

            stackPanel.Children.Add(icon);
            stackPanel.Children.Add(text);
            border.Child = stackPanel;
            border.Tag = icon;
            
            _stepIndicatorBorders.Add(border);
            StepIndicators.Items.Add(border);
        }
    }

    private void UpdateStepIndicator(int stepIndex, StepStatus status)
    {
        if (stepIndex < 0 || stepIndex >= _stepIndicatorBorders.Count) return;

        var border = _stepIndicatorBorders[stepIndex];
        var icon = border.Tag as PackIcon;
        
        if (icon == null) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (status)
            {
                case StepStatus.Running:
                    icon.Kind = PackIconKind.ProgressClock;
                    icon.Foreground = (Brush)FindResource("PrimaryHueMidBrush");
                    border.Background = (Brush)FindResource("PrimaryHueLightBrush");
                    break;
                case StepStatus.Completed:
                    icon.Kind = PackIconKind.CheckCircle;
                    icon.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    border.Background = new SolidColorBrush(Color.FromArgb(30, 76, 175, 80));
                    break;
                case StepStatus.Failed:
                    icon.Kind = PackIconKind.AlertCircle;
                    icon.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    border.Background = new SolidColorBrush(Color.FromArgb(30, 244, 67, 54));
                    break;
                default:
                    icon.Kind = PackIconKind.CircleOutline;
                    icon.Foreground = (Brush)FindResource("MaterialDesignBodyLight");
                    border.Background = (Brush)FindResource("MaterialDesignDivider");
                    break;
            }
        });
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWorkflow == null)
        {
            StatusText.Text = "Please select a workflow";
            return;
        }

        if (string.IsNullOrWhiteSpace(InputTextBox.Text))
        {
            StatusText.Text = "Please enter input for the workflow";
            return;
        }

        var selectedModel = ModelSelector.SelectedItem as AIModel;
        if (selectedModel == null)
        {
            StatusText.Text = "Please select an AI model";
            return;
        }

        await RunWorkflowAsync(selectedModel);
    }

    private async Task RunWorkflowAsync(AIModel model)
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _currentResults.Clear();
        ResultTabs.Items.Clear();
        
        // Reset step indicators
        for (int i = 0; i < _stepIndicatorBorders.Count; i++)
            UpdateStepIndicator(i, StepStatus.Pending);

        SetRunningState(true);

        var settings = new AIGenerationSettings
        {
            ModelId = model.Id,
            Temperature = TemperatureSlider.Value,
            MaxOutputTokens = (int)MaxTokensSlider.Value
        };

        try
        {
            var totalSteps = _selectedWorkflow!.Steps.Count;
            var completedSteps = 0;

            await foreach (var result in _workflowService.ExecuteWorkflowAsync(
                _selectedWorkflow,
                InputTextBox.Text,
                settings,
                _cancellationTokenSource.Token))
            {
                _currentResults.Add(result);
                
                // Update UI
                Application.Current.Dispatcher.Invoke(() =>
                {
                    completedSteps++;
                    WorkflowProgress.Value = (completedSteps * 100.0) / totalSteps;
                    ProgressText.Text = $"Step {completedSteps}/{totalSteps}: {result.StepName}";
                    
                    UpdateStepIndicator(completedSteps - 1, result.Success ? StepStatus.Completed : StepStatus.Failed);
                    AddResultTab(result);
                    
                    if (!result.Success)
                    {
                        StatusText.Text = $"Workflow failed at step {completedSteps}: {result.Error}";
                    }
                });
            }

            if (_currentResults.All(r => r.Success))
            {
                StatusText.Text = $"Workflow completed successfully! ({_currentResults.Sum(r => r.Duration.TotalSeconds):F1}s total)";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Workflow cancelled";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            SetRunningState(false);
        }
    }

    private void AddResultTab(WorkflowStepResult result)
    {
        var tabItem = new TabItem
        {
            Header = $"{result.StepOrder}. {result.StepName}"
        };

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var textBox = new TextBox
        {
            Text = result.Success ? result.Output : $"Error: {result.Error}",
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Padding = new Thickness(12),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = (Brush)FindResource("MaterialDesignBody")
        };

        scrollViewer.Content = textBox;
        tabItem.Content = scrollViewer;
        
        ResultTabs.Items.Add(tabItem);
        ResultTabs.SelectedItem = tabItem;
    }

    private void SetRunningState(bool isRunning)
    {
        RunButton.IsEnabled = !isRunning;
        CancelButton.IsEnabled = isRunning;
        WorkflowList.IsEnabled = !isRunning;
        InputTextBox.IsEnabled = !isRunning;
        ModelSelector.IsEnabled = !isRunning;
        TemperatureSlider.IsEnabled = !isRunning;
        MaxTokensSlider.IsEnabled = !isRunning;
        
        if (isRunning)
        {
            StatusText.Text = "Running workflow...";
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        StatusText.Text = "Cancelling...";
    }

    private void CopyResultButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_currentResults.Any())
        {
            StatusText.Text = "No results to copy";
            return;
        }

        var allResults = string.Join("\n\n" + new string('=', 50) + "\n\n",
            _currentResults.Select(r => $"## {r.StepName}\n\n{r.Output}"));
        
        Clipboard.SetText(allResults);
        StatusText.Text = "All results copied to clipboard";
    }

    private void UseResultButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_currentResults.Any())
        {
            StatusText.Text = "No results to use";
            return;
        }

        // Use the last successful result
        var lastResult = _currentResults.LastOrDefault(r => r.Success);
        if (lastResult != null)
        {
            ResultPrompt = lastResult.Output;
            DialogResult = true;
            Close();
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AISettingsDialog(_secureStorage, _aiService)
        {
            Owner = this
        };
        
        if (dialog.ShowDialog() == true)
        {
            _ = LoadModelsAsync();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        DialogResult = false;
        Close();
    }

    private void TemperatureSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TemperatureValue != null)
        {
            TemperatureValue.Text = e.NewValue.ToString("F1");
        }
    }

    private void MaxTokensSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MaxTokensValue != null)
        {
            MaxTokensValue.Text = ((int)e.NewValue).ToString();
        }
    }

    private void CreateWorkflow_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WorkflowEditorDialog(_workflowService)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _ = LoadWorkflowsAsync();
            StatusText.Text = "Workflow created successfully!";
        }
    }

    private void EditWorkflow_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWorkflow == null || _selectedWorkflow.IsBuiltIn)
        {
            StatusText.Text = "Cannot edit built-in workflows";
            return;
        }

        var dialog = new WorkflowEditorDialog(_workflowService, _selectedWorkflow)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _ = LoadWorkflowsAsync();
            StatusText.Text = "Workflow updated successfully!";
        }
    }

    private async void DeleteWorkflow_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWorkflow == null || _selectedWorkflow.IsBuiltIn)
        {
            StatusText.Text = "Cannot delete built-in workflows";
            return;
        }

        var titleText = new TextBlock
        {
            Text = $"Delete '{_selectedWorkflow.Name}'?",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var messageText = new TextBlock
        {
            Text = "This action cannot be undone.",
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

        var deleteButton = new Button
        {
            Content = "Delete",
            Style = Application.Current.FindResource("MaterialDesignRaisedButton") as Style,
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = true,
            Background = new SolidColorBrush(Color.FromRgb(211, 47, 47))
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(deleteButton);

        var mainPanel = new StackPanel
        {
            Margin = new Thickness(16)
        };
        mainPanel.Children.Add(titleText);
        mainPanel.Children.Add(messageText);
        mainPanel.Children.Add(buttonPanel);

        var result = await DialogHost.Show(mainPanel, "WorkflowDialog");

        if (result is bool confirmed && confirmed)
        {
            try
            {
                await _workflowService.DeleteWorkflowAsync(_selectedWorkflow.Id);
                await LoadWorkflowsAsync();
                StatusText.Text = "Workflow deleted successfully!";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error deleting workflow: {ex.Message}";
            }
        }
    }

    private enum StepStatus
    {
        Pending,
        Running,
        Completed,
        Failed
    }
}
