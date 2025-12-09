using PromptBox.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace PromptBox.Services;

/// <summary>
/// Service for batch processing multiple prompts against multiple AI models
/// </summary>
public class BatchProcessingService : IBatchProcessingService
{
    private readonly IDatabaseService _databaseService;
    private readonly IAIService _aiService;
    private readonly IModelPricingService _pricingService;
    private readonly ConcurrentDictionary<int, bool> _pausedJobs = new();

    public BatchProcessingService(
        IDatabaseService databaseService,
        IAIService aiService,
        IModelPricingService pricingService)
    {
        _databaseService = databaseService;
        _aiService = aiService;
        _pricingService = pricingService;
    }

    public async Task<BatchJob> CreateBatchJobAsync(
        string name,
        string description,
        List<int> promptIds,
        List<string> modelIds,
        AIGenerationSettings settings)
    {
        var job = new BatchJob
        {
            Name = name,
            Description = description,
            PromptIds = promptIds,
            ModelIds = modelIds,
            Settings = settings,
            Status = BatchJobStatus.NotStarted,
            TotalPrompts = promptIds.Count * modelIds.Count,
            CreatedDate = DateTime.Now
        };

        var jobId = await _databaseService.SaveBatchJobAsync(job);
        job.Id = jobId;
        return job;
    }

    public async IAsyncEnumerable<BatchExecutionProgress> ExecuteBatchAsync(
        BatchJob job,
        List<Prompt> prompts,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Update job status to running
        job.Status = BatchJobStatus.Running;
        job.StartedDate = DateTime.Now;
        await _databaseService.SaveBatchJobAsync(job);

        var availableModels = await _aiService.GetAvailableModelsAsync();
        var selectedModels = availableModels.Where(m => job.ModelIds.Contains(m.Id)).ToList();
        
        int totalExecutions = prompts.Count * selectedModels.Count;
        int completedExecutions = 0;

        foreach (var prompt in prompts)
        {
            foreach (var model in selectedModels)
            {
                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    job.Status = BatchJobStatus.Cancelled;
                    job.CompletedDate = DateTime.Now;
                    await _databaseService.SaveBatchJobAsync(job);
                    _pausedJobs.TryRemove(job.Id, out _); // Clean up pause state on cancellation
                    yield break;
                }

                // Check for pause
                while (_pausedJobs.TryGetValue(job.Id, out var isPaused) && isPaused)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        job.Status = BatchJobStatus.Cancelled;
                        job.CompletedDate = DateTime.Now;
                        await _databaseService.SaveBatchJobAsync(job);
                        _pausedJobs.TryRemove(job.Id, out _); // Clean up pause state on cancellation
                        yield break;
                    }
                    await Task.Delay(500, cancellationToken);
                }

                var settings = new AIGenerationSettings
                {
                    ModelId = model.Id,
                    Temperature = job.Settings.Temperature,
                    MaxOutputTokens = job.Settings.MaxOutputTokens
                };

                var result = new BatchResult
                {
                    BatchJobId = job.Id,
                    PromptId = prompt.Id,
                    PromptTitle = prompt.Title,
                    ModelId = model.Id,
                    ModelName = model.Name
                };

                try
                {
                    var response = await _aiService.GenerateAsync(prompt.Content, settings);
                    
                    result.Success = response.Success;
                    result.Response = response.Content ?? string.Empty;
                    result.Error = response.Error;
                    result.TokensUsed = response.TokensUsed;
                    result.Duration = response.Duration;

                    if (response.Success)
                        job.SuccessfulPrompts++;
                    else
                        job.FailedPrompts++;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                    result.Duration = TimeSpan.Zero;
                    job.FailedPrompts++;
                }

                result.ExecutedAt = DateTime.Now;
                completedExecutions++;
                job.CompletedPrompts = completedExecutions;
                
                // Save result and update job atomically
                await _databaseService.SaveBatchResultAndUpdateJobAsync(result, job);

                var progress = new BatchExecutionProgress
                {
                    CurrentPromptIndex = prompts.IndexOf(prompt) + 1,
                    CurrentModelIndex = selectedModels.IndexOf(model) + 1,
                    TotalPrompts = prompts.Count,
                    TotalModels = selectedModels.Count,
                    PromptTitle = prompt.Title,
                    ModelName = model.Name,
                    PercentComplete = (double)completedExecutions / totalExecutions * 100,
                    LastResult = result
                };

                yield return progress;
            }
        }

        // Mark job as completed
        job.Status = job.FailedPrompts > 0 && job.SuccessfulPrompts == 0 
            ? BatchJobStatus.Failed 
            : BatchJobStatus.Completed;
        job.CompletedDate = DateTime.Now;
        await _databaseService.SaveBatchJobAsync(job);
        
        // Clean up pause state
        _pausedJobs.TryRemove(job.Id, out _);
    }

    public Task PauseBatchJobAsync(int jobId)
    {
        _pausedJobs[jobId] = true;
        return Task.CompletedTask;
    }

    public Task ResumeBatchJobAsync(int jobId)
    {
        _pausedJobs[jobId] = false;
        return Task.CompletedTask;
    }

    public async Task CancelBatchJobAsync(int jobId)
    {
        _pausedJobs.TryRemove(jobId, out _);
        
        var job = await _databaseService.GetBatchJobByIdAsync(jobId);
        if (job != null)
        {
            job.Status = BatchJobStatus.Cancelled;
            job.CompletedDate = DateTime.Now;
            await _databaseService.SaveBatchJobAsync(job);
        }
    }

    public async Task<List<BatchJob>> GetRecentBatchJobsAsync(int count = 10)
    {
        var allJobs = await _databaseService.GetAllBatchJobsAsync();
        return allJobs
            .OrderByDescending(j => j.CreatedDate)
            .Take(count)
            .ToList();
    }

    public async Task<BatchJobStatistics> GetBatchJobStatisticsAsync(int jobId)
    {
        var results = await _databaseService.GetBatchResultsByJobIdAsync(jobId);
        
        if (results.Count == 0)
        {
            return new BatchJobStatistics();
        }

        var successfulResults = results.Where(r => r.Success).ToList();
        var totalDuration = TimeSpan.FromTicks(results.Sum(r => r.Duration.Ticks));
        var totalTokens = results.Sum(r => r.TokensUsed);
        
        // Calculate estimated cost based on token usage and model pricing
        var estimatedCost = CalculateEstimatedCost(results);
        
        return new BatchJobStatistics
        {
            TotalExecutions = results.Count,
            SuccessfulExecutions = successfulResults.Count,
            FailedExecutions = results.Count - successfulResults.Count,
            SuccessRate = results.Count > 0 ? (double)successfulResults.Count / results.Count * 100 : 0,
            TotalDuration = totalDuration,
            AverageDuration = results.Count > 0 
                ? TimeSpan.FromTicks(totalDuration.Ticks / results.Count) 
                : TimeSpan.Zero,
            TotalTokensUsed = totalTokens,
            AverageTokensPerPrompt = results.Count > 0 
                ? (double)totalTokens / results.Count 
                : 0,
            EstimatedTotalCost = estimatedCost,
            EstimatedAverageCostPerExecution = results.Count > 0 
                ? estimatedCost / results.Count 
                : 0
        };
    }

    /// <summary>
    /// Calculates estimated cost based on token usage and model pricing.
    /// Uses the pricing service for per-model rates.
    /// </summary>
    private double CalculateEstimatedCost(List<BatchResult> results)
    {
        double totalCost = 0;

        foreach (var result in results)
        {
            var costPer1KTokens = _pricingService.GetCostPer1KTokens(result.ModelId);
            totalCost += (result.TokensUsed / 1000.0) * costPer1KTokens;
        }

        return totalCost;
    }

    public bool IsJobPaused(int jobId)
    {
        return _pausedJobs.TryGetValue(jobId, out var isPaused) && isPaused;
    }
}
