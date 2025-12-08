using MaterialDesignThemes.Wpf;
using PromptBox.Models;
using PromptBox.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PromptBox.Views;

/// <summary>
/// Dialog for creating and editing custom workflows
/// </summary>
public partial class WorkflowEditorDialog : Window
{
    private readonly IWorkflowService _workflowService;
    private readonly Workflow? _existingWorkflow;
    
    public ObservableCollection<WorkflowStep> Steps { get; } = new();
    public Workflow? SavedWorkflow { get; private set; }

    public WorkflowEditorDialog(IWorkflowService workflowService, Workflow? existingWorkflow = null)
    {
        _workflowService = workflowService;
        _existingWorkflow = existingWorkflow;
        
        InitializeComponent();
        
        StepsList.ItemsSource = Steps;
        
        if (_existingWorkflow != null)
        {
            LoadExistingWorkflow();
        }
        else
        {
            // Add a default first step
            AddNewStep();
        }
    }

    private void LoadExistingWorkflow()
    {
        if (_existingWorkflow == null) return;
        
        WorkflowNameBox.Text = _existingWorkflow.Name;
        WorkflowCategoryBox.Text = _existingWorkflow.Category;
        WorkflowDescriptionBox.Text = _existingWorkflow.Description;
        
        foreach (var step in _existingWorkflow.Steps.OrderBy(s => s.Order))
        {
            Steps.Add(new WorkflowStep
            {
                Order = step.Order,
                Name = step.Name,
                Description = step.Description,
                PromptTemplate = step.PromptTemplate,
                OutputVariable = step.OutputVariable,
                UsesPreviousOutput = step.UsesPreviousOutput
            });
        }
    }

    private void AddStep_Click(object sender, RoutedEventArgs e)
    {
        AddNewStep();
    }

    private void AddNewStep()
    {
        var newOrder = Steps.Count + 1;
        Steps.Add(new WorkflowStep
        {
            Order = newOrder,
            Name = $"Step {newOrder}",
            Description = "",
            PromptTemplate = "{{previous_output}}",
            UsesPreviousOutput = true
        });
    }

    private void DeleteStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is WorkflowStep step)
        {
            Steps.Remove(step);
            ReorderSteps();
        }
    }

    private void MoveStepUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is WorkflowStep step)
        {
            var index = Steps.IndexOf(step);
            if (index > 0)
            {
                Steps.Move(index, index - 1);
                ReorderSteps();
            }
        }
    }

    private void MoveStepDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is WorkflowStep step)
        {
            var index = Steps.IndexOf(step);
            if (index < Steps.Count - 1)
            {
                Steps.Move(index, index + 1);
                ReorderSteps();
            }
        }
    }

    private void ReorderSteps()
    {
        for (int i = 0; i < Steps.Count; i++)
        {
            Steps[i].Order = i + 1;
        }
        
        // Refresh the list to update order numbers
        var items = Steps.ToList();
        Steps.Clear();
        foreach (var item in items)
        {
            Steps.Add(item);
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(WorkflowNameBox.Text))
        {
            await ShowValidationError("Please enter a workflow name.");
            return;
        }

        if (Steps.Count == 0)
        {
            await ShowValidationError("Please add at least one step.");
            return;
        }

        foreach (var step in Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Name))
            {
                await ShowValidationError($"Step {step.Order} needs a name.");
                return;
            }
            if (string.IsNullOrWhiteSpace(step.PromptTemplate))
            {
                await ShowValidationError($"Step '{step.Name}' needs a prompt template.");
                return;
            }
        }

        // Create or update workflow
        var workflow = _existingWorkflow ?? new Workflow();
        workflow.Name = WorkflowNameBox.Text.Trim();
        workflow.Category = string.IsNullOrWhiteSpace(WorkflowCategoryBox.Text) ? "Custom" : WorkflowCategoryBox.Text.Trim();
        workflow.Description = WorkflowDescriptionBox.Text?.Trim() ?? "";
        workflow.IsBuiltIn = false;
        workflow.Steps = Steps.Select(s => new WorkflowStep
        {
            Order = s.Order,
            Name = s.Name,
            Description = s.Description,
            PromptTemplate = s.PromptTemplate,
            OutputVariable = $"step{s.Order}",
            UsesPreviousOutput = true
        }).ToList();

        try
        {
            SaveButton.IsEnabled = false;
            await _workflowService.SaveWorkflowAsync(workflow);
            SavedWorkflow = workflow;
            DialogResult = true;
            Close();
        }
        catch (System.Exception ex)
        {
            await ShowValidationError($"Error saving workflow: {ex.Message}");
            SaveButton.IsEnabled = true;
        }
    }

    private async System.Threading.Tasks.Task ShowValidationError(string message)
    {
        var messageText = new TextBlock
        {
            Text = message,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var okButton = new Button
        {
            Content = "OK",
            Style = Application.Current.FindResource("MaterialDesignRaisedButton") as Style,
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
        mainPanel.Children.Add(messageText);
        mainPanel.Children.Add(buttonPanel);

        await DialogHost.Show(mainPanel, "WorkflowEditorDialog");
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
