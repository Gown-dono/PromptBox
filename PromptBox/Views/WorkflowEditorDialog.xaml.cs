using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using PromptBox.Models;
using PromptBox.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace PromptBox.Views;

/// <summary>
/// Dialog for creating and editing custom workflows with visual designer
/// </summary>
public partial class WorkflowEditorDialog : Window
{
    private readonly IWorkflowService _workflowService;
    private readonly Workflow _workflow;
    private readonly Workflow? _originalWorkflow;
    private WorkflowStep? _selectedStep;
    private bool _isUpdatingProperties;
    
    public Workflow? SavedWorkflow { get; private set; }

    public WorkflowEditorDialog(IWorkflowService workflowService, Workflow? existingWorkflow = null)
    {
        _workflowService = workflowService;
        
        // Store original and work on a clone to support cancel
        _originalWorkflow = existingWorkflow;
        _workflow = existingWorkflow?.Clone() ?? new Workflow();
        
        InitializeComponent();
        
        // Bind directly to _workflow.Steps to avoid divergence
        RefreshStepsList();
        
        if (existingWorkflow != null)
        {
            LoadExistingWorkflow();
        }
        else
        {
            // Add a default first step
            AddNewStep();
        }
        
        // Defer canvas initialization until after the dialog is fully loaded
        Loaded += (s, e) =>
        {
            WorkflowCanvas.Workflow = _workflow;
        };
    }
    
    /// <summary>
    /// Refreshes the steps list binding to reflect current _workflow.Steps
    /// </summary>
    private void RefreshStepsList()
    {
        StepsList.ItemsSource = null;
        StepsList.ItemsSource = _workflow.Steps.OrderBy(s => s.Order).ToList();
    }

    private void LoadExistingWorkflow()
    {
        WorkflowNameBox.Text = _workflow.Name;
        WorkflowCategoryBox.Text = _workflow.Category;
        WorkflowDescriptionBox.Text = _workflow.Description;
        
        // Migrate workflow if needed
        _workflowService.MigrateWorkflow(_workflow);
        
        // Refresh the list to show existing steps
        RefreshStepsList();
    }

    #region List View Event Handlers

    private void AddStep_Click(object sender, RoutedEventArgs e)
    {
        AddNewStep();
    }

    private void AddNewStep()
    {
        var newOrder = _workflow.Steps.Count;
        var step = new WorkflowStep
        {
            StepId = Guid.NewGuid().ToString(),
            Order = newOrder,
            Name = $"Step {newOrder + 1}",
            Description = "",
            PromptTemplate = "{{previous_output}}",
            UsesPreviousOutput = true,
            IsStartStep = newOrder == 0
        };
        
        _workflow.Steps.Add(step);
        RefreshStepsList();
        
        // Update canvas
        WorkflowCanvas.LoadWorkflow();
    }

    private void DeleteStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is WorkflowStep step)
        {
            var deletedStepId = step.StepId;
            
            // Clean up references to the deleted step in other steps
            foreach (var s in _workflow.Steps)
            {
                // Clear NextStepId if it points to deleted step
                if (s.NextStepId == deletedStepId)
                    s.NextStepId = null;
                
                // Remove conditional branches pointing to deleted step
                s.ConditionalBranches.RemoveAll(b => b.NextStepId == deletedStepId);
                
                // Clear fallback step if it points to deleted step
                if (s.ErrorHandling?.FallbackStepId == deletedStepId)
                    s.ErrorHandling.FallbackStepId = null;
                
                // Remove from parallel branch step IDs
                if (s.ParallelConfig != null)
                    s.ParallelConfig.BranchStepIds.RemoveAll(id => id == deletedStepId);
            }
            
            _workflow.Steps.Remove(step);
            ReorderSteps();
            WorkflowCanvas.LoadWorkflow();
        }
    }

    private void MoveStepUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is WorkflowStep step)
        {
            var index = _workflow.Steps.IndexOf(step);
            if (index > 0)
            {
                _workflow.Steps.RemoveAt(index);
                _workflow.Steps.Insert(index - 1, step);
                
                ReorderSteps();
                WorkflowCanvas.LoadWorkflow();
            }
        }
    }

    private void MoveStepDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is WorkflowStep step)
        {
            var index = _workflow.Steps.IndexOf(step);
            if (index < _workflow.Steps.Count - 1)
            {
                _workflow.Steps.RemoveAt(index);
                _workflow.Steps.Insert(index + 1, step);
                
                ReorderSteps();
                WorkflowCanvas.LoadWorkflow();
            }
        }
    }

    private void ReorderSteps()
    {
        for (int i = 0; i < _workflow.Steps.Count; i++)
        {
            _workflow.Steps[i].Order = i;
        }
        
        // Refresh the list to update order numbers
        RefreshStepsList();
    }

    #endregion

    #region Visual Designer Event Handlers

    private void WorkflowCanvas_StepSelected(object? sender, WorkflowStep step)
    {
        _selectedStep = step;
        ShowStepProperties(step);
    }

    private void WorkflowCanvas_StepDoubleClicked(object? sender, WorkflowStep step)
    {
        // Could open a detailed editor dialog here
        _selectedStep = step;
        ShowStepProperties(step);
    }

    private void WorkflowCanvas_WorkflowModified(object? sender, EventArgs e)
    {
        // Sync list view
        RefreshStepsList();
        
        // Refresh branches panel if a conditional step is selected
        // This ensures canvas-created branches appear in the panel
        if (_selectedStep != null && _selectedStep.StepType == WorkflowStepType.Conditional)
        {
            _isUpdatingProperties = true;
            BranchesItemsControl.ItemsSource = null;
            BranchesItemsControl.ItemsSource = _selectedStep.ConditionalBranches;
            _isUpdatingProperties = false;
        }
    }

    private void ShowStepProperties(WorkflowStep step)
    {
        _isUpdatingProperties = true;
        
        NoSelectionText.Visibility = Visibility.Collapsed;
        StepPropertiesPanel.Visibility = Visibility.Visible;
        
        StepNameBox.Text = step.Name;
        StepDescriptionBox.Text = step.Description;
        StepOutputVariableBox.Text = step.OutputVariable;
        StepPromptBox.Text = step.PromptTemplate;
        
        // Step type
        StepTypeCombo.SelectedIndex = step.StepType switch
        {
            WorkflowStepType.Conditional => 1,
            WorkflowStepType.Loop => 2,
            WorkflowStepType.Parallel => 3,
            _ => 0
        };
        
        // Conditional branches config
        ConditionalBranchesPanel.Visibility = step.StepType == WorkflowStepType.Conditional ? Visibility.Visible : Visibility.Collapsed;
        if (step.StepType == WorkflowStepType.Conditional)
        {
            BranchesItemsControl.ItemsSource = step.ConditionalBranches;
            PopulateStepComboBoxes();
            DefaultNextStepCombo.SelectedValue = step.NextStepId;
        }
        
        // Parallel config
        ParallelConfigPanel.Visibility = step.StepType == WorkflowStepType.Parallel ? Visibility.Visible : Visibility.Collapsed;
        if (step.StepType == WorkflowStepType.Parallel)
        {
            PopulateParallelBranchStepsList();
            if (step.ParallelConfig != null)
            {
                ParallelOutputPrefixBox.Text = step.ParallelConfig.OutputVariablePrefix;
                ParallelWaitForAllCheck.IsChecked = step.ParallelConfig.WaitForAll;
                ParallelContinueOnFailureCheck.IsChecked = step.ParallelConfig.ContinueOnBranchFailure;
                
                // Select the configured branch steps
                foreach (var item in ParallelBranchStepsList.Items)
                {
                    if (item is WorkflowStep s && step.ParallelConfig.BranchStepIds.Contains(s.StepId))
                    {
                        ParallelBranchStepsList.SelectedItems.Add(item);
                    }
                }
            }
        }
        
        // Loop config
        LoopConfigPanel.Visibility = step.StepType == WorkflowStepType.Loop ? Visibility.Visible : Visibility.Collapsed;
        if (step.LoopConfig != null)
        {
            MaxIterationsBox.Text = step.LoopConfig.MaxIterations.ToString();
            LoopVariableBox.Text = step.LoopConfig.LoopVariable;
            
            ExitConditionTypeCombo.SelectedIndex = step.LoopConfig.ExitCondition.Type switch
            {
                ConditionType.OutputMatches => 1,
                ConditionType.Regex => 2,
                ConditionType.Success => 3,
                ConditionType.OutputLength => 4,
                ConditionType.TokenCount => 5,
                _ => 0
            };
            ExitConditionValueBox.Text = step.LoopConfig.ExitCondition.ComparisonValue;
            UpdateExitConditionHint();
        }
        
        // Error handling
        PopulateStepComboBoxes();
        if (step.ErrorHandling != null)
        {
            MaxRetriesBox.Text = step.ErrorHandling.MaxRetries.ToString();
            RetryDelayBox.Text = step.ErrorHandling.RetryDelayMs.ToString();
            ExponentialBackoffCheck.IsChecked = step.ErrorHandling.UseExponentialBackoff;
            ContinueOnErrorCheck.IsChecked = step.ErrorHandling.ContinueOnError;
            FallbackStepCombo.SelectedValue = step.ErrorHandling.FallbackStepId;
        }
        else
        {
            MaxRetriesBox.Text = "0";
            RetryDelayBox.Text = "1000";
            ExponentialBackoffCheck.IsChecked = true;
            ContinueOnErrorCheck.IsChecked = false;
            FallbackStepCombo.SelectedValue = null;
        }
        
        // Flags
        IsStartStepCheck.IsChecked = step.IsStartStep;
        IsEndStepCheck.IsChecked = step.IsEndStep;
        
        _isUpdatingProperties = false;
    }

    private void StepProperty_Changed(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingProperties || _selectedStep == null) return;
        
        _selectedStep.Name = StepNameBox.Text;
        _selectedStep.Description = StepDescriptionBox.Text;
        _selectedStep.OutputVariable = StepOutputVariableBox.Text;
        _selectedStep.PromptTemplate = StepPromptBox.Text;
        
        WorkflowCanvas.LoadWorkflow();
        RefreshStepsList();
    }

    private void StepType_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _selectedStep == null) return;
        
        var selectedItem = StepTypeCombo.SelectedItem as ComboBoxItem;
        var tag = selectedItem?.Tag?.ToString();
        
        _selectedStep.StepType = tag switch
        {
            "Conditional" => WorkflowStepType.Conditional,
            "Loop" => WorkflowStepType.Loop,
            "Parallel" => WorkflowStepType.Parallel,
            _ => WorkflowStepType.Standard
        };
        
        // Initialize loop config if needed
        if (_selectedStep.StepType == WorkflowStepType.Loop && _selectedStep.LoopConfig == null)
        {
            _selectedStep.LoopConfig = new LoopConfig();
        }
        
        // Initialize parallel config if needed
        if (_selectedStep.StepType == WorkflowStepType.Parallel && _selectedStep.ParallelConfig == null)
        {
            _selectedStep.ParallelConfig = new ParallelConfig();
        }
        
        LoopConfigPanel.Visibility = _selectedStep.StepType == WorkflowStepType.Loop ? Visibility.Visible : Visibility.Collapsed;
        ConditionalBranchesPanel.Visibility = _selectedStep.StepType == WorkflowStepType.Conditional ? Visibility.Visible : Visibility.Collapsed;
        ParallelConfigPanel.Visibility = _selectedStep.StepType == WorkflowStepType.Parallel ? Visibility.Visible : Visibility.Collapsed;
        
        if (_selectedStep.StepType == WorkflowStepType.Conditional)
        {
            BranchesItemsControl.ItemsSource = _selectedStep.ConditionalBranches;
            PopulateStepComboBoxes();
        }
        
        if (_selectedStep.StepType == WorkflowStepType.Parallel)
        {
            PopulateParallelBranchStepsList();
        }
        
        WorkflowCanvas.LoadWorkflow();
    }

    private void LoopConfig_Changed(object sender, object e)
    {
        if (_isUpdatingProperties || _selectedStep == null) return;
        
        _selectedStep.LoopConfig ??= new LoopConfig();
        
        if (int.TryParse(MaxIterationsBox.Text, out var maxIter))
            _selectedStep.LoopConfig.MaxIterations = maxIter;
        
        _selectedStep.LoopConfig.LoopVariable = LoopVariableBox.Text;
        
        var conditionItem = ExitConditionTypeCombo.SelectedItem as ComboBoxItem;
        var conditionTag = conditionItem?.Tag?.ToString();
        
        _selectedStep.LoopConfig.ExitCondition.Type = conditionTag switch
        {
            "OutputMatches" => ConditionType.OutputMatches,
            "Regex" => ConditionType.Regex,
            "Success" => ConditionType.Success,
            "OutputLength" => ConditionType.OutputLength,
            "TokenCount" => ConditionType.TokenCount,
            _ => ConditionType.OutputContains
        };
        
        _selectedStep.LoopConfig.ExitCondition.ComparisonValue = ExitConditionValueBox.Text;
        
        // Validate input and update hints
        ValidateConditionValue();
        UpdateExitConditionHint();
    }
    
    private void ValidateConditionValue()
    {
        var conditionItem = ExitConditionTypeCombo.SelectedItem as ComboBoxItem;
        var conditionTag = conditionItem?.Tag?.ToString();
        
        if (conditionTag == "Regex" && !string.IsNullOrEmpty(ExitConditionValueBox.Text))
        {
            // Validate regex pattern
            try
            {
                _ = new System.Text.RegularExpressions.Regex(ExitConditionValueBox.Text);
                RegexValidationMessage.Visibility = Visibility.Collapsed;
            }
            catch (ArgumentException ex)
            {
                RegexValidationMessage.Text = $"Invalid regex: {ex.Message}";
                RegexValidationMessage.Visibility = Visibility.Visible;
            }
        }
        else if ((conditionTag == "OutputLength" || conditionTag == "TokenCount") && !string.IsNullOrEmpty(ExitConditionValueBox.Text))
        {
            // Validate numeric value
            if (!int.TryParse(ExitConditionValueBox.Text, out var numValue) || numValue < 0)
            {
                RegexValidationMessage.Text = "Value must be a positive integer.";
                RegexValidationMessage.Visibility = Visibility.Visible;
            }
            else
            {
                RegexValidationMessage.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            RegexValidationMessage.Visibility = Visibility.Collapsed;
        }
    }
    
    private void ValidateRegexPattern()
    {
        // Delegate to the more comprehensive validation method
        ValidateConditionValue();
    }
    
    private void UpdateExitConditionHint()
    {
        var conditionItem = ExitConditionTypeCombo.SelectedItem as ComboBoxItem;
        var conditionTag = conditionItem?.Tag?.ToString();
        
        ExitConditionHint.Text = conditionTag switch
        {
            "OutputContains" => "Loop exits when output contains the specified text (case-insensitive).",
            "OutputMatches" => "Loop exits when output exactly matches the specified text.",
            "Regex" => "Loop exits when output matches the regex pattern.",
            "Success" => "Loop exits on first successful AI response. No value needed.",
            "OutputLength" => "Loop exits when output length is less than or equal to the specified character count. Enter a positive integer.",
            "TokenCount" => "Loop exits when the current iteration's token count is ≤ the specified value. Each iteration is evaluated independently. Enter a positive integer.",
            _ => ""
        };
    }

    private void ErrorHandling_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _selectedStep == null) return;
        
        _selectedStep.ErrorHandling ??= new ErrorHandlingConfig();
        
        if (int.TryParse(MaxRetriesBox.Text, out var maxRetries))
            _selectedStep.ErrorHandling.MaxRetries = maxRetries;
        
        if (int.TryParse(RetryDelayBox.Text, out var retryDelay))
            _selectedStep.ErrorHandling.RetryDelayMs = retryDelay;
        
        _selectedStep.ErrorHandling.UseExponentialBackoff = ExponentialBackoffCheck.IsChecked ?? true;
        _selectedStep.ErrorHandling.ContinueOnError = ContinueOnErrorCheck.IsChecked ?? false;
    }

    private void PopulateParallelBranchStepsList()
    {
        // Get all steps except the currently selected one
        var availableSteps = _workflow.Steps
            .Where(s => s.StepId != _selectedStep?.StepId)
            .OrderBy(s => s.Order)
            .ToList();
        
        ParallelBranchStepsList.ItemsSource = availableSteps;
    }

    private void ParallelBranchSteps_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _selectedStep == null) return;
        
        _selectedStep.ParallelConfig ??= new ParallelConfig();
        _selectedStep.ParallelConfig.BranchStepIds = ParallelBranchStepsList.SelectedItems
            .Cast<WorkflowStep>()
            .Select(s => s.StepId)
            .ToList();
        
        WorkflowCanvas.LoadWorkflow();
    }

    private void ParallelConfig_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _selectedStep == null) return;
        
        _selectedStep.ParallelConfig ??= new ParallelConfig();
        _selectedStep.ParallelConfig.OutputVariablePrefix = ParallelOutputPrefixBox.Text;
        _selectedStep.ParallelConfig.WaitForAll = ParallelWaitForAllCheck.IsChecked ?? true;
        _selectedStep.ParallelConfig.ContinueOnBranchFailure = ParallelContinueOnFailureCheck.IsChecked ?? false;
    }

    private void StepFlag_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _selectedStep == null) return;
        
        _selectedStep.IsStartStep = IsStartStepCheck.IsChecked ?? false;
        _selectedStep.IsEndStep = IsEndStepCheck.IsChecked ?? false;
        
        // Ensure only one start step
        if (_selectedStep.IsStartStep)
        {
            foreach (var step in _workflow.Steps.Where(s => s.StepId != _selectedStep.StepId))
            {
                step.IsStartStep = false;
            }
        }
        
        WorkflowCanvas.LoadWorkflow();
    }

    #endregion

    #region Conditional Branches and Fallback Step Handlers

    private void PopulateStepComboBoxes()
    {
        // Get all steps except the currently selected one (to avoid self-reference)
        var availableSteps = _workflow.Steps
            .Where(s => s.StepId != _selectedStep?.StepId)
            .OrderBy(s => s.Order)
            .ToList();
        
        // Add a "None" option at the beginning
        var stepsWithNone = new List<object> { new { Name = "(None)", StepId = (string?)null } };
        stepsWithNone.AddRange(availableSteps.Select(s => new { s.Name, StepId = (string?)s.StepId }));
        
        FallbackStepCombo.ItemsSource = stepsWithNone;
        DefaultNextStepCombo.ItemsSource = stepsWithNone;
    }

    private void FallbackStep_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _selectedStep == null) return;
        
        _selectedStep.ErrorHandling ??= new ErrorHandlingConfig();
        _selectedStep.ErrorHandling.FallbackStepId = FallbackStepCombo.SelectedValue as string;
        
        WorkflowCanvas.LoadWorkflow();
    }

    private void DefaultNextStep_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _selectedStep == null) return;
        
        _selectedStep.NextStepId = DefaultNextStepCombo.SelectedValue as string;
        
        WorkflowCanvas.LoadWorkflow();
    }

    private void AddBranch_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedStep == null) return;
        
        var newBranch = new ConditionalBranch
        {
            Label = $"Branch {_selectedStep.ConditionalBranches.Count + 1}",
            Condition = new ConditionEvaluator
            {
                Type = ConditionType.OutputContains,
                Operator = ComparisonOperator.Contains
            }
        };
        
        _selectedStep.ConditionalBranches.Add(newBranch);
        BranchesItemsControl.ItemsSource = null;
        BranchesItemsControl.ItemsSource = _selectedStep.ConditionalBranches;
        
        WorkflowCanvas.LoadWorkflow();
    }

    private void RemoveBranch_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedStep == null) return;
        
        var button = sender as Button;
        var branch = button?.Tag as ConditionalBranch;
        
        if (branch != null)
        {
            _selectedStep.ConditionalBranches.Remove(branch);
            BranchesItemsControl.ItemsSource = null;
            BranchesItemsControl.ItemsSource = _selectedStep.ConditionalBranches;
            
            WorkflowCanvas.LoadWorkflow();
        }
    }

    private void BranchLabel_Changed(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        WorkflowCanvas.LoadWorkflow();
    }

    private void BranchConditionType_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        
        var comboBox = sender as ComboBox;
        var branch = comboBox?.Tag as ConditionalBranch;
        var selectedItem = comboBox?.SelectedItem as ComboBoxItem;
        var tag = selectedItem?.Tag?.ToString();
        
        if (branch != null && tag != null)
        {
            branch.Condition.Type = tag switch
            {
                "OutputMatches" => ConditionType.OutputMatches,
                "Regex" => ConditionType.Regex,
                "Success" => ConditionType.Success,
                _ => ConditionType.OutputContains
            };
            
            // Set appropriate operator for the condition type
            branch.Condition.Operator = branch.Condition.Type == ConditionType.OutputContains 
                ? ComparisonOperator.Contains 
                : ComparisonOperator.Equals;
        }
    }

    private void BranchConditionTypeCombo_Loaded(object sender, RoutedEventArgs e)
    {
        var comboBox = sender as ComboBox;
        var branch = comboBox?.Tag as ConditionalBranch;
        
        if (branch != null)
        {
            comboBox!.SelectedIndex = branch.Condition.Type switch
            {
                ConditionType.OutputMatches => 1,
                ConditionType.Regex => 2,
                ConditionType.Success => 3,
                _ => 0
            };
        }
    }

    private void BranchConditionValue_Changed(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        // The binding handles the value update automatically
    }



    #endregion

    #region Template Loading and Import

    private async void ImportJson_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON Files|*.json",
            Title = "Import Workflow from JSON"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = await File.ReadAllTextAsync(dialog.FileName);
                var importedWorkflow = JsonSerializer.Deserialize<Workflow>(json);
                
                if (importedWorkflow == null)
                {
                    await ShowValidationError("Invalid workflow file.");
                    return;
                }
                
                // First, migrate the workflow to ensure all steps have valid, unique StepIds
                // This handles legacy workflows that may have empty or duplicate IDs
                _workflowService.MigrateWorkflow(importedWorkflow);
                
                // Build mapping from original IDs to new IDs (after migration ensures valid IDs)
                var oldToNewIds = new Dictionary<string, string>();
                foreach (var step in importedWorkflow.Steps)
                {
                    var oldId = step.StepId;
                    // Skip if we've already seen this ID (shouldn't happen after migration, but guard anyway)
                    if (string.IsNullOrEmpty(oldId) || oldToNewIds.ContainsKey(oldId))
                        continue;
                    var newId = Guid.NewGuid().ToString();
                    oldToNewIds[oldId] = newId;
                }
                
                // Now update all step IDs and references in a single pass
                foreach (var step in importedWorkflow.Steps)
                {
                    // Update the step's own ID
                    if (oldToNewIds.TryGetValue(step.StepId, out var newStepId))
                        step.StepId = newStepId;
                    
                    // Update NextStepId reference
                    if (!string.IsNullOrEmpty(step.NextStepId) && oldToNewIds.TryGetValue(step.NextStepId, out var newNextId))
                        step.NextStepId = newNextId;
                    
                    // Update conditional branch references
                    foreach (var branch in step.ConditionalBranches)
                    {
                        if (!string.IsNullOrEmpty(branch.NextStepId) && oldToNewIds.TryGetValue(branch.NextStepId, out var newBranchId))
                            branch.NextStepId = newBranchId;
                    }
                    
                    // Update fallback step reference
                    if (step.ErrorHandling != null && !string.IsNullOrEmpty(step.ErrorHandling.FallbackStepId) 
                        && oldToNewIds.TryGetValue(step.ErrorHandling.FallbackStepId, out var newFallbackId))
                        step.ErrorHandling.FallbackStepId = newFallbackId;
                    
                    // Update parallel branch step IDs
                    if (step.ParallelConfig != null)
                    {
                        var remappedBranchIds = new List<string>();
                        foreach (var branchId in step.ParallelConfig.BranchStepIds)
                        {
                            if (!string.IsNullOrEmpty(branchId) && oldToNewIds.TryGetValue(branchId, out var newBranchStepId))
                                remappedBranchIds.Add(newBranchStepId);
                        }
                        step.ParallelConfig.BranchStepIds = remappedBranchIds;
                    }
                }
                
                _workflow.Name = importedWorkflow.Name + " (Imported)";
                _workflow.Description = importedWorkflow.Description;
                _workflow.Category = importedWorkflow.Category;
                _workflow.Steps.Clear();
                _workflow.Steps.AddRange(importedWorkflow.Steps);
                
                WorkflowNameBox.Text = _workflow.Name;
                WorkflowDescriptionBox.Text = _workflow.Description;
                WorkflowCategoryBox.Text = _workflow.Category;
                
                RefreshStepsList();
                WorkflowCanvas.LoadWorkflow();
                
                await ShowMessage("Import Successful", $"Imported workflow with {importedWorkflow.Steps.Count} steps.");
            }
            catch (Exception ex)
            {
                await ShowValidationError($"Error importing workflow: {ex.Message}");
            }
        }
    }

    // LoadTemplate_Click removed - template loading is problematic in visual designer

    #endregion

    #region Validation and Save

    private async void Validate_Click(object sender, RoutedEventArgs e)
    {
        var (isValid, errors, warnings) = _workflowService.ValidateWorkflow(_workflow);
        
        var messageParts = new List<string>();
        
        if (errors.Any())
            messageParts.Add("Errors:\n• " + string.Join("\n• ", errors));
        
        if (warnings.Any())
            messageParts.Add("Warnings:\n• " + string.Join("\n• ", warnings));
        
        var message = isValid 
            ? (warnings.Any() ? "Workflow is valid with warnings:\n• " + string.Join("\n• ", warnings) : "Workflow is valid!")
            : string.Join("\n\n", messageParts);
        
        await ShowMessage(isValid ? "Validation Passed" : "Validation Failed", message);
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(WorkflowNameBox.Text))
        {
            await ShowValidationError("Please enter a workflow name.");
            return;
        }

        if (_workflow.Steps.Count == 0)
        {
            await ShowValidationError("Please add at least one step.");
            return;
        }

        foreach (var step in _workflow.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Name))
            {
                await ShowValidationError($"Step {step.Order + 1} needs a name.");
                return;
            }
            if (string.IsNullOrWhiteSpace(step.PromptTemplate))
            {
                await ShowValidationError($"Step '{step.Name}' needs a prompt template.");
                return;
            }
        }

        // Validate workflow structure
        var (isValid, errors, warnings) = _workflowService.ValidateWorkflow(_workflow);
        if (!isValid)
        {
            await ShowValidationError("Workflow validation failed:\n• " + string.Join("\n• ", errors));
            return;
        }
        
        // Show warnings but don't block save
        if (warnings.Any())
        {
            await ShowMessage("Validation Warnings", "The workflow has warnings:\n• " + string.Join("\n• ", warnings) + "\n\nSaving anyway...");
        }

        // Update workflow properties
        _workflow.Name = WorkflowNameBox.Text.Trim();
        _workflow.Category = string.IsNullOrWhiteSpace(WorkflowCategoryBox.Text) ? "Custom" : WorkflowCategoryBox.Text.Trim();
        _workflow.Description = WorkflowDescriptionBox.Text?.Trim() ?? "";
        _workflow.IsBuiltIn = false;

        try
        {
            SaveButton.IsEnabled = false;
            await _workflowService.SaveWorkflowAsync(_workflow);
            SavedWorkflow = _workflow;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            await ShowValidationError($"Error saving workflow: {ex.Message}");
            SaveButton.IsEnabled = true;
        }
    }

    private async System.Threading.Tasks.Task ShowValidationError(string message)
    {
        await ShowMessage("Validation Error", message);
    }

    private async System.Threading.Tasks.Task ShowMessage(string title, string message)
    {
        var titleText = new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        
        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 400,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var okButton = new Button
        {
            Content = "OK",
            Style = FindResource("MaterialDesignRaisedButton") as Style,
            Command = DialogHost.CloseDialogCommand
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttonPanel.Children.Add(okButton);

        var mainPanel = new StackPanel
        {
            Margin = new Thickness(16)
        };
        mainPanel.Children.Add(titleText);
        mainPanel.Children.Add(messageText);
        mainPanel.Children.Add(buttonPanel);

        await DialogHost.Show(mainPanel, "WorkflowEditorDialog");
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    #endregion
}
