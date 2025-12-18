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
using System.Windows.Shapes;

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
    
    // Visual execution view elements
    private readonly Dictionary<string, Border> _visualNodes = new();
    private readonly List<Line> _visualConnectors = new();
    
    // Cache results per workflow to persist when switching
    private readonly Dictionary<string, List<WorkflowStepResult>> _workflowResultsCache = new();
    
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
            
            // Restore cached results for this workflow if available
            var workflowKey = _selectedWorkflow.Id.ToString();
            if (_workflowResultsCache.TryGetValue(workflowKey, out var cachedResults) && cachedResults.Any())
            {
                _currentResults = cachedResults;
                foreach (var result in _currentResults)
                {
                    AddResultTab(result);
                }
                WorkflowProgress.Value = 100;
                ProgressText.Text = $"Completed {_currentResults.Count} steps";
            }
            else
            {
                _currentResults = new List<WorkflowStepResult>();
                WorkflowProgress.Value = 0;
                ProgressText.Text = "";
            }
            
            StatusText.Text = $"Selected: {_selectedWorkflow.Name} ({_selectedWorkflow.Steps.Count} steps)";
            
            // Enable/disable edit and delete buttons based on whether it's a custom workflow
            var isCustom = !_selectedWorkflow.IsBuiltIn;
            EditWorkflowButton.IsEnabled = isCustom;
            DeleteWorkflowButton.IsEnabled = isCustom;
            
            // Show visual execution view only for custom workflows
            VisualExecutionExpander.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            EditWorkflowButton.IsEnabled = false;
            DeleteWorkflowButton.IsEnabled = false;
            VisualExecutionExpander.Visibility = Visibility.Collapsed;
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
        
        // Update visual execution view
        UpdateVisualExecutionView();
    }
    
    private void UpdateVisualExecutionView()
    {
        ExecutionCanvas.Children.Clear();
        _visualNodes.Clear();
        _visualConnectors.Clear();
        
        if (_selectedWorkflow == null || _selectedWorkflow.Steps.Count == 0) return;
        
        const int nodeWidth = 120;
        const int nodeHeight = 50;
        const int horizontalSpacing = 150;
        const int verticalSpacing = 70;
        const int startX = 20;
        const int startY = 20;
        
        // Calculate positions using BFS from start step
        var positions = new Dictionary<string, Point>();
        var startStep = _selectedWorkflow.GetStartStep() ?? _selectedWorkflow.Steps.First();
        
        var visited = new HashSet<string>();
        var queue = new Queue<(string stepId, int level, int position)>();
        queue.Enqueue((startStep.StepId, 0, 0));
        var levelCounts = new Dictionary<int, int>();
        
        while (queue.Count > 0)
        {
            var (stepId, level, pos) = queue.Dequeue();
            if (visited.Contains(stepId)) continue;
            visited.Add(stepId);
            
            if (!levelCounts.ContainsKey(level))
                levelCounts[level] = 0;
            
            var actualPos = levelCounts[level]++;
            positions[stepId] = new Point(startX + level * horizontalSpacing, startY + actualPos * verticalSpacing);
            
            var step = _selectedWorkflow.GetStepById(stepId);
            if (step == null) continue;
            
            if (!string.IsNullOrEmpty(step.NextStepId))
                queue.Enqueue((step.NextStepId, level + 1, 0));
            
            foreach (var branch in step.ConditionalBranches)
            {
                if (!string.IsNullOrEmpty(branch.NextStepId))
                    queue.Enqueue((branch.NextStepId, level + 1, 0));
            }
        }
        
        // Add unvisited steps
        foreach (var step in _selectedWorkflow.Steps.Where(s => !visited.Contains(s.StepId)))
        {
            var level = positions.Count > 0 ? (int)(positions.Values.Max(p => p.X) / horizontalSpacing) + 1 : 0;
            positions[step.StepId] = new Point(startX + level * horizontalSpacing, startY);
        }
        
        // Draw connectors first (so they appear behind nodes)
        foreach (var step in _selectedWorkflow.Steps)
        {
            if (!positions.TryGetValue(step.StepId, out var fromPos)) continue;
            
            // Standard connection
            if (!string.IsNullOrEmpty(step.NextStepId) && positions.TryGetValue(step.NextStepId, out var toPos))
            {
                DrawVisualConnector(fromPos, toPos, nodeWidth, nodeHeight, Colors.Gray);
            }
            
            // Conditional branches
            foreach (var branch in step.ConditionalBranches)
            {
                if (!string.IsNullOrEmpty(branch.NextStepId) && positions.TryGetValue(branch.NextStepId, out var branchToPos))
                {
                    DrawVisualConnector(fromPos, branchToPos, nodeWidth, nodeHeight, Color.FromRgb(33, 150, 243));
                }
            }
        }
        
        // Draw nodes
        foreach (var step in _selectedWorkflow.Steps)
        {
            if (!positions.TryGetValue(step.StepId, out var pos)) continue;
            
            var nodeColor = step.StepType switch
            {
                WorkflowStepType.Conditional => Color.FromRgb(33, 150, 243), // Blue
                WorkflowStepType.Loop => Color.FromRgb(76, 175, 80), // Green
                WorkflowStepType.Parallel => Color.FromRgb(255, 152, 0), // Orange
                _ => Color.FromRgb(103, 58, 183) // Purple (Standard)
            };
            
            var node = CreateVisualNode(step, nodeWidth, nodeHeight, nodeColor);
            Canvas.SetLeft(node, pos.X);
            Canvas.SetTop(node, pos.Y);
            ExecutionCanvas.Children.Add(node);
            _visualNodes[step.StepId] = node;
        }
        
        // Update canvas size
        var maxX = positions.Values.Max(p => p.X) + nodeWidth + 40;
        var maxY = positions.Values.Max(p => p.Y) + nodeHeight + 20;
        ExecutionCanvas.Width = Math.Max(maxX, 400);
        ExecutionCanvas.Height = Math.Max(maxY, 100);
    }
    
    private Border CreateVisualNode(WorkflowStep step, int width, int height, Color headerColor)
    {
        var border = new Border
        {
            Width = width,
            Height = height,
            Background = new SolidColorBrush(Colors.White),
            BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 4,
                ShadowDepth = 1,
                Opacity = 0.2
            }
        };
        
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        
        // Header
        var header = new Border
        {
            Background = new SolidColorBrush(headerColor),
            CornerRadius = new CornerRadius(4, 4, 0, 0)
        };
        
        var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 2, 4, 2) };
        
        var numberBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4, 0, 4, 0),
            Margin = new Thickness(0, 0, 4, 0)
        };
        numberBadge.Child = new TextBlock
        {
            Text = (step.Order + 1).ToString(),
            FontSize = 9,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };
        
        var nameText = new TextBlock
        {
            Text = step.Name.Length > 12 ? step.Name[..12] + "..." : step.Name,
            FontSize = 9,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        
        headerStack.Children.Add(numberBadge);
        headerStack.Children.Add(nameText);
        header.Child = headerStack;
        Grid.SetRow(header, 0);
        grid.Children.Add(header);
        
        // Body - Status indicator
        var body = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        
        var statusIcon = new PackIcon
        {
            Kind = PackIconKind.CircleOutline,
            Width = 14,
            Height = 14,
            Foreground = new SolidColorBrush(Colors.Gray)
        };
        body.Children.Add(statusIcon);
        
        var statusText = new TextBlock
        {
            Text = "Ready",
            FontSize = 9,
            Foreground = new SolidColorBrush(Colors.Gray),
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        body.Children.Add(statusText);
        
        Grid.SetRow(body, 1);
        grid.Children.Add(body);
        
        border.Child = grid;
        border.Tag = new { StatusIcon = statusIcon, StatusText = statusText };
        
        return border;
    }
    
    private void DrawVisualConnector(Point from, Point to, int nodeWidth, int nodeHeight, Color color)
    {
        var line = new Line
        {
            X1 = from.X + nodeWidth,
            Y1 = from.Y + nodeHeight / 2,
            X2 = to.X,
            Y2 = to.Y + nodeHeight / 2,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        
        ExecutionCanvas.Children.Add(line);
        _visualConnectors.Add(line);
        
        // Arrow head
        var arrowSize = 6;
        var arrow = new Polygon
        {
            Points = new PointCollection
            {
                new Point(to.X, to.Y + nodeHeight / 2),
                new Point(to.X - arrowSize, to.Y + nodeHeight / 2 - arrowSize),
                new Point(to.X - arrowSize, to.Y + nodeHeight / 2 + arrowSize)
            },
            Fill = new SolidColorBrush(color)
        };
        ExecutionCanvas.Children.Add(arrow);
    }
    
    private void UpdateVisualNodeStatus(string stepId, StepStatus status)
    {
        if (!_visualNodes.TryGetValue(stepId, out var node)) return;
        
        Application.Current.Dispatcher.Invoke(() =>
        {
            var tag = node.Tag as dynamic;
            if (tag == null) return;
            
            var statusIcon = tag.StatusIcon as PackIcon;
            var statusText = tag.StatusText as TextBlock;
            
            if (statusIcon == null || statusText == null) return;
            
            switch (status)
            {
                case StepStatus.Running:
                    statusIcon.Kind = PackIconKind.ProgressClock;
                    statusIcon.Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                    statusText.Text = "Running...";
                    statusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                    node.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                    node.BorderThickness = new Thickness(2);
                    break;
                case StepStatus.Completed:
                    statusIcon.Kind = PackIconKind.CheckCircle;
                    statusIcon.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    statusText.Text = "Done";
                    statusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    node.BorderBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    node.BorderThickness = new Thickness(2);
                    break;
                case StepStatus.Failed:
                    statusIcon.Kind = PackIconKind.AlertCircle;
                    statusIcon.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    statusText.Text = "Failed";
                    statusText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    node.BorderBrush = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    node.BorderThickness = new Thickness(2);
                    break;
                default:
                    statusIcon.Kind = PackIconKind.CircleOutline;
                    statusIcon.Foreground = new SolidColorBrush(Colors.Gray);
                    statusText.Text = "Ready";
                    statusText.Foreground = new SolidColorBrush(Colors.Gray);
                    node.BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200));
                    node.BorderThickness = new Thickness(1);
                    break;
            }
        });
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
        
        // Reset step indicators and visual nodes
        for (int i = 0; i < _stepIndicatorBorders.Count; i++)
            UpdateStepIndicator(i, StepStatus.Pending);
        
        foreach (var stepId in _visualNodes.Keys)
            UpdateVisualNodeStatus(stepId, StepStatus.Pending);

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
            var stepIdToIndex = _selectedWorkflow.Steps.ToDictionary(s => s.StepId, s => s.Order);

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
                    
                    // Update step indicator
                    if (stepIdToIndex.TryGetValue(result.StepId, out var stepIndex))
                    {
                        UpdateStepIndicator(stepIndex, result.Success ? StepStatus.Completed : StepStatus.Failed);
                    }
                    
                    // Update visual node
                    UpdateVisualNodeStatus(result.StepId, result.Success ? StepStatus.Completed : StepStatus.Failed);
                    
                    // Mark next step as running if there is one
                    var currentStep = _selectedWorkflow.GetStepById(result.StepId);
                    if (currentStep != null && result.Success)
                    {
                        var nextStepId = currentStep.NextStepId;
                        if (!string.IsNullOrEmpty(nextStepId))
                        {
                            UpdateVisualNodeStatus(nextStepId, StepStatus.Running);
                        }
                    }
                    
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
            
            // Cache results for this workflow
            var workflowKey = _selectedWorkflow.Id.ToString();
            _workflowResultsCache[workflowKey] = new List<WorkflowStepResult>(_currentResults);
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

        var content = result.Success ? result.Output : $"Error: {result.Error}";
        
        // Create context menu with View Full Screen option
        var contextMenu = new ContextMenu();
        var viewFullScreenItem = new MenuItem
        {
            Header = "View Full Screen",
            Icon = new PackIcon { Kind = PackIconKind.Fullscreen }
        };
        viewFullScreenItem.Click += (s, e) =>
        {
            var dialog = new FullScreenPromptDialog($"{result.StepOrder}. {result.StepName}", content)
            {
                Owner = this
            };
            dialog.ShowDialog();
        };
        
        var copyItem = new MenuItem
        {
            Header = "Copy",
            Icon = new PackIcon { Kind = PackIconKind.ContentCopy }
        };
        copyItem.Click += (s, e) => Clipboard.SetText(content);
        
        contextMenu.Items.Add(viewFullScreenItem);
        contextMenu.Items.Add(copyItem);
        
        var textBox = new TextBox
        {
            Text = content,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Padding = new Thickness(12),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = (Brush)FindResource("MaterialDesignBody"),
            ContextMenu = contextMenu
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
