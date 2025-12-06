using PromptBox.Models;

namespace PromptBox.Services;

public interface ISearchService
{
    System.Collections.Generic.List<Prompt> Search(System.Collections.Generic.List<Prompt> prompts, string searchQuery);
    System.Collections.Generic.List<Prompt> FilterByCategory(System.Collections.Generic.List<Prompt> prompts, string category);
    System.Collections.Generic.List<Prompt> FilterByTag(System.Collections.Generic.List<Prompt> prompts, string tag);
}
