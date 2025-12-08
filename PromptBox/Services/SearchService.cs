using PromptBox.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PromptBox.Services;

/// <summary>
/// Service for searching and filtering prompts
/// </summary>
public class SearchService : ISearchService
{
    public List<Prompt> Search(List<Prompt> prompts, string searchQuery)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
            return prompts;

        var query = searchQuery.ToLowerInvariant();
        
        return prompts.Where(p =>
            (p.Title ?? string.Empty).ToLowerInvariant().Contains(query) ||
            (p.Category ?? string.Empty).ToLowerInvariant().Contains(query) ||
            (p.Content ?? string.Empty).ToLowerInvariant().Contains(query) ||
            (p.Tags ?? Enumerable.Empty<string>()).Any(t => (t ?? string.Empty).ToLowerInvariant().Contains(query))
        ).ToList();
    }

    public List<Prompt> FilterByCategory(List<Prompt> prompts, string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return prompts;

        return prompts.Where(p => 
            (p.Category ?? string.Empty).Equals(category, StringComparison.OrdinalIgnoreCase)
        ).ToList();
    }

    public List<Prompt> FilterByTag(List<Prompt> prompts, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return prompts;

        return prompts.Where(p => 
            (p.Tags ?? Enumerable.Empty<string>()).Any(t => (t ?? string.Empty).Equals(tag, StringComparison.OrdinalIgnoreCase))
        ).ToList();
    }
}
