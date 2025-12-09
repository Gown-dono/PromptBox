using PromptBox.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PromptBox.Services;

/// <summary>
/// Service interface for prompt testing and A/B comparison
/// </summary>
public interface IPromptTestingService
{
    /// <summary>
    /// Creates a new test suite
    /// </summary>
    Task<PromptTest> CreateTestAsync(string name, string description, int promptId, string promptContent, List<TestCase> testCases, TestEvaluationCriteria criteria);

    /// <summary>
    /// Runs test suite with progress streaming
    /// </summary>
    IAsyncEnumerable<TestExecutionProgress> ExecuteTestAsync(PromptTest test, List<string> modelIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes one test case
    /// </summary>
    Task<TestResult> ExecuteSingleTestCaseAsync(TestCase testCase, string promptContent, string modelId, TestEvaluationCriteria criteria);

    /// <summary>
    /// Validates test outcome against criteria and sets result.Success and result.FailureReason
    /// </summary>
    Task EvaluateTestResultAsync(TestResult result, TestCase testCase, TestEvaluationCriteria criteria);

    /// <summary>
    /// Creates A/B test comparison
    /// </summary>
    Task<TestComparison> CreateComparisonAsync(string name, List<PromptVariation> variations, string testInput, List<string> modelIds);

    /// <summary>
    /// Runs comparison test
    /// </summary>
    Task<List<ComparisonResult>> ExecuteComparisonAsync(TestComparison comparison, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates aggregate metrics for a test
    /// </summary>
    Task<TestStatistics> GetTestStatisticsAsync(int testId);

    /// <summary>
    /// Pauses test execution
    /// </summary>
    Task PauseTestAsync(int testId);

    /// <summary>
    /// Resumes paused test execution
    /// </summary>
    Task ResumeTestAsync(int testId);

    /// <summary>
    /// Checks if a test is currently paused
    /// </summary>
    bool IsTestPaused(int testId);
}
