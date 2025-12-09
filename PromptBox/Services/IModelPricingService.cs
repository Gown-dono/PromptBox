using System.Collections.Generic;
using PromptBox.Models;

namespace PromptBox.Services;

/// <summary>
/// Service for retrieving AI model pricing information.
/// Centralizes pricing data to avoid hardcoding in business logic.
/// </summary>
public interface IModelPricingService
{
    /// <summary>
    /// Gets the blended cost per 1K tokens for a model.
    /// Returns a default fallback rate if the model is not found.
    /// </summary>
    double GetCostPer1KTokens(string modelId);

    /// <summary>
    /// Gets full pricing information for a model, or null if not found.
    /// </summary>
    ModelPricing? GetModelPricing(string modelId);

    /// <summary>
    /// Gets all available model pricing data.
    /// </summary>
    IReadOnlyList<ModelPricing> GetAllPricing();

    /// <summary>
    /// Gets the default fallback cost per 1K tokens for unknown models.
    /// </summary>
    double DefaultCostPer1KTokens { get; }

    /// <summary>
    /// Gets the date when pricing data was last updated.
    /// </summary>
    string PricingLastUpdated { get; }
}
