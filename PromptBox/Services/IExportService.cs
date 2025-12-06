using PromptBox.Models;

namespace PromptBox.Services;

public interface IExportService
{
    System.Threading.Tasks.Task ExportPromptAsMarkdownAsync(Prompt prompt, string filePath);
    System.Threading.Tasks.Task ExportPromptAsTextAsync(Prompt prompt, string filePath);
    System.Threading.Tasks.Task ExportAllPromptsAsJsonAsync(System.Collections.Generic.List<Prompt> prompts, string filePath);
    System.Threading.Tasks.Task<System.Collections.Generic.List<Prompt>> ImportPromptsFromJsonAsync(string filePath);
}
