namespace PromptBox.Models;

/// <summary>
/// Represents pricing information for an AI model.
/// Pricing is expressed as cost per 1K tokens (blended input/output estimate).
/// </summary>
public class ModelPricing
{
    /// <summary>
    /// The model identifier (matches AIModel.Id)
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Blended cost per 1K tokens (assumes ~30% input, ~70% output ratio)
    /// </summary>
    public double CostPer1KTokens { get; set; }

    /// <summary>
    /// Cost per 1M input tokens (for reference)
    /// </summary>
    public double? InputCostPer1M { get; set; }

    /// <summary>
    /// Cost per 1M output tokens (for reference)
    /// </summary>
    public double? OutputCostPer1M { get; set; }

    /// <summary>
    /// Optional notes about the pricing
    /// </summary>
    public string? Notes { get; set; }
}
