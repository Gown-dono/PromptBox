using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using PromptBox.Models;
using PromptBox.Services;
using PromptBox.ViewModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PromptBox.Views;

/// <summary>
/// Outcome of batch processing
/// </summary>
public enum BatchOutcome
{
    None,
    Completed,
    Cancelled,
    Failed
}

/// <summary>
/// Wrapper class for prompt selection in the UI
/// </summary>
public class SelectablePrompt
{
    public Prompt Prompt { get; set; } = null!;
    public bool IsSelected { get; set; }
    public string Title => Prompt.Title;
    public string Category => Prompt.Category;
}

/// <summary>
/// Wrapper class for model selection in the UI
/// </summary>
public class SelectableModel
{
    public AIModel Model { get; set; } = null!;
    public bool IsSelected { get; set; }
    public string Name => Model.Name;
    public string Provider => Model.Provider;
    public string Description => Model.Description;
}

public partial class BatchProcessingDialog : Window
{
    private readonly IBatchProcessingService _batchService;
    private readonly IDatabaseService _databaseService;
    private readonly IAIService _aiService;
    private readonly IExportService _exportService;
    
    private List<SelectablePrompt> _allPrompts = new();
    private List<SelectablePrompt> _filteredPrompts = new();
    private List<SelectableModel> _allModels = new();
    private BatchJob? _currentJob;
    private CancellationTokenSource? _cancellationTokenSource;
    private ObservableCollection<BatchResultViewModel> _results = new();
    private bool _isRunning;
    private int _successCount;
    private int _failedCount;
    private int _totalTokens;
    private BatchOutcome _batchOutcome = BatchOutcome.None;
    
    // UI throttling for large batches
    private readonly ConcurrentQueue<BatchResultViewModel> _pendingResults = new();
    private DispatcherTimer? _uiUpdateTimer;
    private const int UI_UPDATE_INTERVAL_MS = 100;
    private const int MAX_RESULTS_PER_UPDATE = 10;
    
    /// <summary>
    /// Indicates the final outcome of the batch processing
    /// </summary>
    public BatchOutcome Outcome => _batchOutcome;
    
    /// <summary>
    /// Number of results produced
    /// </summary>
    public int ResultCount => _results.Count;

    public BatchProcessingDialog(
        IBatchProcessingService batchService,
        IDatabaseService databaseService,
        IAIService aiService,
        IExportService exportService)
    {
        InitializeComponent();
        _batchService = batchService;
        _databaseService = databaseService;
        _aiService = aiService;
        _exportService = exportService;
        
        ResultsDataGrid.ItemsSource = _results;
        
        TemperatureSlider.ValueChanged += (s, e) => TemperatureValue.Text = TemperatureSlider.Value.ToString("F1");
        MaxTokensSlider.ValueChanged += (s, e) => MaxTokensValue.Text = ((int)MaxTokensSlider.Value).ToString();
        
        // Initialize UI update timer for throttled batch updates
        _uiUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(UI_UPDATE_INTERVAL_MS)
        };
        _uiUpdateTimer.Tick += UiUpdateTimer_Tick;
        
        Loaded += async (s, e) => await LoadDataAsync();
    }

    private void UiUpdateTimer_Tick(object? sender, EventArgs e)
    {
        FlushPendingResults();
    }

    private void FlushPendingResults()
    {
        var count = 0;
        while (count < MAX_RESULTS_PER_UPDATE && _pendingResults.TryDequeue(out var result))
        {
            _results.Add(result);
            count++;
        }
    }

    private async Task LoadDataAsync()
    {
        // Load prompts
        var prompts = await _databaseService.GetAllPromptsAsync();
        _allPrompts = prompts.Select(p => new SelectablePrompt { Prompt = p }).ToList();
        _filteredPrompts = _allPrompts.ToList();
        PromptsListBox.ItemsSource = _filteredPrompts;
        
        // Load available models
        var models = await _aiService.GetAvailableModelsAsync();
        _allModels = models.Select(m => new SelectableModel { Model = m }).ToList();
        ModelsListBox.ItemsSource = _allModels;
        
        if (_allPrompts.Count == 0)
        {
            StatusText.Text = "No prompts available. Create some prompts first.";
        }
        else if (_allModels.Count == 0)
        {
            StatusText.Text = "No AI models configured. Add API keys in Settings.";
        }
        else
        {
            StatusText.Text = $"Ready. {_allPrompts.Count} prompts, {_allModels.Count} models available.";
        }
    }

    private void PromptSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = PromptSearchBox.Text.ToLowerInvariant();
        
        if (string.IsNullOrWhiteSpace(searchText))
        {
            _filteredPrompts = _allPrompts.ToList();
        }
        else
        {
            _filteredPrompts = _allPrompts
                .Where(p => p.Title.ToLowerInvariant().Contains(searchText) ||
                           p.Category.ToLowerInvariant().Contains(searchText))
                .ToList();
        }
        
        PromptsListBox.ItemsSource = _filteredPrompts;
    }

    private void SelectAllPrompts_Click(object sender, RoutedEventArgs e)
    {
        foreach (var prompt in _filteredPrompts)
        {
            prompt.IsSelected = true;
        }
        PromptsListBox.Items.Refresh();
    }

    private void ClearAllPrompts_Click(object sender, RoutedEventArgs e)
    {
        foreach (var prompt in _allPrompts)
        {
            prompt.IsSelected = false;
        }
        PromptsListBox.Items.Refresh();
    }

    private async void StartBatch_Click(object sender, RoutedEventArgs e)
    {
        var selectedPrompts = _allPrompts.Where(p => p.IsSelected).Select(p => p.Prompt).ToList();
        var selectedModels = _allModels.Where(m => m.IsSelected).ToList();
        
        if (selectedPrompts.Count == 0)
        {
            StatusText.Text = "⚠️ Please select at least one prompt";
            return;
        }
        
        if (selectedModels.Count == 0)
        {
            StatusText.Text = "⚠️ Please select at least one model";
            return;
        }
        
        var batchName = string.IsNullOrWhiteSpace(BatchNameBox.Text) 
            ? $"Batch {DateTime.Now:yyyy-MM-dd HH:mm}" 
            : BatchNameBox.Text.Trim();
        
        var batchDescription = BatchDescriptionBox.Text?.Trim() ?? string.Empty;
        
        var settings = new AIGenerationSettings
        {
            Temperature = TemperatureSlider.Value,
            MaxOutputTokens = (int)MaxTokensSlider.Value
        };
        
        // Create batch job
        _currentJob = await _batchService.CreateBatchJobAsync(
            batchName,
            batchDescription,
            selectedPrompts.Select(p => p.Id).ToList(),
            selectedModels.Select(m => m.Model.Id).ToList(),
            settings);
        
        // Reset UI
        _results.Clear();
        _successCount = 0;
        _failedCount = 0;
        _totalTokens = 0;
        UpdateStatistics();
        
        // Update button states
        _isRunning = true;
        StartButton.IsEnabled = false;
        PauseButton.IsEnabled = true;
        CancelButton.IsEnabled = true;
        ExportButton.IsEnabled = false;
        
        _cancellationTokenSource = new CancellationTokenSource();
        
        try
        {
            var totalOperations = selectedPrompts.Count * selectedModels.Count;
            TotalCountText.Text = totalOperations.ToString();
            
            // Start UI update timer for throttled results display
            _uiUpdateTimer?.Start();
            
            await foreach (var progress in _batchService.ExecuteBatchAsync(
                _currentJob, selectedPrompts, _cancellationTokenSource.Token))
            {
                // Compute statistics on background thread
                var percentComplete = progress.PercentComplete;
                var promptTitle = progress.PromptTitle;
                var modelName = progress.ModelName;
                BatchResultViewModel? resultVm = null;
                var wasSuccess = false;
                var tokensUsed = 0;
                
                if (progress.LastResult != null)
                {
                    resultVm = new BatchResultViewModel(progress.LastResult);
                    wasSuccess = progress.LastResult.Success;
                    tokensUsed = progress.LastResult.TokensUsed;
                }
                
                // Update progress bar immediately (lightweight)
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    OverallProgressBar.Value = percentComplete;
                    ProgressText.Text = $"{(int)percentComplete}% complete";
                    CurrentOperationText.Text = $"Processing: {promptTitle} with {modelName}";
                    
                    if (resultVm != null)
                    {
                        // Queue result for batched UI update instead of immediate add
                        _pendingResults.Enqueue(resultVm);
                        
                        if (wasSuccess)
                            _successCount++;
                        else
                            _failedCount++;
                        
                        _totalTokens += tokensUsed;
                        UpdateStatistics();
                    }
                }, DispatcherPriority.Background);
            }
            
            // Flush any remaining results
            _uiUpdateTimer?.Stop();
            while (_pendingResults.TryDequeue(out var remaining))
            {
                _results.Add(remaining);
            }
            
            _batchOutcome = BatchOutcome.Completed;
            StatusText.Text = $"✓ Batch completed: {_successCount} successful, {_failedCount} failed";
        }
        catch (OperationCanceledException)
        {
            _batchOutcome = BatchOutcome.Cancelled;
            StatusText.Text = "Batch cancelled";
        }
        catch (Exception ex)
        {
            _batchOutcome = BatchOutcome.Failed;
            StatusText.Text = $"❌ Error: {ex.Message}";
        }
        finally
        {
            _uiUpdateTimer?.Stop();
            
            // Flush any remaining pending results
            while (_pendingResults.TryDequeue(out var remaining))
            {
                _results.Add(remaining);
            }
            
            _isRunning = false;
            StartButton.IsEnabled = true;
            PauseButton.IsEnabled = false;
            PauseButton.Content = "Pause";
            CancelButton.IsEnabled = false;
            ExportButton.IsEnabled = _results.Count > 0;
            CurrentOperationText.Text = "";
            
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private async void PauseBatch_Click(object sender, RoutedEventArgs e)
    {
        if (_currentJob == null) return;
        
        if (_batchService.IsJobPaused(_currentJob.Id))
        {
            await _batchService.ResumeBatchJobAsync(_currentJob.Id);
            PauseButton.Content = "Pause";
            StatusText.Text = "Resumed...";
        }
        else
        {
            await _batchService.PauseBatchJobAsync(_currentJob.Id);
            PauseButton.Content = "Resume";
            StatusText.Text = "Paused";
        }
    }

    private async void CancelBatch_Click(object sender, RoutedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        
        // Also notify the service to clean up job status and pause state
        if (_currentJob != null)
        {
            await _batchService.CancelBatchJobAsync(_currentJob.Id);
        }
    }

    private async void ExportResults_Click(object sender, RoutedEventArgs e)
    {
        if (_results.Count == 0) return;
        
        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|JSON files (*.json)|*.json",
            FileName = $"batch_results_{DateTime.Now:yyyyMMdd_HHmmss}"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var results = _results.Select(r => r.Result).ToList();
                
                if (dialog.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    await _exportService.ExportBatchResultsAsCsvAsync(results, dialog.FileName);
                }
                else
                {
                    await _exportService.ExportBatchResultsAsJsonAsync(results, dialog.FileName);
                }
                
                StatusText.Text = $"✓ Results exported to {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ Export failed: {ex.Message}";
            }
        }
    }

    private async void Close_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            var result = await ShowConfirmationAsync("A batch is currently running. Cancel and close?", "Confirm Close");
            if (!result) return;
            
            _cancellationTokenSource?.Cancel();
            _batchOutcome = BatchOutcome.Cancelled;
        }
        
        // Return true only if batch completed successfully with results
        DialogResult = _results.Count > 0 && _batchOutcome == BatchOutcome.Completed;
        Close();
    }

    private void UpdateStatistics()
    {
        SuccessCountText.Text = _successCount.ToString();
        FailedCountText.Text = _failedCount.ToString();
        TokensCountText.Text = _totalTokens.ToString();
    }

    private void ResultsDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ResultsDataGrid.SelectedItem is BatchResultViewModel result)
        {
            ShowFullResponse(result);
        }
    }

    private void CopyResponse_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsDataGrid.SelectedItem is BatchResultViewModel result)
        {
            Clipboard.SetText(result.FullResponse);
            StatusText.Text = "✓ Response copied to clipboard";
        }
    }

    private void ViewFullResponse_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsDataGrid.SelectedItem is BatchResultViewModel result)
        {
            ShowFullResponse(result);
        }
    }

    private async void RetryFailed_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            StatusText.Text = "⚠️ Cannot retry while batch is running";
            return;
        }

        // Get selected failed result or all failed results
        var failedResults = new List<BatchResultViewModel>();
        
        if (ResultsDataGrid.SelectedItem is BatchResultViewModel selectedResult && selectedResult.IsFailed)
        {
            failedResults.Add(selectedResult);
        }
        else
        {
            // Retry all failed results
            failedResults = _results.Where(r => r.IsFailed).ToList();
        }

        if (failedResults.Count == 0)
        {
            StatusText.Text = "⚠️ No failed results to retry";
            return;
        }

        // Get prompts and models for retry
        var promptIds = failedResults.Select(r => r.Result.PromptId).Distinct().ToList();
        var modelIds = failedResults.Select(r => r.Result.ModelId).Distinct().ToList();
        
        // Load prompts from database
        var allPrompts = await _databaseService.GetAllPromptsAsync();
        var promptsToRetry = allPrompts.Where(p => promptIds.Contains(p.Id)).ToList();
        
        if (promptsToRetry.Count == 0)
        {
            StatusText.Text = "⚠️ Could not find prompts to retry";
            return;
        }

        // Get available models
        var availableModels = await _aiService.GetAvailableModelsAsync();
        var modelsToRetry = availableModels.Where(m => modelIds.Contains(m.Id)).ToList();

        if (modelsToRetry.Count == 0)
        {
            StatusText.Text = "⚠️ Could not find models to retry";
            return;
        }

        StatusText.Text = $"Retrying {failedResults.Count} failed execution(s)...";
        
        // Update UI state
        _isRunning = true;
        StartButton.IsEnabled = false;
        PauseButton.IsEnabled = true;
        CancelButton.IsEnabled = true;
        
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var settings = new AIGenerationSettings
            {
                Temperature = TemperatureSlider.Value,
                MaxOutputTokens = (int)MaxTokensSlider.Value
            };

            // Retry each failed result
            foreach (var failedResult in failedResults)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                    break;

                var prompt = promptsToRetry.FirstOrDefault(p => p.Id == failedResult.Result.PromptId);
                var model = modelsToRetry.FirstOrDefault(m => m.Id == failedResult.Result.ModelId);
                
                if (prompt == null || model == null)
                    continue;

                CurrentOperationText.Text = $"Retrying: {prompt.Title} with {model.Name}";

                var retrySettings = new AIGenerationSettings
                {
                    ModelId = model.Id,
                    Temperature = settings.Temperature,
                    MaxOutputTokens = settings.MaxOutputTokens
                };

                var newResult = new BatchResult
                {
                    BatchJobId = failedResult.Result.BatchJobId,
                    PromptId = prompt.Id,
                    PromptTitle = prompt.Title,
                    ModelId = model.Id,
                    ModelName = model.Name
                };

                try
                {
                    var response = await _aiService.GenerateAsync(prompt.Content, retrySettings);
                    
                    newResult.Success = response.Success;
                    newResult.Response = response.Content ?? string.Empty;
                    newResult.Error = response.Error;
                    newResult.TokensUsed = response.TokensUsed;
                    newResult.Duration = response.Duration;
                }
                catch (Exception ex)
                {
                    newResult.Success = false;
                    newResult.Error = ex.Message;
                    newResult.Duration = TimeSpan.Zero;
                }

                newResult.ExecutedAt = DateTime.Now;
                await _databaseService.SaveBatchResultAsync(newResult);

                // Update UI - replace failed result with new result
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var index = _results.IndexOf(failedResult);
                    if (index >= 0)
                    {
                        _results[index] = new BatchResultViewModel(newResult);
                    }
                    else
                    {
                        _results.Add(new BatchResultViewModel(newResult));
                    }

                    // Update statistics
                    if (newResult.Success)
                    {
                        _successCount++;
                        _failedCount--;
                    }
                    _totalTokens += newResult.TokensUsed;
                    UpdateStatistics();
                });
            }

            var successfulRetries = failedResults.Count(r => 
                _results.FirstOrDefault(res => res.Result.PromptId == r.Result.PromptId && 
                                               res.Result.ModelId == r.Result.ModelId)?.IsSuccess == true);
            StatusText.Text = $"✓ Retry completed: {successfulRetries}/{failedResults.Count} succeeded";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Retry cancelled";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"❌ Retry error: {ex.Message}";
        }
        finally
        {
            _isRunning = false;
            StartButton.IsEnabled = true;
            PauseButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            CurrentOperationText.Text = "";
            
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private async void ShowFullResponse(BatchResultViewModel result)
    {
        var scrollViewer = new ScrollViewer
        {
            MaxHeight = 400,
            MaxWidth = 700,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var responseText = new TextBox
        {
            Text = string.IsNullOrEmpty(result.FullResponse) ? result.Error ?? "No response" : result.FullResponse,
            TextWrapping = TextWrapping.Wrap,
            IsReadOnly = true,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 12,
            Padding = new Thickness(8)
        };
        scrollViewer.Content = responseText;

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
            Text = $"{result.PromptTitle} - {result.ModelName}",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        });
        mainPanel.Children.Add(new TextBlock
        {
            Text = $"Status: {(result.IsSuccess ? "Success" : "Failed")} | Tokens: {result.TokensUsed} | Duration: {result.DurationFormatted}",
            FontSize = 12,
            Opacity = 0.7,
            Margin = new Thickness(0, 0, 0, 12)
        });
        mainPanel.Children.Add(new Border
        {
            Background = (System.Windows.Media.Brush)FindResource("MaterialDesignCardBackground"),
            CornerRadius = new CornerRadius(4),
            BorderBrush = (System.Windows.Media.Brush)FindResource("MaterialDesignDivider"),
            BorderThickness = new Thickness(1),
            Child = scrollViewer
        });
        mainPanel.Children.Add(closeButton);

        await DialogHost.Show(mainPanel, "BatchDialog");
    }

    private async Task<bool> ShowConfirmationAsync(string message, string title)
    {
        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
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
            Margin = new Thickness(0, 0, 0, 12)
        });
        mainPanel.Children.Add(messageText);
        mainPanel.Children.Add(buttonPanel);

        var result = await DialogHost.Show(mainPanel, "BatchDialog");
        return result is bool confirmed && confirmed;
    }
}
