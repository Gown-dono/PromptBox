using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using PromptBox.Models;
using PromptBox.Services;
using PromptBox.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PromptBox.Views;

/// <summary>
/// Wrapper class for model selection in testing UI
/// </summary>
public class SelectableTestModel
{
    public AIModel Model { get; set; } = null!;
    public bool IsSelected { get; set; }
    public string Name => Model.Name;
    public string Provider => Model.Provider;
}

/// <summary>
/// Outcome data from prompt testing dialog
/// </summary>
public record PromptTestingOutcome(int TestsExecuted, int PassedTests, int FailedTests);

public partial class PromptTestingDialog : Window
{
    private readonly IPromptTestingService _testingService;
    private readonly IDatabaseService _databaseService;
    private readonly IAIService _aiService;
    private readonly IExportService _exportService;

    private List<PromptTest> _allTests = new();
    private List<PromptTest> _filteredTests = new();
    private List<SelectableTestModel> _allModels = new();
    private List<Prompt> _allPrompts = new();
    private PromptTest? _selectedTest;
    private List<PromptVariation> _variations = new();
    private TestComparison? _currentComparison;

    private ObservableCollection<TestResultViewModel> _results = new();
    private ObservableCollection<ComparisonResultViewModel> _comparisonResults = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isRunning;

    private int _totalTests;
    private int _passedTests;
    private int _failedTests;
    private int _totalTokens;
    private double _totalQuality;

    /// <summary>
    /// Gets the outcome of the testing session
    /// </summary>
    public PromptTestingOutcome Outcome => new(_totalTests, _passedTests, _failedTests);

    public PromptTestingDialog(
        IPromptTestingService testingService,
        IDatabaseService databaseService,
        IAIService aiService,
        IExportService exportService)
    {
        InitializeComponent();
        _testingService = testingService;
        _databaseService = databaseService;
        _aiService = aiService;
        _exportService = exportService;

        ResultsDataGrid.ItemsSource = _results;
        ComparisonDataGrid.ItemsSource = _comparisonResults;
        VariationsListBox.ItemsSource = _variations;

        MinQualitySlider.ValueChanged += (s, e) => MinQualityValue.Text = ((int)MinQualitySlider.Value).ToString();

        Loaded += async (s, e) => await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _allTests = await _databaseService.GetAllPromptTestsAsync();
        _filteredTests = _allTests.ToList();
        TestsListBox.ItemsSource = _filteredTests;

        _allPrompts = await _databaseService.GetAllPromptsAsync();

        var models = await _aiService.GetAvailableModelsAsync();
        _allModels = models.Select(m => new SelectableTestModel { Model = m }).ToList();
        ModelsListBox.ItemsSource = _allModels;

        StatusText.Text = $"Ready. {_allTests.Count} tests, {_allModels.Count} models available.";
    }


    private void TestSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = TestSearchBox.Text.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(searchText))
        {
            _filteredTests = _allTests.ToList();
        }
        else
        {
            _filteredTests = _allTests
                .Where(t => t.Name.ToLowerInvariant().Contains(searchText) ||
                           t.Description.ToLowerInvariant().Contains(searchText))
                .ToList();
        }

        TestsListBox.ItemsSource = _filteredTests;
    }

    private void TestsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedTest = TestsListBox.SelectedItem as PromptTest;
        if (_selectedTest != null)
        {
            TestCasesDataGrid.ItemsSource = _selectedTest.TestCases;
            LoadCriteriaFromTest(_selectedTest);
        }
    }

    private void LoadCriteriaFromTest(PromptTest test)
    {
        var criteria = test.EvaluationCriteria;
        CheckKeywordsBox.IsChecked = criteria.CheckKeywords;
        CheckPatternsBox.IsChecked = criteria.CheckPatterns;
        CheckQualityScoreBox.IsChecked = criteria.CheckQualityScore;
        MinQualitySlider.Value = criteria.MinimumQualityScore;
        CheckTokenUsageBox.IsChecked = criteria.CheckTokenUsage;
        MaxTokensBox.Text = criteria.MaxTokensAllowed.ToString();
        CheckResponseTimeBox.IsChecked = criteria.CheckResponseTime;
        MaxResponseTimeBox.Text = criteria.MaxResponseTimeSeconds.ToString();
    }

    private TestEvaluationCriteria GetCriteriaFromUI()
    {
        return new TestEvaluationCriteria
        {
            CheckKeywords = CheckKeywordsBox.IsChecked == true,
            CheckPatterns = CheckPatternsBox.IsChecked == true,
            CheckQualityScore = CheckQualityScoreBox.IsChecked == true,
            MinimumQualityScore = MinQualitySlider.Value,
            CheckTokenUsage = CheckTokenUsageBox.IsChecked == true,
            MaxTokensAllowed = int.TryParse(MaxTokensBox.Text, out var maxTokens) ? maxTokens : 2000,
            CheckResponseTime = CheckResponseTimeBox.IsChecked == true,
            MaxResponseTimeSeconds = double.TryParse(MaxResponseTimeBox.Text, out var maxTime) ? maxTime : 30
        };
    }

    private async void NewTest_Click(object sender, RoutedEventArgs e)
    {
        var dialog = await ShowTestEditorDialogAsync(null);
        if (dialog != null)
        {
            _allTests = await _databaseService.GetAllPromptTestsAsync();
            _filteredTests = _allTests.ToList();
            TestsListBox.ItemsSource = _filteredTests;
            StatusText.Text = "✓ Test created";
        }
    }

    private async void EditTest_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTest == null)
        {
            StatusText.Text = "⚠️ Please select a test to edit";
            return;
        }

        // Update criteria from UI
        _selectedTest.EvaluationCriteria = GetCriteriaFromUI();
        _selectedTest.UpdatedDate = DateTime.Now;
        await _databaseService.SavePromptTestAsync(_selectedTest);
        StatusText.Text = "✓ Test updated";
    }

    private async void DeleteTest_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTest == null)
        {
            StatusText.Text = "⚠️ Please select a test to delete";
            return;
        }

        var confirmed = await ShowConfirmationAsync($"Delete test '{_selectedTest.Name}'?", "Confirm Delete");
        if (confirmed)
        {
            await _databaseService.DeletePromptTestAsync(_selectedTest.Id);
            _allTests = await _databaseService.GetAllPromptTestsAsync();
            _filteredTests = _allTests.ToList();
            TestsListBox.ItemsSource = _filteredTests;
            _selectedTest = null;
            TestCasesDataGrid.ItemsSource = null;
            StatusText.Text = "✓ Test deleted";
        }
    }

    private async void AddTestCase_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTest == null)
        {
            StatusText.Text = "⚠️ Please select or create a test first";
            return;
        }

        var testCase = await ShowTestCaseEditorAsync(null);
        if (testCase != null)
        {
            _selectedTest.TestCases.Add(testCase);
            await _databaseService.SavePromptTestAsync(_selectedTest);
            TestCasesDataGrid.Items.Refresh();
        }
    }

    private async void EditTestCase_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTest == null || TestCasesDataGrid.SelectedItem is not TestCase selectedCase)
        {
            StatusText.Text = "⚠️ Please select a test case to edit";
            return;
        }

        var editedCase = await ShowTestCaseEditorAsync(selectedCase);
        if (editedCase != null)
        {
            var index = _selectedTest.TestCases.IndexOf(selectedCase);
            if (index >= 0)
            {
                _selectedTest.TestCases[index] = editedCase;
                await _databaseService.SavePromptTestAsync(_selectedTest);
                TestCasesDataGrid.Items.Refresh();
            }
        }
    }

    private async void RemoveTestCase_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTest == null || TestCasesDataGrid.SelectedItem is not TestCase selectedCase)
        {
            StatusText.Text = "⚠️ Please select a test case to remove";
            return;
        }

        _selectedTest.TestCases.Remove(selectedCase);
        await _databaseService.SavePromptTestAsync(_selectedTest);
        TestCasesDataGrid.Items.Refresh();
    }


    private async void RunTests_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTest == null)
        {
            StatusText.Text = "⚠️ Please select a test to run";
            return;
        }

        var selectedModels = _allModels.Where(m => m.IsSelected).Select(m => m.Model.Id).ToList();
        if (selectedModels.Count == 0)
        {
            StatusText.Text = "⚠️ Please select at least one model";
            return;
        }

        if (_selectedTest.TestCases.Count == 0)
        {
            StatusText.Text = "⚠️ Test has no test cases";
            return;
        }

        // Update criteria before running
        _selectedTest.EvaluationCriteria = GetCriteriaFromUI();

        // Reset UI
        _results.Clear();
        _totalTests = 0;
        _passedTests = 0;
        _failedTests = 0;
        _totalTokens = 0;
        _totalQuality = 0;
        UpdateStatistics();

        _isRunning = true;
        RunButton.IsEnabled = false;
        PauseButton.IsEnabled = true;
        CancelButton.IsEnabled = true;
        ExportButton.IsEnabled = false;

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var totalOperations = _selectedTest.TestCases.Count * selectedModels.Count;
            TotalCountText.Text = totalOperations.ToString();

            await foreach (var progress in _testingService.ExecuteTestAsync(
                _selectedTest, selectedModels, _cancellationTokenSource.Token))
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    OverallProgressBar.Value = progress.PercentComplete;
                    ProgressText.Text = $"{(int)progress.PercentComplete}%";
                    CurrentOperationText.Text = $"Testing: {progress.LastResult?.TestCaseName} with {progress.CurrentModel}";

                    if (progress.LastResult != null)
                    {
                        _results.Add(new TestResultViewModel(progress.LastResult));
                        _totalTests++;

                        if (progress.LastResult.Success)
                            _passedTests++;
                        else
                            _failedTests++;

                        _totalTokens += progress.LastResult.TokensUsed;
                        _totalQuality += progress.LastResult.QualityScore;
                        UpdateStatistics();
                    }
                });
            }

            StatusText.Text = $"✓ Tests completed: {_passedTests} passed, {_failedTests} failed";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Tests cancelled";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"❌ Error: {ex.Message}";
        }
        finally
        {
            _isRunning = false;
            RunButton.IsEnabled = true;
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
        if (!_isRunning || _selectedTest == null) return;

        if (_testingService.IsTestPaused(_selectedTest.Id))
        {
            await _testingService.ResumeTestAsync(_selectedTest.Id);
            PauseButton.Content = "Pause";
            StatusText.Text = "Resumed...";
        }
        else
        {
            await _testingService.PauseTestAsync(_selectedTest.Id);
            PauseButton.Content = "Resume";
            StatusText.Text = "Paused";
        }
    }

    private void CancelBatch_Click(object sender, RoutedEventArgs e)
    {
        if (!_isRunning) return;
        _cancellationTokenSource?.Cancel();
    }

    private void UpdateStatistics()
    {
        TotalCountText.Text = _totalTests.ToString();
        PassedCountText.Text = _passedTests.ToString();
        FailedCountText.Text = _failedTests.ToString();
        PassRateText.Text = _totalTests > 0 ? $"{(double)_passedTests / _totalTests * 100:F1}%" : "0%";
        AvgQualityText.Text = _totalTests > 0 ? $"{_totalQuality / _totalTests:F1}" : "0";
        TotalTokensText.Text = _totalTokens.ToString("N0");

        // Update Analytics tab
        UpdateAnalytics();
    }

    private void UpdateAnalytics()
    {
        if (_results.Count == 0)
        {
            AnalyticsSummaryText.Text = "No test results yet. Run tests to see analytics.";
            AnalyticsPassRateText.Text = "";
            AnalyticsQualityText.Text = "";
            AnalyticsTokensText.Text = "";
            AnalyticsModelBreakdownPanel.Children.Clear();
            AnalyticsTestCaseBreakdownPanel.Children.Clear();
            return;
        }

        var results = _results.Select(r => r.Result).ToList();
        var passRate = _totalTests > 0 ? (double)_passedTests / _totalTests * 100 : 0;
        var avgQuality = _totalTests > 0 ? _totalQuality / _totalTests : 0;

        // Summary
        AnalyticsSummaryText.Text = $"Total Executions: {_totalTests}\n" +
                                    $"Passed: {_passedTests}\n" +
                                    $"Failed: {_failedTests}";

        AnalyticsPassRateText.Text = $"Pass Rate: {passRate:F1}%\n" +
                                     $"Average Quality: {avgQuality:F1}\n" +
                                     $"Total Tokens: {_totalTokens:N0}";

        // Quality score distribution
        var qualityScores = results.Select(r => r.QualityScore).ToList();
        var minQuality = qualityScores.Min();
        var maxQuality = qualityScores.Max();
        var avgClarity = results.Average(r => r.ClarityScore);
        var avgSpecificity = results.Average(r => r.SpecificityScore);
        var avgEffectiveness = results.Average(r => r.EffectivenessScore);

        AnalyticsQualityText.Text = $"Quality Score Range: {minQuality:F1} - {maxQuality:F1}\n" +
                                    $"Average Quality: {avgQuality:F1}\n" +
                                    $"Average Clarity: {avgClarity:F1}\n" +
                                    $"Average Specificity: {avgSpecificity:F1}\n" +
                                    $"Average Effectiveness: {avgEffectiveness:F1}";

        // Per-model breakdown
        AnalyticsModelBreakdownPanel.Children.Clear();
        var modelGroups = results.GroupBy(r => r.ModelName).OrderBy(g => g.Key);
        foreach (var group in modelGroups)
        {
            var modelPassed = group.Count(r => r.Success);
            var modelTotal = group.Count();
            var modelPassRate = (double)modelPassed / modelTotal * 100;
            var modelAvgQuality = group.Average(r => r.QualityScore);
            var modelAvgTokens = group.Average(r => r.TokensUsed);

            var modelText = new TextBlock
            {
                Text = $"🤖 {group.Key}: {modelPassed}/{modelTotal} passed ({modelPassRate:F0}%) | " +
                       $"Avg Quality: {modelAvgQuality:F1} | Avg Tokens: {modelAvgTokens:F0}",
                Margin = new Thickness(0, 2, 0, 2),
                FontSize = 13
            };
            AnalyticsModelBreakdownPanel.Children.Add(modelText);
        }

        // Per-test-case breakdown
        AnalyticsTestCaseBreakdownPanel.Children.Clear();
        var testCaseGroups = results.GroupBy(r => r.TestCaseName).OrderBy(g => g.Key);
        foreach (var group in testCaseGroups)
        {
            var casePassed = group.Count(r => r.Success);
            var caseTotal = group.Count();
            var casePassRate = (double)casePassed / caseTotal * 100;
            var caseAvgQuality = group.Average(r => r.QualityScore);

            var caseText = new TextBlock
            {
                Text = $"📝 {group.Key}: {casePassed}/{caseTotal} passed ({casePassRate:F0}%) | " +
                       $"Avg Quality: {caseAvgQuality:F1}",
                Margin = new Thickness(0, 2, 0, 2),
                FontSize = 13
            };
            AnalyticsTestCaseBreakdownPanel.Children.Add(caseText);
        }

        // Token usage
        var totalTokens = results.Sum(r => r.TokensUsed);
        var avgTokens = results.Average(r => r.TokensUsed);
        var minTokens = results.Min(r => r.TokensUsed);
        var maxTokens = results.Max(r => r.TokensUsed);
        var avgDuration = TimeSpan.FromTicks((long)results.Average(r => r.Duration.Ticks));

        AnalyticsTokensText.Text = $"Total Tokens Used: {totalTokens:N0}\n" +
                                   $"Average per Test: {avgTokens:F0}\n" +
                                   $"Range: {minTokens:N0} - {maxTokens:N0}\n" +
                                   $"Average Duration: {(avgDuration.TotalSeconds < 1 ? $"{avgDuration.TotalMilliseconds:F0}ms" : $"{avgDuration.TotalSeconds:F1}s")}";
    }

    private async void AddVariation_Click(object sender, RoutedEventArgs e)
    {
        var variation = await ShowVariationEditorAsync(null);
        if (variation != null)
        {
            _variations.Add(variation);
            VariationsListBox.Items.Refresh();
        }
    }

    private async void AddVariationFromPrompt_Click(object sender, RoutedEventArgs e)
    {
        var prompts = await _databaseService.GetAllPromptsAsync();
        if (!prompts.Any())
        {
            StatusText.Text = "⚠️ No prompts available. Create some prompts first.";
            return;
        }

        var listBox = new ListBox
        {
            ItemsSource = prompts,
            DisplayMemberPath = "Title",
            MaxHeight = 300,
            MinWidth = 350
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text = "Select a Prompt",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        });
        panel.Children.Add(listBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        buttonPanel.Children.Add(new Button
        {
            Content = "Cancel",
            Style = FindResource("MaterialDesignFlatButton") as Style,
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = false,
            Margin = new Thickness(0, 0, 8, 0)
        });
        buttonPanel.Children.Add(new Button
        {
            Content = "Add",
            Style = FindResource("MaterialDesignRaisedButton") as Style,
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = true
        });
        panel.Children.Add(buttonPanel);

        var result = await DialogHost.Show(panel, "TestingDialog");
        if (result is true && listBox.SelectedItem is Prompt selectedPrompt)
        {
            var variation = new PromptVariation
            {
                Name = selectedPrompt.Title,
                Description = $"From prompt: {selectedPrompt.Title}",
                Content = selectedPrompt.Content
            };
            _variations.Add(variation);
            VariationsListBox.Items.Refresh();
            StatusText.Text = $"✓ Added variation from '{selectedPrompt.Title}'";
        }
    }

    private void RemoveVariation_Click(object sender, RoutedEventArgs e)
    {
        if (VariationsListBox.SelectedItem is PromptVariation selected)
        {
            _variations.Remove(selected);
            VariationsListBox.Items.Refresh();
        }
    }

    private async void RunComparison_Click(object sender, RoutedEventArgs e)
    {
        if (_variations.Count < 2)
        {
            StatusText.Text = "⚠️ Add at least 2 variations to compare";
            return;
        }

        var selectedModels = _allModels.Where(m => m.IsSelected).Select(m => m.Model.Id).ToList();
        if (selectedModels.Count == 0)
        {
            StatusText.Text = "⚠️ Please select at least one model";
            return;
        }

        var testInput = ComparisonInputBox.Text?.Trim() ?? string.Empty;

        _comparisonResults.Clear();
        StatusText.Text = "Running comparison...";

        // Update button states for comparison run
        _isRunning = true;
        RunButton.IsEnabled = false;
        CancelButton.IsEnabled = true;

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            _currentComparison = await _testingService.CreateComparisonAsync(
                $"Comparison {DateTime.Now:yyyy-MM-dd HH:mm}",
                _variations.ToList(),
                testInput,
                selectedModels);

            var results = await _testingService.ExecuteComparisonAsync(_currentComparison, _cancellationTokenSource.Token);

            foreach (var result in results.OrderBy(r => r.Ranking))
            {
                _comparisonResults.Add(new ComparisonResultViewModel(result));
            }

            var winner = results.OrderBy(r => r.Ranking).FirstOrDefault();
            if (winner != null)
            {
                WinnerAnalysisText.Text = $"🏆 Winner: {winner.VariationName} with {winner.ModelName} (Quality: {winner.QualityScore:F1})";
            }

            StatusText.Text = $"✓ Comparison completed with {results.Count} results";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Comparison cancelled";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"❌ Error: {ex.Message}";
        }
        finally
        {
            _isRunning = false;
            RunButton.IsEnabled = true;
            CancelButton.IsEnabled = false;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }


    private void ResultsDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ResultsDataGrid.SelectedItem is TestResultViewModel result)
        {
            ShowFullOutputDialog(result);
        }
    }

    private void ViewFullOutput_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsDataGrid.SelectedItem is TestResultViewModel result)
        {
            ShowFullOutputDialog(result);
        }
    }

    private void CopyOutput_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsDataGrid.SelectedItem is TestResultViewModel result)
        {
            Clipboard.SetText(result.FullOutput);
            StatusText.Text = "✓ Output copied to clipboard";
        }
    }

    private async void ViewFailureDetails_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsDataGrid.SelectedItem is TestResultViewModel result && result.IsFailed)
        {
            await ShowMessageDialogAsync("Failure Details", result.FailureReason ?? "No details available");
        }
    }

    private async void RerunTest_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsDataGrid.SelectedItem is not TestResultViewModel result || _selectedTest == null)
        {
            StatusText.Text = "⚠️ Please select a result to rerun";
            return;
        }

        var testCase = _selectedTest.TestCases.FirstOrDefault(tc => tc.Name == result.TestCaseName);
        if (testCase == null)
        {
            StatusText.Text = "⚠️ Test case not found";
            return;
        }

        StatusText.Text = $"Rerunning {testCase.Name}...";

        try
        {
            var newResult = await _testingService.ExecuteSingleTestCaseAsync(
                testCase, _selectedTest.PromptContent, result.Result.ModelId, _selectedTest.EvaluationCriteria);
            newResult.TestId = _selectedTest.Id;
            newResult.ModelName = result.ModelName;

            await _databaseService.SaveTestResultAsync(newResult);

            var index = _results.IndexOf(result);
            if (index >= 0)
            {
                _results[index] = new TestResultViewModel(newResult);
            }

            StatusText.Text = $"✓ Rerun completed: {(newResult.Success ? "Passed" : "Failed")}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"❌ Rerun failed: {ex.Message}";
        }
    }

    private async void ExportResults_Click(object sender, RoutedEventArgs e)
    {
        if (_results.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|JSON files (*.json)|*.json",
            FileName = $"test_results_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var results = _results.Select(r => r.Result).ToList();

                if (dialog.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    await _exportService.ExportTestResultsAsCsvAsync(results, dialog.FileName);
                }
                else
                {
                    await _exportService.ExportTestResultsAsJsonAsync(results, dialog.FileName);
                }

                StatusText.Text = $"✓ Results exported to {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ Export failed: {ex.Message}";
            }
        }
    }

    private async void ExportComparison_Click(object sender, RoutedEventArgs e)
    {
        if (_currentComparison == null || _comparisonResults.Count == 0)
        {
            StatusText.Text = "⚠️ No comparison results to export";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Markdown files (*.md)|*.md",
            FileName = $"comparison_report_{DateTime.Now:yyyyMMdd_HHmmss}.md"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var results = _comparisonResults.Select(r => r.Result).ToList();
                await _exportService.ExportComparisonReportAsync(_currentComparison, results, dialog.FileName);
                StatusText.Text = $"✓ Report exported to {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ Export failed: {ex.Message}";
            }
        }
    }

    private async void ViewComparisonOutput_Click(object sender, RoutedEventArgs e)
    {
        if (ComparisonDataGrid.SelectedItem is not ComparisonResultViewModel selected) return;

        var scrollViewer = new ScrollViewer { MaxHeight = 400, MaxWidth = 700, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var responseText = new TextBox
        {
            Text = selected.FullOutput,
            TextWrapping = TextWrapping.Wrap,
            IsReadOnly = true,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 12,
            Padding = new Thickness(8)
        };
        scrollViewer.Content = responseText;

        var panel = new StackPanel { Margin = new Thickness(16), MinWidth = 500 };
        panel.Children.Add(new TextBlock
        {
            Text = $"{selected.VariationName} - {selected.ModelName}",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        });
        panel.Children.Add(scrollViewer);
        panel.Children.Add(new Button
        {
            Content = "Close",
            Style = FindResource("MaterialDesignFlatButton") as Style,
            Command = DialogHost.CloseDialogCommand,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        });

        await DialogHost.Show(panel, "TestingDialog");
    }

    private void CopyComparisonOutput_Click(object sender, RoutedEventArgs e)
    {
        if (ComparisonDataGrid.SelectedItem is ComparisonResultViewModel selected)
        {
            Clipboard.SetText(selected.FullOutput);
            StatusText.Text = "✓ Output copied to clipboard";
        }
    }

    private async void Close_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            var confirmed = await ShowConfirmationAsync("Tests are running. Cancel and close?", "Confirm Close");
            if (!confirmed) return;
            _cancellationTokenSource?.Cancel();
        }

        DialogResult = _totalTests > 0;
        Close();
    }


    #region Dialog Helpers

    private async Task<PromptTest?> ShowTestEditorDialogAsync(PromptTest? existingTest)
    {
        var nameBox = new TextBox
        {
            Text = existingTest?.Name ?? "",
            Margin = new Thickness(0, 0, 0, 8)
        };
        HintAssist.SetHint(nameBox, "Test Name");

        var descBox = new TextBox
        {
            Text = existingTest?.Description ?? "",
            Margin = new Thickness(0, 0, 0, 8),
            AcceptsReturn = true,
            Height = 60
        };
        HintAssist.SetHint(descBox, "Description");

        var promptCombo = new ComboBox
        {
            ItemsSource = _allPrompts,
            DisplayMemberPath = "Title",
            Margin = new Thickness(0, 0, 0, 8)
        };
        if (existingTest != null)
        {
            promptCombo.SelectedItem = _allPrompts.FirstOrDefault(p => p.Id == existingTest.PromptId);
        }

        var panel = new StackPanel { Margin = new Thickness(16), MinWidth = 350 };
        panel.Children.Add(new TextBlock { Text = existingTest == null ? "New Test" : "Edit Test", FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(nameBox);
        panel.Children.Add(descBox);
        panel.Children.Add(new TextBlock { Text = "Select Prompt:", Margin = new Thickness(0, 0, 0, 4) });
        panel.Children.Add(promptCombo);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttonPanel.Children.Add(new Button { Content = "Cancel", Style = (Style)FindResource("MaterialDesignFlatButton"), Command = DialogHost.CloseDialogCommand, CommandParameter = false, Margin = new Thickness(0, 0, 8, 0) });
        buttonPanel.Children.Add(new Button { Content = "Save", Style = (Style)FindResource("MaterialDesignRaisedButton"), Command = DialogHost.CloseDialogCommand, CommandParameter = true });
        panel.Children.Add(buttonPanel);

        var result = await DialogHost.Show(panel, "TestingDialog");
        if (result is bool confirmed && confirmed)
        {
            var selectedPrompt = promptCombo.SelectedItem as Prompt;
            if (string.IsNullOrWhiteSpace(nameBox.Text) || selectedPrompt == null)
            {
                StatusText.Text = "⚠️ Please enter a name and select a prompt";
                return null;
            }

            var test = existingTest ?? new PromptTest();
            test.Name = nameBox.Text.Trim();
            test.Description = descBox.Text?.Trim() ?? "";
            test.PromptId = selectedPrompt.Id;
            test.PromptContent = selectedPrompt.Content;
            test.EvaluationCriteria = GetCriteriaFromUI();

            await _databaseService.SavePromptTestAsync(test);
            return test;
        }

        return null;
    }

    private async Task<TestCase?> ShowTestCaseEditorAsync(TestCase? existingCase)
    {
        var nameBox = new TextBox { Text = existingCase?.Name ?? "", Margin = new Thickness(0, 0, 0, 8) };
        HintAssist.SetHint(nameBox, "Test Case Name");

        var inputBox = new TextBox { Text = existingCase?.Input ?? "", Margin = new Thickness(0, 0, 0, 8), AcceptsReturn = true, Height = 80 };
        HintAssist.SetHint(inputBox, "Test Input");

        var keywordsBox = new TextBox { Text = existingCase != null ? string.Join(", ", existingCase.ExpectedKeywords) : "", Margin = new Thickness(0, 0, 0, 8) };
        HintAssist.SetHint(keywordsBox, "Expected Keywords (comma-separated)");

        var forbiddenBox = new TextBox { Text = existingCase != null ? string.Join(", ", existingCase.ShouldNotContain) : "", Margin = new Thickness(0, 0, 0, 8) };
        HintAssist.SetHint(forbiddenBox, "Should Not Contain (comma-separated)");

        var patternBox = new TextBox { Text = existingCase?.ExpectedOutputPattern ?? "", Margin = new Thickness(0, 0, 0, 8) };
        HintAssist.SetHint(patternBox, "Expected Output Pattern (regex)");

        var minScoreSlider = new Slider { Minimum = 0, Maximum = 100, Value = existingCase?.MinQualityScore ?? 50, TickFrequency = 10, IsSnapToTickEnabled = true };

        var panel = new StackPanel { Margin = new Thickness(16), MinWidth = 400 };
        panel.Children.Add(new TextBlock { Text = existingCase == null ? "Add Test Case" : "Edit Test Case", FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(nameBox);
        panel.Children.Add(inputBox);
        panel.Children.Add(keywordsBox);
        panel.Children.Add(forbiddenBox);
        panel.Children.Add(patternBox);
        panel.Children.Add(new TextBlock { Text = "Minimum Quality Score (overrides global if > 0):", Margin = new Thickness(0, 8, 0, 4) });
        panel.Children.Add(minScoreSlider);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttonPanel.Children.Add(new Button { Content = "Cancel", Style = (Style)FindResource("MaterialDesignFlatButton"), Command = DialogHost.CloseDialogCommand, CommandParameter = false, Margin = new Thickness(0, 0, 8, 0) });
        buttonPanel.Children.Add(new Button { Content = "Save", Style = (Style)FindResource("MaterialDesignRaisedButton"), Command = DialogHost.CloseDialogCommand, CommandParameter = true });
        panel.Children.Add(buttonPanel);

        var result = await DialogHost.Show(panel, "TestingDialog");
        if (result is bool confirmed && confirmed)
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text))
            {
                StatusText.Text = "⚠️ Please enter a test case name";
                return null;
            }

            return new TestCase
            {
                Id = existingCase?.Id ?? 0,
                Name = nameBox.Text.Trim(),
                Input = inputBox.Text?.Trim() ?? "",
                ExpectedKeywords = keywordsBox.Text?.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).ToList() ?? new(),
                ShouldNotContain = forbiddenBox.Text?.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).ToList() ?? new(),
                ExpectedOutputPattern = patternBox.Text?.Trim() ?? "",
                MinQualityScore = minScoreSlider.Value
            };
        }

        return null;
    }


    private async Task<PromptVariation?> ShowVariationEditorAsync(PromptVariation? existingVariation)
    {
        var nameBox = new TextBox { Text = existingVariation?.Name ?? "", Margin = new Thickness(0, 0, 0, 8) };
        HintAssist.SetHint(nameBox, "Variation Name");

        var descBox = new TextBox { Text = existingVariation?.Description ?? "", Margin = new Thickness(0, 0, 0, 8) };
        HintAssist.SetHint(descBox, "Description");

        var contentBox = new TextBox { Text = existingVariation?.Content ?? "", Margin = new Thickness(0, 0, 0, 8), AcceptsReturn = true, Height = 150, TextWrapping = TextWrapping.Wrap };
        HintAssist.SetHint(contentBox, "Prompt Content");

        var panel = new StackPanel { Margin = new Thickness(16), MinWidth = 450 };
        panel.Children.Add(new TextBlock { Text = existingVariation == null ? "Add Variation" : "Edit Variation", FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(nameBox);
        panel.Children.Add(descBox);
        panel.Children.Add(contentBox);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttonPanel.Children.Add(new Button { Content = "Cancel", Style = (Style)FindResource("MaterialDesignFlatButton"), Command = DialogHost.CloseDialogCommand, CommandParameter = false, Margin = new Thickness(0, 0, 8, 0) });
        buttonPanel.Children.Add(new Button { Content = "Add", Style = (Style)FindResource("MaterialDesignRaisedButton"), Command = DialogHost.CloseDialogCommand, CommandParameter = true });
        panel.Children.Add(buttonPanel);

        var result = await DialogHost.Show(panel, "TestingDialog");
        if (result is bool confirmed && confirmed)
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text) || string.IsNullOrWhiteSpace(contentBox.Text))
            {
                StatusText.Text = "⚠️ Please enter a name and content";
                return null;
            }

            return new PromptVariation
            {
                Name = nameBox.Text.Trim(),
                Description = descBox.Text?.Trim() ?? "",
                Content = contentBox.Text.Trim()
            };
        }

        return null;
    }

    private async void ShowFullOutputDialog(TestResultViewModel result)
    {
        var scrollViewer = new ScrollViewer { MaxHeight = 400, MaxWidth = 700, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var responseText = new TextBox
        {
            Text = string.IsNullOrEmpty(result.FullOutput) ? result.FailureReason ?? "No output" : result.FullOutput,
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
            Text = $"{result.TestCaseName} - {result.ModelName}",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        });
        mainPanel.Children.Add(new TextBlock
        {
            Text = $"Status: {result.StatusIcon} | Quality: {result.QualityScoreFormatted} | Tokens: {result.TokensUsed} | Duration: {result.DurationFormatted}",
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

        await DialogHost.Show(mainPanel, "TestingDialog");
    }

    private async Task<bool> ShowConfirmationAsync(string message, string title)
    {
        var messageText = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) };

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttonPanel.Children.Add(new Button { Content = "Cancel", Style = (Style)FindResource("MaterialDesignFlatButton"), Command = DialogHost.CloseDialogCommand, CommandParameter = false, Margin = new Thickness(0, 0, 8, 0) });
        buttonPanel.Children.Add(new Button { Content = "OK", Style = (Style)FindResource("MaterialDesignRaisedButton"), Command = DialogHost.CloseDialogCommand, CommandParameter = true });

        var mainPanel = new StackPanel { Margin = new Thickness(16), MinWidth = 300 };
        mainPanel.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
        mainPanel.Children.Add(messageText);
        mainPanel.Children.Add(buttonPanel);

        var result = await DialogHost.Show(mainPanel, "TestingDialog");
        return result is bool confirmed && confirmed;
    }

    private async Task ShowMessageDialogAsync(string title, string message)
    {
        var messageText = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) };

        var closeButton = new Button { Content = "OK", Style = (Style)FindResource("MaterialDesignRaisedButton"), Command = DialogHost.CloseDialogCommand, HorizontalAlignment = HorizontalAlignment.Right };

        var mainPanel = new StackPanel { Margin = new Thickness(16), MinWidth = 300 };
        mainPanel.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
        mainPanel.Children.Add(messageText);
        mainPanel.Children.Add(closeButton);

        await DialogHost.Show(mainPanel, "TestingDialog");
    }

    #endregion
}
