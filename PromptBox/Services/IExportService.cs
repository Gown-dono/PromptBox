using PromptBox.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PromptBox.Services;

public interface IExportService
{
    Task ExportPromptAsMarkdownAsync(Prompt prompt, string filePath);
    Task ExportPromptAsTextAsync(Prompt prompt, string filePath);
    Task ExportAllPromptsAsJsonAsync(List<Prompt> prompts, string filePath);
    Task<List<Prompt>> ImportPromptsFromJsonAsync(string filePath);
    
    // Workflow export/import
    Task ExportWorkflowsAsJsonAsync(List<Workflow> workflows, string filePath);
    Task<List<Workflow>> ImportWorkflowsFromJsonAsync(string filePath);
    
    // Version history export/import
    Task ExportPromptsWithHistoryAsJsonAsync(List<Prompt> prompts, List<PromptVersion> versions, string filePath);
    Task<(List<Prompt> Prompts, List<PromptVersion> Versions)> ImportPromptsWithHistoryFromJsonAsync(string filePath);
    
    // Batch results export
    Task ExportBatchResultsAsCsvAsync(List<BatchResult> results, string filePath);
    Task ExportBatchResultsAsJsonAsync(List<BatchResult> results, string filePath);

    // Test results export
    Task ExportTestResultsAsCsvAsync(List<TestResult> results, string filePath);
    Task ExportTestResultsAsJsonAsync(List<TestResult> results, string filePath);
    Task ExportComparisonReportAsync(TestComparison comparison, List<ComparisonResult> results, string filePath);

    // Prompt comparison export
    Task ExportPromptComparisonReportAsync(PromptComparisonSession session, List<ComparisonResult> results, string filePath);
    
    // Workflow visual export
    Task ExportWorkflowAsPngAsync(Workflow workflow, string filePath);
    Task ExportSingleWorkflowAsJsonAsync(Workflow workflow, string filePath);
    Task<Workflow?> ImportSingleWorkflowFromJsonAsync(string filePath);
}
