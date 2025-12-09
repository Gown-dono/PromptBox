using PromptBox.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PromptBox.Services;

/// <summary>
/// Service for prompt testing and A/B comparison
/// </summary>
public class PromptTestingService : IPromptTestingService
{
    private readonly IAIService _aiService;
    private readonly IDatabaseService _databaseService;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, bool> _pausedTests = new();

    public PromptTestingService(IAIService aiService, IDatabaseService databaseService)
    {
        _aiService = aiService;
        _databaseService = databaseService;
    }

    public async Task<PromptTest> CreateTestAsync(string name, string description, int promptId, string promptContent, List<TestCase> testCases, TestEvaluationCriteria criteria)
    {
        var test = new PromptTest
        {
            Name = name,
            Description = description,
            PromptId = promptId,
            PromptContent = promptContent,
            TestCases = testCases,
            EvaluationCriteria = criteria,
            CreatedDate = DateTime.Now,
            UpdatedDate = DateTime.Now
        };

        var testId = await _databaseService.SavePromptTestAsync(test);
        test.Id = testId;
        return test;
    }

    public async IAsyncEnumerable<TestExecutionProgress> ExecuteTestAsync(
        PromptTest test,
        List<string> modelIds,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var availableModels = await _aiService.GetAvailableModelsAsync();
        var selectedModels = availableModels.Where(m => modelIds.Contains(m.Id)).ToList();

        int totalExecutions = test.TestCases.Count * selectedModels.Count;
        int completedExecutions = 0;

        foreach (var testCase in test.TestCases)
        {
            foreach (var model in selectedModels)
            {
                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    _pausedTests.TryRemove(test.Id, out _);
                    yield break;
                }

                // Check for pause - wait while paused
                while (_pausedTests.TryGetValue(test.Id, out var isPaused) && isPaused)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _pausedTests.TryRemove(test.Id, out _);
                        yield break;
                    }
                    await Task.Delay(500, cancellationToken);
                }

                var result = await ExecuteSingleTestCaseAsync(testCase, test.PromptContent, model.Id, test.EvaluationCriteria);
                result.TestId = test.Id;
                result.ModelName = model.Name;

                await _databaseService.SaveTestResultAsync(result);

                completedExecutions++;

                var progress = new TestExecutionProgress
                {
                    CurrentTestCase = test.TestCases.IndexOf(testCase) + 1,
                    TotalTestCases = test.TestCases.Count,
                    CurrentModel = model.Name,
                    PercentComplete = (double)completedExecutions / totalExecutions * 100,
                    LastResult = result
                };

                yield return progress;
            }
        }
    }

    public async Task<TestResult> ExecuteSingleTestCaseAsync(TestCase testCase, string promptContent, string modelId, TestEvaluationCriteria criteria)
    {
        var result = new TestResult
        {
            TestCaseName = testCase.Name,
            PromptContent = promptContent,
            ModelId = modelId,
            Input = testCase.Input,
            ExecutedAt = DateTime.Now
        };

        var startTime = DateTime.Now;

        try
        {
            // Build full prompt by combining prompt content with test case input
            var fullPrompt = string.IsNullOrWhiteSpace(testCase.Input)
                ? promptContent
                : $"{promptContent}\n\nInput: {testCase.Input}";

            var settings = new AIGenerationSettings
            {
                ModelId = modelId,
                MaxOutputTokens = testCase.MaxTokens > 0 ? testCase.MaxTokens : 2000
            };

            // Generate response
            var response = await _aiService.GenerateAsync(fullPrompt, settings);
            result.ActualOutput = response.Content ?? string.Empty;
            result.TokensUsed = response.TokensUsed;
            result.Duration = response.Duration;

            if (!response.Success)
            {
                result.Success = false;
                result.FailureReason = response.Error ?? "Generation failed";
                return result;
            }

            // Analyze the AI-generated response quality (not the prompt itself)
            // These scores measure how well the model responded, indicating prompt effectiveness
            try
            {
                var analysis = await _aiService.AnalyzePromptAsync(result.ActualOutput, settings);
                result.QualityScore = analysis.QualityScore;
                result.ClarityScore = ConvertRatingToScore(analysis.ClarityRating);
                result.SpecificityScore = ConvertRatingToScore(analysis.SpecificityRating);
            }
            catch
            {
                // If analysis fails, use default scores based on output length and structure
                result.QualityScore = CalculateBasicQualityScore(result.ActualOutput);
                result.ClarityScore = 50;
                result.SpecificityScore = 50;
            }

            // Calculate effectiveness score based on criteria matching
            result.EffectivenessScore = CalculateEffectivenessScore(result, testCase);

            // Evaluate test result
            await EvaluateTestResultAsync(result, testCase, criteria);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.FailureReason = ex.Message;
            result.Duration = DateTime.Now - startTime;
        }

        return result;
    }


    public Task EvaluateTestResultAsync(TestResult result, TestCase testCase, TestEvaluationCriteria criteria)
    {
        var failures = new List<string>();
        var output = result.ActualOutput.ToLowerInvariant();

        // Check keywords
        if (criteria.CheckKeywords && testCase.ExpectedKeywords.Count > 0)
        {
            var missingKeywords = testCase.ExpectedKeywords
                .Where(k => !output.Contains(k.ToLowerInvariant()))
                .ToList();

            if (missingKeywords.Count > 0)
            {
                failures.Add($"Missing keywords: {string.Join(", ", missingKeywords)}");
            }
        }

        // Check patterns
        if (criteria.CheckPatterns && !string.IsNullOrWhiteSpace(testCase.ExpectedOutputPattern))
        {
            try
            {
                if (!Regex.IsMatch(result.ActualOutput, testCase.ExpectedOutputPattern, RegexOptions.IgnoreCase))
                {
                    failures.Add($"Output does not match expected pattern");
                }
            }
            catch (RegexParseException)
            {
                failures.Add("Invalid regex pattern in test case");
            }
        }

        // Check should not contain
        if (testCase.ShouldNotContain.Count > 0)
        {
            var foundForbidden = testCase.ShouldNotContain
                .Where(k => output.Contains(k.ToLowerInvariant()))
                .ToList();

            if (foundForbidden.Count > 0)
            {
                failures.Add($"Output contains forbidden content: {string.Join(", ", foundForbidden)}");
            }
        }

        // Check quality score - use per-test-case threshold if set, otherwise use global criteria
        if (criteria.CheckQualityScore)
        {
            // Per-test-case MinQualityScore overrides global criteria when > 0
            var effectiveMinScore = testCase.MinQualityScore > 0 
                ? testCase.MinQualityScore 
                : criteria.MinimumQualityScore;
            
            if (result.QualityScore < effectiveMinScore)
            {
                failures.Add($"Quality score {result.QualityScore:F1} below minimum {effectiveMinScore}");
            }
        }

        // Check token usage
        if (criteria.CheckTokenUsage && result.TokensUsed > criteria.MaxTokensAllowed)
        {
            failures.Add($"Token usage {result.TokensUsed} exceeds maximum {criteria.MaxTokensAllowed}");
        }

        // Check response time
        if (criteria.CheckResponseTime && result.Duration.TotalSeconds > criteria.MaxResponseTimeSeconds)
        {
            failures.Add($"Response time {result.Duration.TotalSeconds:F1}s exceeds maximum {criteria.MaxResponseTimeSeconds}s");
        }

        result.Success = failures.Count == 0;
        result.FailureReason = failures.Count > 0 ? string.Join("; ", failures) : null;

        return Task.CompletedTask;
    }

    public async Task<TestComparison> CreateComparisonAsync(string name, List<PromptVariation> variations, string testInput, List<string> modelIds)
    {
        var comparison = new TestComparison
        {
            Name = name,
            PromptVariations = variations,
            TestInput = testInput,
            ModelIds = modelIds,
            CreatedDate = DateTime.Now
        };

        var comparisonId = await _databaseService.SaveTestComparisonAsync(comparison);
        comparison.Id = comparisonId;
        return comparison;
    }

    public async Task<List<ComparisonResult>> ExecuteComparisonAsync(TestComparison comparison, CancellationToken cancellationToken = default)
    {
        var results = new List<ComparisonResult>();
        var availableModels = await _aiService.GetAvailableModelsAsync();
        var selectedModels = availableModels.Where(m => comparison.ModelIds.Contains(m.Id)).ToList();

        foreach (var variation in comparison.PromptVariations)
        {
            if (cancellationToken.IsCancellationRequested)
                return results;

            foreach (var model in selectedModels)
            {
                if (cancellationToken.IsCancellationRequested)
                    return results;

                var result = new ComparisonResult
                {
                    VariationName = variation.Name,
                    ModelName = model.Name,
                    ModelId = model.Id
                };

                try
                {
                    var fullPrompt = string.IsNullOrWhiteSpace(comparison.TestInput)
                        ? variation.Content
                        : $"{variation.Content}\n\nInput: {comparison.TestInput}";

                    var settings = new AIGenerationSettings { ModelId = model.Id };
                    var response = await _aiService.GenerateAsync(fullPrompt, settings);

                    result.Output = response.Content ?? string.Empty;
                    result.TokensUsed = response.TokensUsed;
                    result.Duration = response.Duration;

                    // Get quality score
                    try
                    {
                        var analysis = await _aiService.AnalyzePromptAsync(result.Output, settings);
                        result.QualityScore = analysis.QualityScore;
                    }
                    catch
                    {
                        result.QualityScore = CalculateBasicQualityScore(result.Output);
                    }
                }
                catch (Exception ex)
                {
                    result.Output = $"Error: {ex.Message}";
                    result.QualityScore = 0;
                }

                results.Add(result);
            }
        }

        // Rank results by quality score (highest first)
        var ranked = results.OrderByDescending(r => r.QualityScore).ToList();
        for (int i = 0; i < ranked.Count; i++)
        {
            ranked[i].Ranking = i + 1;
        }

        comparison.Results = results;
        await _databaseService.SaveTestComparisonAsync(comparison);

        return results;
    }


    public async Task<TestStatistics> GetTestStatisticsAsync(int testId)
    {
        var results = await _databaseService.GetTestResultsByTestIdAsync(testId);

        if (results.Count == 0)
        {
            return new TestStatistics();
        }

        var passedTests = results.Count(r => r.Success);

        return new TestStatistics
        {
            TotalExecutions = results.Count,
            PassedTests = passedTests,
            FailedTests = results.Count - passedTests,
            PassRate = (double)passedTests / results.Count * 100,
            AverageQualityScore = results.Average(r => r.QualityScore),
            AverageClarityScore = results.Average(r => r.ClarityScore),
            AverageSpecificityScore = results.Average(r => r.SpecificityScore),
            AverageEffectivenessScore = results.Average(r => r.EffectivenessScore),
            AverageTokensUsed = results.Average(r => r.TokensUsed),
            AverageDuration = TimeSpan.FromTicks((long)results.Average(r => r.Duration.Ticks))
        };
    }

    private static double ConvertRatingToScore(string rating)
    {
        return rating?.ToLowerInvariant() switch
        {
            "high" => 85,
            "medium" => 60,
            "low" => 35,
            _ => 50
        };
    }

    private static double CalculateBasicQualityScore(string output)
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

    private static double CalculateEffectivenessScore(TestResult result, TestCase testCase)
    {
        double score = 50; // Base score
        var output = result.ActualOutput.ToLowerInvariant();

        // Keyword matching bonus
        if (testCase.ExpectedKeywords.Count > 0)
        {
            var matchedKeywords = testCase.ExpectedKeywords
                .Count(k => output.Contains(k.ToLowerInvariant()));
            var keywordRatio = (double)matchedKeywords / testCase.ExpectedKeywords.Count;
            score += keywordRatio * 30;
        }

        // Quality score contribution
        score += result.QualityScore * 0.2;

        // Penalize forbidden content
        if (testCase.ShouldNotContain.Count > 0)
        {
            var foundForbidden = testCase.ShouldNotContain
                .Count(k => output.Contains(k.ToLowerInvariant()));
            score -= foundForbidden * 10;
        }

        return Math.Max(0, Math.Min(100, score));
    }

    public Task PauseTestAsync(int testId)
    {
        _pausedTests[testId] = true;
        return Task.CompletedTask;
    }

    public Task ResumeTestAsync(int testId)
    {
        _pausedTests[testId] = false;
        return Task.CompletedTask;
    }

    public bool IsTestPaused(int testId)
    {
        return _pausedTests.TryGetValue(testId, out var isPaused) && isPaused;
    }
}
