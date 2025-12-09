using System;
using System.Collections.Generic;

namespace PromptBox.Models;

/// <summary>
/// Represents a test suite for a specific prompt
/// </summary>
public class PromptTest
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PromptId { get; set; }
    public string PromptContent { get; set; } = string.Empty;
    public List<TestCase> TestCases { get; set; } = new();
    public TestEvaluationCriteria EvaluationCriteria { get; set; } = new();
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime UpdatedDate { get; set; } = DateTime.Now;
}

/// <summary>
/// Represents individual test scenarios with expected outcomes
/// </summary>
public class TestCase
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string ExpectedOutputPattern { get; set; } = string.Empty;
    public List<string> ExpectedKeywords { get; set; } = new();
    public List<string> ShouldNotContain { get; set; } = new();
    public double MinQualityScore { get; set; } = 50;
    public int MaxTokens { get; set; } = 2000;
}

/// <summary>
/// Stores execution results with detailed metrics
/// </summary>
public class TestResult
{
    public int Id { get; set; }
    public int TestId { get; set; }
    public string TestCaseName { get; set; } = string.Empty;
    public string PromptContent { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string ActualOutput { get; set; } = string.Empty;
    public bool Success { get; set; }

    /// <summary>Response quality score (0-100) - measures the quality of the AI-generated response, not the prompt itself</summary>
    public double QualityScore { get; set; }

    /// <summary>Response clarity score (0-100) - measures how clear and understandable the AI response is</summary>
    public double ClarityScore { get; set; }

    /// <summary>Response specificity score (0-100) - measures how specific and detailed the AI response is</summary>
    public double SpecificityScore { get; set; }

    /// <summary>Effectiveness score (0-100) - measures how well the response meets test case criteria (keywords, patterns)</summary>
    public double EffectivenessScore { get; set; }

    public int TokensUsed { get; set; }
    public TimeSpan Duration { get; set; }
    public string? FailureReason { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Defines evaluation rules for test validation
/// </summary>
public class TestEvaluationCriteria
{
    public bool CheckKeywords { get; set; } = true;
    public bool CheckPatterns { get; set; } = false;
    public bool CheckQualityScore { get; set; } = true;
    public double MinimumQualityScore { get; set; } = 50;
    public bool CheckTokenUsage { get; set; } = false;
    public int MaxTokensAllowed { get; set; } = 2000;
    public bool CheckResponseTime { get; set; } = false;
    public double MaxResponseTimeSeconds { get; set; } = 30;
}


/// <summary>
/// Supports A/B testing of prompt variations
/// </summary>
public class TestComparison
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<PromptVariation> PromptVariations { get; set; } = new();
    public string TestInput { get; set; } = string.Empty;
    public List<string> ModelIds { get; set; } = new();
    public List<ComparisonResult> Results { get; set; } = new();
    public DateTime CreatedDate { get; set; } = DateTime.Now;
}

/// <summary>
/// Represents different versions of a prompt for comparison
/// </summary>
public class PromptVariation
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Stores comparison metrics for each variation
/// </summary>
public class ComparisonResult
{
    public string VariationName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public double QualityScore { get; set; }
    public double ClarityScore { get; set; }
    public double SpecificityScore { get; set; }
    public int TokensUsed { get; set; }
    public TimeSpan Duration { get; set; }
    public int Ranking { get; set; }
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
}

/// <summary>
/// Real-time progress updates during comparison execution
/// </summary>
public class ComparisonProgress
{
    public string CurrentVariation { get; set; } = string.Empty;
    public string CurrentModel { get; set; } = string.Empty;
    public double PercentComplete { get; set; }
    public ComparisonResult? LastResult { get; set; }
}

/// <summary>
/// Real-time progress updates during test execution
/// </summary>
public class TestExecutionProgress
{
    public int CurrentTestCase { get; set; }
    public int TotalTestCases { get; set; }
    public string CurrentModel { get; set; } = string.Empty;
    public double PercentComplete { get; set; }
    public TestResult? LastResult { get; set; }
}

/// <summary>
/// Aggregate statistics for a test suite
/// </summary>
public class TestStatistics
{
    public int TotalExecutions { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public double PassRate { get; set; }
    public double AverageQualityScore { get; set; }
    public double AverageClarityScore { get; set; }
    public double AverageSpecificityScore { get; set; }
    public double AverageEffectivenessScore { get; set; }
    public double AverageTokensUsed { get; set; }
    public TimeSpan AverageDuration { get; set; }
}
