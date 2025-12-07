using PromptBox.Models;

namespace PromptBox.Services;

/// <summary>
/// Interface for prompt versioning operations
/// </summary>
public interface IVersioningService
{
    System.Threading.Tasks.Task SaveVersionAsync(Prompt prompt);
    System.Threading.Tasks.Task<System.Collections.Generic.List<PromptVersion>> GetVersionsAsync(int promptId);
    System.Threading.Tasks.Task<PromptVersion?> GetVersionAsync(int versionId);
    System.Threading.Tasks.Task DeleteVersionsForPromptAsync(int promptId);
    string GetDiff(string oldContent, string newContent);
}
