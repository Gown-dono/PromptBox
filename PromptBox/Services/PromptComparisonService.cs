using PromptBox.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace PromptBox.Services;

public class PromptComparisonService : IPromptComparisonService
{
    private readonly IAIService _aiService;
    private readonly IDatabaseService _databaseService;

    public PromptComparisonService(IAIService aiService, IDatabaseService databaseService)
    {
        _aiService = aiService;
        _databaseService = databaseService;
    }

    public async IAsyncEnumerable<ComparisonProgress> ExecuteComparisonAsync(
        PromptComparisonSession session,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var totalOperations = session.PromptVariations.Count * session.ModelIds.Count;
        var completedOperations = 0;

        // Fetch models once at the beginning of the session
        var models = await _aiService.GetAvailableModelsAsync();
        var modelLookup = models.ToDictionary(m => m.Id, m => m.Name);

        foreach (var variation in session.PromptVariations)
        {
            foreach (var modelId in session.ModelIds)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                var progress = new ComparisonProgress
                {
                    CurrentVariation = variation.Name,
                    CurrentModel = modelId,
                    PercentComplete = (double)completedOperations / totalOperations * 100
                };

                yield return progress;

                var result = await ExecuteSingleComparisonAsync(
                    variation,
                    session.SharedInput,
                    modelId,
                    session.Temperature,
                    session.MaxTokens,
                    modelLookup);

                session.Results.Add(result);
                completedOperations++;

                progress.LastResult = result;
                progress.PercentComplete = (double)completedOperations / totalOperations * 100;

                yield return progress;
            }
        }

        // Rank results after all comparisons complete
        session.Results = RankResults(session.Results);
        session.CompletedDate = DateTime.Now;
    }

    public async Task<ComparisonResult> ExecuteSingleComparisonAsync(
        PromptVariation variation,
        string input,
        string modelId,
        double temperature,
        int maxTokens)
    {
        // Fetch models for standalone calls (interface compatibility)
        var models = await _aiService.GetAvailableModelsAsync();
        var modelLookup = models.ToDictionary(m => m.Id, m => m.Name);
        return await ExecuteSingleComparisonAsync(variation, input, modelId, temperature, maxTokens, modelLookup);
    }

    private async Task<ComparisonResult> ExecuteSingleComparisonAsync(
        PromptVariation variation,
        string input,
        string modelId,
        double temperature,
        int maxTokens,
        Dictionary<string, string> modelLookup)
    {
        var result = new ComparisonResult
        {
            VariationName = variation.Name,
            ModelId = modelId
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Get model name from pre-fetched lookup
            result.ModelName = modelLookup.TryGetValue(modelId, out var modelName) ? modelName : modelId;

            // Build the full prompt with variation content and input
            var fullPrompt = $"{variation.Content}\n\n{input}";

            var settings = new AIGenerationSettings
            {
                ModelId = modelId,
                Temperature = temperature,
                MaxOutputTokens = maxTokens
            };

            // Generate response
            var response = await _aiService.GenerateAsync(fullPrompt, settings);

            // Check if the AI response was successful
            if (!response.Success)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Error = response.Error;
                result.Output = string.Empty;
                result.TokensUsed = 0;
                result.QualityScore = 0;
                result.ClarityScore = 0;
                result.SpecificityScore = 0;
                result.Duration = stopwatch.Elapsed;
                return result;
            }

            result.Output = response.Content;
            result.TokensUsed = response.TokensUsed;
            result.Success = true;

            // Analyze the AI-generated response quality (not the prompt itself)
            // These scores measure how well the model responded, indicating prompt effectiveness
            try
            {
                var analysis = await _aiService.AnalyzePromptAsync(result.Output, settings);
                result.QualityScore = analysis.QualityScore;
                // Convert string ratings to numeric scores
                result.ClarityScore = ConvertRatingToScore(analysis.ClarityRating);
                result.SpecificityScore = ConvertRatingToScore(analysis.SpecificityRating);
            }
            catch
            {
                // If analysis fails, use default scores based on output length and structure
                result.QualityScore = CalculateBasicQualityScore(result.Output);
                result.ClarityScore = 50;
                result.SpecificityScore = 50;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            result.Output = string.Empty;
            result.QualityScore = 0;
            result.ClarityScore = 0;
            result.SpecificityScore = 0;
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    public List<ComparisonResult> RankResults(List<ComparisonResult> results)
    {
        var ranked = results
            .OrderByDescending(r => r.Success)
            .ThenByDescending(r => r.QualityScore)
            .ThenByDescending(r => r.ClarityScore)
            .ThenByDescending(r => r.SpecificityScore)
            .ToList();

        for (int i = 0; i < ranked.Count; i++)
        {
            ranked[i].Ranking = i + 1;
        }

        return ranked;
    }

    private static double ConvertRatingToScore(string rating)
    {
        return rating?.ToLowerInvariant() switch
        {
            "excellent" or "very high" => 95,
            "good" or "high" => 80,
            "moderate" or "medium" => 65,
            "fair" or "low" => 50,
            "poor" or "very low" => 30,
            _ => 70 // Default
        };
    }

    private static double CalculateBasicQualityScore(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return 0;

        double score = 50; // Base score

        // Length bonus (up to 20 points)
        var length = output.Length;
        if (length > 100) score += Math.Min(20, length / 50.0);

        // Structure bonus (up to 15 points)
        if (output.Contains('\n')) score += 5;
        if (output.Contains('.')) score += 5;
        if (output.Contains(':')) score += 5;

        // Penalize very short responses
        if (length < 50) score -= 20;

        return Math.Max(0, Math.Min(100, score));
    }
}
