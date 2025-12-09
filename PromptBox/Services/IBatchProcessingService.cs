using PromptBox.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PromptBox.Services;

/// <summary>
/// Interface for batch processing of multiple prompts against multiple AI models
/// </summary>
public interface IBatchProcessingService
{
    /// <summary>
    /// Executes a batch job, yielding progress updates as each prompt-model combination completes
    /// </summary>
    IAsyncEnumerable<BatchExecutionProgress> ExecuteBatchAsync(
        BatchJob job, 
        List<Prompt> prompts, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new batch job with the specified parameters
    /// </summary>
    Task<BatchJob> CreateBatchJobAsync(
        string name,
        string description,
        List<int> promptIds, 
        List<string> modelIds, 
        AIGenerationSettings settings);
    
    /// <summary>
    /// Pauses a running batch job
    /// </summary>
    Task PauseBatchJobAsync(int jobId);
    
    /// <summary>
    /// Resumes a paused batch job
    /// </summary>
    Task ResumeBatchJobAsync(int jobId);
    
    /// <summary>
    /// Cancels a running or paused batch job
    /// </summary>
    Task CancelBatchJobAsync(int jobId);
    
    /// <summary>
    /// Gets recent batch jobs
    /// </summary>
    Task<List<BatchJob>> GetRecentBatchJobsAsync(int count = 10);
    
    /// <summary>
    /// Gets statistics for a completed batch job
    /// </summary>
    Task<BatchJobStatistics> GetBatchJobStatisticsAsync(int jobId);
    
    /// <summary>
    /// Checks if a job is currently paused
    /// </summary>
    bool IsJobPaused(int jobId);
}
