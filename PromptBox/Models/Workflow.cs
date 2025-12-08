using System;
using System.Collections.Generic;

namespace PromptBox.Models;

/// <summary>
/// Represents a multi-step prompt workflow
/// </summary>
public class Workflow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<WorkflowStep> Steps { get; set; } = new();
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime UpdatedDate { get; set; } = DateTime.Now;
    public bool IsBuiltIn { get; set; }
}

/// <summary>
/// Represents a single step in a workflow
/// </summary>
public class WorkflowStep
{
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PromptTemplate { get; set; } = string.Empty;
    public bool UsesPreviousOutput { get; set; } = true;
    public string OutputVariable { get; set; } = string.Empty;
}

/// <summary>
/// Represents the execution state of a workflow
/// </summary>
public class WorkflowExecution
{
    public Workflow Workflow { get; set; } = new();
    public string InitialInput { get; set; } = string.Empty;
    public int CurrentStepIndex { get; set; }
    public List<WorkflowStepResult> StepResults { get; set; } = new();
    public WorkflowStatus Status { get; set; } = WorkflowStatus.NotStarted;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Result of a single workflow step execution
/// </summary>
public class WorkflowStepResult
{
    public int StepOrder { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Status of workflow execution
/// </summary>
public enum WorkflowStatus
{
    NotStarted,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}
