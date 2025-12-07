using PromptBox.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PromptBox.Services;

/// <summary>
/// Interface for AI model interactions
/// </summary>
public interface IAIService
{
    /// <summary>
    /// Sends a prompt to the AI and returns the response
    /// </summary>
    System.Threading.Tasks.Task<AIResponse> GenerateAsync(string prompt, AIGenerationSettings settings);
    
    /// <summary>
    /// Sends a prompt with streaming response
    /// </summary>
    IAsyncEnumerable<string> GenerateStreamAsync(string prompt, AIGenerationSettings settings, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Enhances a prompt using AI
    /// </summary>
    System.Threading.Tasks.Task<string> EnhancePromptAsync(string prompt, string enhancementType, AIGenerationSettings settings);
    
    /// <summary>
    /// Analyzes prompt quality and provides suggestions
    /// </summary>
    System.Threading.Tasks.Task<PromptAnalysis> AnalyzePromptAsync(string prompt, AIGenerationSettings settings);
    
    /// <summary>
    /// Generates prompt variations
    /// </summary>
    System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GenerateVariationsAsync(string prompt, int count, AIGenerationSettings settings);
    
    /// <summary>
    /// Tests if an API key is valid for a provider
    /// </summary>
    System.Threading.Tasks.Task<bool> ValidateApiKeyAsync(string provider, string apiKey);
    
    /// <summary>
    /// Gets available models for providers with valid API keys
    /// </summary>
    System.Threading.Tasks.Task<System.Collections.Generic.List<AIModel>> GetAvailableModelsAsync();
    
    /// <summary>
    /// Gets AI-powered smart suggestions to improve a prompt
    /// </summary>
    System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GetSmartSuggestionsAsync(string prompt, AIGenerationSettings settings);
    
    /// <summary>
    /// Gets AI-powered smart suggestions with error information
    /// </summary>
    System.Threading.Tasks.Task<(System.Collections.Generic.List<string> Suggestions, string? Error)> GetSmartSuggestionsWithErrorAsync(string prompt, AIGenerationSettings settings);
}

/// <summary>
/// Response from AI generation
/// </summary>
public class AIResponse
{
    public bool Success { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Error { get; set; }
    public int TokensUsed { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Prompt quality analysis result
/// </summary>
public class PromptAnalysis
{
    public int QualityScore { get; set; } // 0-100
    public string Summary { get; set; } = string.Empty;
    public System.Collections.Generic.List<string> Strengths { get; set; } = new();
    public System.Collections.Generic.List<string> Improvements { get; set; } = new();
    public System.Collections.Generic.List<string> SuggestedAdditions { get; set; } = new();
    public string ClarityRating { get; set; } = string.Empty;
    public string SpecificityRating { get; set; } = string.Empty;
}
