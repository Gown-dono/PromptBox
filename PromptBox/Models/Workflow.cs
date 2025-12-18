using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;

namespace PromptBox.Models;

#region Enums

/// <summary>
/// Type of workflow step determining execution behavior
/// </summary>
public enum WorkflowStepType
{
    /// <summary>Standard linear step</summary>
    Standard,
    /// <summary>Conditional branching step</summary>
    Conditional,
    /// <summary>Loop step with exit condition</summary>
    Loop,
    /// <summary>Parallel execution step - executes multiple branches concurrently</summary>
    Parallel
}

/// <summary>
/// Type of condition to evaluate
/// </summary>
public enum ConditionType
{
    /// <summary>Check if output contains a value</summary>
    OutputContains,
    /// <summary>Check if output matches exactly</summary>
    OutputMatches,
    /// <summary>Check output length</summary>
    OutputLength,
    /// <summary>Check if step succeeded</summary>
    Success,
    /// <summary>Check token count</summary>
    TokenCount,
    /// <summary>Custom regex pattern</summary>
    Regex
}

/// <summary>
/// Comparison operator for conditions
/// </summary>
public enum ComparisonOperator
{
    Equals,
    NotEquals,
    Contains,
    GreaterThan,
    LessThan,
    GreaterThanOrEquals,
    LessThanOrEquals,
    RegexMatch
}

/// <summary>
/// Execution status of a workflow node
/// </summary>
public enum NodeExecutionStatus
{
    Pending,
    Running,
    Success,
    Failed,
    Skipped
}

#endregion

#region Condition and Configuration Classes

/// <summary>
/// Evaluates conditions for conditional branching and loop exits
/// </summary>
public class ConditionEvaluator
{
    public ConditionType Type { get; set; } = ConditionType.Success;
    public string ComparisonValue { get; set; } = string.Empty;
    public ComparisonOperator Operator { get; set; } = ComparisonOperator.Equals;
    
    /// <summary>
    /// Evaluates the condition against the given output and result
    /// </summary>
    public bool Evaluate(string output, WorkflowStepResult? result)
    {
        return Type switch
        {
            // Force Contains semantics for OutputContains regardless of Operator setting
            ConditionType.OutputContains => EvaluateStringComparison(output, ComparisonValue, ComparisonOperator.Contains),
            ConditionType.OutputMatches => output.Equals(ComparisonValue, StringComparison.OrdinalIgnoreCase),
            ConditionType.OutputLength => EvaluateNumericComparison(output.Length, int.TryParse(ComparisonValue, out var len) ? len : 0, Operator),
            ConditionType.Success => result?.Success == true,
            ConditionType.TokenCount => EvaluateNumericComparison(result?.TokensUsed ?? 0, int.TryParse(ComparisonValue, out var tokens) ? tokens : 0, Operator),
            ConditionType.Regex => SafeRegexMatch(output, ComparisonValue),
            _ => false
        };
    }
    
    /// <summary>
    /// Safely evaluates a regex match, returning false if the pattern is invalid
    /// </summary>
    private static bool SafeRegexMatch(string input, string pattern)
    {
        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(input, pattern);
        }
        catch (ArgumentException)
        {
            // Invalid regex pattern - treat as condition not met
            System.Diagnostics.Debug.WriteLine($"Invalid regex pattern: {pattern}");
            return false;
        }
    }
    
    private static bool EvaluateStringComparison(string value, string comparison, ComparisonOperator op)
    {
        return op switch
        {
            ComparisonOperator.Contains => value.Contains(comparison, StringComparison.OrdinalIgnoreCase),
            ComparisonOperator.Equals => value.Equals(comparison, StringComparison.OrdinalIgnoreCase),
            ComparisonOperator.NotEquals => !value.Equals(comparison, StringComparison.OrdinalIgnoreCase),
            ComparisonOperator.RegexMatch => SafeRegexMatch(value, comparison),
            _ => false
        };
    }
    
    private static bool EvaluateNumericComparison(int value, int comparison, ComparisonOperator op)
    {
        return op switch
        {
            ComparisonOperator.Equals => value == comparison,
            ComparisonOperator.NotEquals => value != comparison,
            ComparisonOperator.GreaterThan => value > comparison,
            ComparisonOperator.LessThan => value < comparison,
            ComparisonOperator.GreaterThanOrEquals => value >= comparison,
            ComparisonOperator.LessThanOrEquals => value <= comparison,
            _ => false
        };
    }
    
    public ConditionEvaluator Clone() => new()
    {
        Type = Type,
        ComparisonValue = ComparisonValue,
        Operator = Operator
    };
}

/// <summary>
/// Configuration for error handling and retry logic
/// </summary>
public class ErrorHandlingConfig
{
    /// <summary>Maximum number of retry attempts (0 = no retry)</summary>
    public int MaxRetries { get; set; } = 0;
    
    /// <summary>Initial delay between retries in milliseconds</summary>
    public int RetryDelayMs { get; set; } = 1000;
    
    /// <summary>Whether to use exponential backoff for retries</summary>
    public bool UseExponentialBackoff { get; set; } = true;
    
    /// <summary>Step ID to execute if all retries fail (null = stop workflow)</summary>
    public string? FallbackStepId { get; set; }
    
    /// <summary>Whether to continue workflow execution on error</summary>
    public bool ContinueOnError { get; set; } = false;
    
    public ErrorHandlingConfig Clone() => new()
    {
        MaxRetries = MaxRetries,
        RetryDelayMs = RetryDelayMs,
        UseExponentialBackoff = UseExponentialBackoff,
        FallbackStepId = FallbackStepId,
        ContinueOnError = ContinueOnError
    };
}

/// <summary>
/// Configuration for loop steps
/// </summary>
public class LoopConfig
{
    /// <summary>Maximum number of iterations before forced exit</summary>
    public int MaxIterations { get; set; } = 10;
    
    /// <summary>
    /// Condition that triggers loop exit when true.
    /// Default is Success type which exits on first successful iteration.
    /// Use OutputContains or Regex for content-based exit conditions.
    /// </summary>
    public ConditionEvaluator ExitCondition { get; set; } = new()
    {
        Type = ConditionType.OutputContains,
        ComparisonValue = "DONE",
        Operator = ComparisonOperator.Contains
    };
    
    /// <summary>Variable name to store current iteration count</summary>
    public string LoopVariable { get; set; } = "iteration_count";
    
    public LoopConfig Clone() => new()
    {
        MaxIterations = MaxIterations,
        ExitCondition = ExitCondition.Clone(),
        LoopVariable = LoopVariable
    };
}

/// <summary>
/// Configuration for parallel execution steps
/// </summary>
public class ParallelConfig
{
    /// <summary>List of step IDs to execute in parallel</summary>
    public List<string> BranchStepIds { get; set; } = new();
    
    /// <summary>Whether to wait for all branches to complete before continuing</summary>
    public bool WaitForAll { get; set; } = true;
    
    /// <summary>Whether to continue if any branch fails</summary>
    public bool ContinueOnBranchFailure { get; set; } = false;
    
    /// <summary>Variable name prefix for storing each branch's output (e.g., "branch_0", "branch_1")</summary>
    public string OutputVariablePrefix { get; set; } = "parallel_branch";
    
    public ParallelConfig Clone() => new()
    {
        BranchStepIds = new List<string>(BranchStepIds),
        WaitForAll = WaitForAll,
        ContinueOnBranchFailure = ContinueOnBranchFailure,
        OutputVariablePrefix = OutputVariablePrefix
    };
}

/// <summary>
/// Represents a conditional branch from a step
/// </summary>
public class ConditionalBranch
{
    /// <summary>Condition to evaluate for this branch</summary>
    public ConditionEvaluator Condition { get; set; } = new();
    
    /// <summary>Step ID to execute if condition is true</summary>
    public string NextStepId { get; set; } = string.Empty;
    
    /// <summary>Display label for this branch</summary>
    public string Label { get; set; } = string.Empty;
    
    public ConditionalBranch Clone() => new()
    {
        Condition = Condition.Clone(),
        NextStepId = NextStepId,
        Label = Label
    };
}

#endregion

#region Main Models

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
    
    /// <summary>
    /// Gets the start step of the workflow
    /// </summary>
    public WorkflowStep? GetStartStep()
    {
        return Steps.FirstOrDefault(s => s.IsStartStep) ?? Steps.OrderBy(s => s.Order).FirstOrDefault();
    }
    
    /// <summary>
    /// Gets a step by its ID
    /// </summary>
    public WorkflowStep? GetStepById(string stepId)
    {
        return Steps.FirstOrDefault(s => s.StepId == stepId);
    }
    
    /// <summary>
    /// Creates a deep copy of this workflow
    /// </summary>
    public Workflow Clone()
    {
        var clone = new Workflow
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Category = Category,
            CreatedDate = CreatedDate,
            UpdatedDate = UpdatedDate,
            IsBuiltIn = IsBuiltIn,
            Steps = Steps.Select(s => s.Clone()).ToList()
        };
        return clone;
    }
}

/// <summary>
/// Represents a single step in a workflow
/// </summary>
public class WorkflowStep
{
    /// <summary>Unique identifier for this step</summary>
    public string StepId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>Order for linear execution (backward compatibility)</summary>
    public int Order { get; set; }
    
    /// <summary>Display name of the step</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Description of what this step does</summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>The prompt template with variable placeholders</summary>
    public string PromptTemplate { get; set; } = string.Empty;
    
    /// <summary>Whether this step uses the previous step's output</summary>
    public bool UsesPreviousOutput { get; set; } = true;
    
    /// <summary>Variable name to store this step's output</summary>
    public string OutputVariable { get; set; } = string.Empty;
    
    // Advanced workflow properties
    
    /// <summary>Type of step (Standard, Conditional, Loop, Parallel)</summary>
    public WorkflowStepType StepType { get; set; } = WorkflowStepType.Standard;
    
    /// <summary>Next step ID for linear flow (null = end of workflow)</summary>
    public string? NextStepId { get; set; }
    
    /// <summary>Conditional branches for Conditional step type</summary>
    public List<ConditionalBranch> ConditionalBranches { get; set; } = new();
    
    /// <summary>Error handling configuration</summary>
    public ErrorHandlingConfig? ErrorHandling { get; set; }
    
    /// <summary>Loop configuration for Loop step type</summary>
    public LoopConfig? LoopConfig { get; set; }
    
    /// <summary>Parallel configuration for Parallel step type</summary>
    public ParallelConfig? ParallelConfig { get; set; }
    
    /// <summary>Position on the visual canvas</summary>
    public Point Position { get; set; } = new Point(0, 0);
    
    /// <summary>Whether the position has been explicitly set (not default)</summary>
    public bool HasValidPosition { get; set; } = false;
    
    /// <summary>Whether this is the starting step of the workflow</summary>
    public bool IsStartStep { get; set; } = false;
    
    /// <summary>Whether this is an ending step of the workflow</summary>
    public bool IsEndStep { get; set; } = false;
    
    /// <summary>
    /// Creates a deep copy of this step
    /// </summary>
    public WorkflowStep Clone()
    {
        return new WorkflowStep
        {
            StepId = StepId,
            Order = Order,
            Name = Name,
            Description = Description,
            PromptTemplate = PromptTemplate,
            UsesPreviousOutput = UsesPreviousOutput,
            OutputVariable = OutputVariable,
            StepType = StepType,
            NextStepId = NextStepId,
            ConditionalBranches = ConditionalBranches.Select(b => b.Clone()).ToList(),
            ErrorHandling = ErrorHandling?.Clone(),
            LoopConfig = LoopConfig?.Clone(),
            ParallelConfig = ParallelConfig?.Clone(),
            Position = Position,
            HasValidPosition = HasValidPosition,
            IsStartStep = IsStartStep,
            IsEndStep = IsEndStep
        };
    }
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
    public string StepId { get; set; } = string.Empty;
    public int StepOrder { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public TimeSpan Duration { get; set; }
    public int TokensUsed { get; set; }
    public int RetryCount { get; set; }
    public NodeExecutionStatus ExecutionStatus { get; set; } = NodeExecutionStatus.Pending;
}

/// <summary>
/// Context for workflow execution containing variables and state
/// </summary>
public class WorkflowExecutionContext
{
    /// <summary>Variables available for substitution</summary>
    public Dictionary<string, string> Variables { get; set; } = new();
    
    /// <summary>Results from executed steps</summary>
    public Dictionary<string, WorkflowStepResult> StepResults { get; set; } = new();
    
    /// <summary>Currently executing step ID</summary>
    public string CurrentStepId { get; set; } = string.Empty;
    
    /// <summary>Set of visited step IDs for loop detection</summary>
    public HashSet<string> VisitedSteps { get; set; } = new();
    
    /// <summary>Loop iteration counters by step ID</summary>
    public Dictionary<string, int> LoopCounters { get; set; } = new();
    
    /// <summary>Cancellation token for the execution</summary>
    public CancellationToken CancellationToken { get; set; }
    
    /// <summary>The initial input to the workflow</summary>
    public string InitialInput { get; set; } = string.Empty;
    
    /// <summary>Output from the previous step</summary>
    public string PreviousOutput { get; set; } = string.Empty;
}

/// <summary>
/// Pre-built workflow template
/// </summary>
public class WorkflowTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string IconKind { get; set; } = "FileDocument";
    public Workflow TemplateWorkflow { get; set; } = new();
    public bool IsBuiltIn { get; set; } = true;
}

#endregion

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
