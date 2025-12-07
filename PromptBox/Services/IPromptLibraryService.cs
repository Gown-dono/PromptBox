using PromptBox.Models;

namespace PromptBox.Services;

/// <summary>
/// Interface for the prompt library service
/// </summary>
public interface IPromptLibraryService
{
    System.Collections.Generic.List<PromptTemplate> GetAllTemplates();
    System.Collections.Generic.List<PromptTemplate> GetTemplatesByCategory(string category);
    System.Collections.Generic.List<string> GetCategories();
    PromptTemplate? GetTemplateById(string id);
    System.Collections.Generic.List<PromptTemplate> SearchTemplates(string query);
}
