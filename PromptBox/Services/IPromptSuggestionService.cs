using System.Collections.Generic;

namespace PromptBox.Services;

/// <summary>
/// Interface for intelligent prompt suggestions
/// </summary>
public interface IPromptSuggestionService
{
    /// <summary>
    /// Gets quick suggestions for improving a prompt based on patterns
    /// </summary>
    List<PromptSuggestion> GetQuickSuggestions(string prompt);
    
    /// <summary>
    /// Gets prompt starters for common use cases
    /// </summary>
    List<PromptStarter> GetPromptStarters();
}

/// <summary>
/// A suggestion for improving a prompt
/// </summary>
public class PromptSuggestion
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SuggestedText { get; set; } = string.Empty;
    public SuggestionType Type { get; set; }
}

/// <summary>
/// A prompt starter template
/// </summary>
public class PromptStarter
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}

public enum SuggestionType
{
    AddContext,
    AddFormat,
    AddConstraints,
    AddExamples,
    AddRole,
    AddTone
}
