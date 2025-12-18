using PromptBox.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PromptBox.Services;

/// <summary>
/// Interface for workflow management and execution
/// </summary>
public interface IWorkflowService
{
    /// <summary>
    /// Gets all available workflows (built-in and custom)
    /// </summary>
    Task<List<Workflow>> GetAllWorkflowsAsync();
    
    /// <summary>
    /// Gets built-in workflow templates
    /// </summary>
    List<Workflow> GetBuiltInWorkflows();
    
    /// <summary>
    /// Gets a workflow by ID
    /// </summary>
    Task<Workflow?> GetWorkflowByIdAsync(int id);
    
    /// <summary>
    /// Saves a custom workflow
    /// </summary>
    Task<int> SaveWorkflowAsync(Workflow workflow);
    
    /// <summary>
    /// Deletes a custom workflow
    /// </summary>
    Task<bool> DeleteWorkflowAsync(int id);
    
    /// <summary>
    /// Gets only custom (user-created) workflows
    /// </summary>
    Task<List<Workflow>> GetCustomWorkflowsAsync();
    
    /// <summary>
    /// Executes a workflow with the given input using graph traversal
    /// </summary>
    IAsyncEnumerable<WorkflowStepResult> ExecuteWorkflowAsync(
        Workflow workflow, 
        string initialInput, 
        AIGenerationSettings settings,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all available workflow templates
    /// </summary>
    List<WorkflowTemplate> GetWorkflowTemplates();
    
    /// <summary>
    /// Gets a workflow template by ID
    /// </summary>
    WorkflowTemplate? GetWorkflowTemplateById(int id);
    
    /// <summary>
    /// Creates a new workflow from a template
    /// </summary>
    Workflow CreateWorkflowFromTemplate(int templateId);
    
    /// <summary>
    /// Validates a workflow for execution.
    /// Returns IsValid (true if no errors), Errors (blocking issues), and Warnings (advisory messages).
    /// </summary>
    (bool IsValid, List<string> Errors, List<string> Warnings) ValidateWorkflow(Workflow workflow);
    
    /// <summary>
    /// Migrates old linear workflows to new graph-based format
    /// </summary>
    void MigrateWorkflow(Workflow workflow);
    
    /// <summary>
    /// Event raised when a step starts
    /// </summary>
    event EventHandler<WorkflowStepEventArgs>? StepStarted;
    
    /// <summary>
    /// Event raised when a step completes
    /// </summary>
    event EventHandler<WorkflowStepEventArgs>? StepCompleted;
}

/// <summary>
/// Event args for workflow step events
/// </summary>
public class WorkflowStepEventArgs : EventArgs
{
    public int StepIndex { get; set; }
    public string StepName { get; set; } = string.Empty;
    public int TotalSteps { get; set; }
    public WorkflowStepResult? Result { get; set; }
}
