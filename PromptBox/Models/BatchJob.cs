using System;
using System.Collections.Generic;

namespace PromptBox.Models;

/// <summary>
/// Represents a batch execution job for processing multiple prompts against multiple models
/// </summary>
public class BatchJob
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<int> PromptIds { get; set; } = new();
    public List<string> ModelIds { get; set; } = new();
    public BatchJobStatus Status { get; set; } = BatchJobStatus.NotStarted;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int TotalPrompts { get; set; }
    public int CompletedPrompts { get; set; }
    public int SuccessfulPrompts { get; set; }
    public int FailedPrompts { get; set; }
    public AIGenerationSettings Settings { get; set; } = new();
}

/// <summary>
/// Represents an individual execution result within a batch job
/// </summary>
public class BatchResult
{
    public int Id { get; set; }
    public int BatchJobId { get; set; }
    public int PromptId { get; set; }
    public string PromptTitle { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Response { get; set; } = string.Empty;
    public string? Error { get; set; }
    public int TokensUsed { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Status of a batch job
/// </summary>
public enum BatchJobStatus
{
    NotStarted,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Progress information for real-time batch execution updates
/// </summary>
public class BatchExecutionProgress
{
    public int CurrentPromptIndex { get; set; }
    public int CurrentModelIndex { get; set; }
    public int TotalPrompts { get; set; }
    public int TotalModels { get; set; }
    public string PromptTitle { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public double PercentComplete { get; set; }
    public BatchResult? LastResult { get; set; }
}

/// <summary>
/// Statistics for a completed batch job
/// </summary>
public class BatchJobStatistics
{
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public double SuccessRate { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public int TotalTokensUsed { get; set; }
    public double AverageTokensPerPrompt { get; set; }
    
    /// <summary>
    /// Estimated total cost in USD based on token usage and model pricing
    /// </summary>
    public double EstimatedTotalCost { get; set; }
    
    /// <summary>
    /// Estimated average cost per execution in USD
    /// </summary>
    public double EstimatedAverageCostPerExecution { get; set; }
}
