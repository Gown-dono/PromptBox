using PromptBox.Models;

namespace PromptBox.Services;

public interface IDatabaseService
{
    System.Threading.Tasks.Task<System.Collections.Generic.List<Prompt>> GetAllPromptsAsync();
    System.Threading.Tasks.Task<Prompt?> GetPromptByIdAsync(int id);
    System.Threading.Tasks.Task<int> SavePromptAsync(Prompt prompt);
    System.Threading.Tasks.Task<bool> DeletePromptAsync(int id);
    System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GetAllCategoriesAsync();
    System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GetAllTagsAsync();
}
