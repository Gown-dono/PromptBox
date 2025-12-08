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
}
