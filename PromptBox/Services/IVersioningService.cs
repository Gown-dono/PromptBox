using PromptBox.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PromptBox.Services;

/// <summary>
/// Interface for prompt versioning operations
/// </summary>
public interface IVersioningService
{
    Task SaveVersionAsync(Prompt prompt);
    Task<List<PromptVersion>> GetVersionsAsync(int promptId);
    Task<List<PromptVersion>> GetAllVersionsAsync();
    Task<PromptVersion?> GetVersionAsync(int versionId);
    Task DeleteVersionsForPromptAsync(int promptId);
    Task SaveVersionsAsync(List<PromptVersion> versions);
    string GetDiff(string oldContent, string newContent);
}
