using System;
using System.Collections.Generic;

namespace PromptBox.Models;

/// <summary>
/// Represents a complete prompt comparison session with metadata
/// </summary>
public class PromptComparisonSession
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SharedInput { get; set; } = string.Empty;
    public List<PromptVariation> PromptVariations { get; set; } = new();
    public List<string> ModelIds { get; set; } = new();
    public List<ComparisonResult> Results { get; set; } = new();
    public string? WinnerVariationName { get; set; }
    public string? WinnerModelId { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? CompletedDate { get; set; }
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2048;
}
