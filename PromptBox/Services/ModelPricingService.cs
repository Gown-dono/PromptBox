using System.Collections.Generic;
using System.Linq;
using PromptBox.Models;

namespace PromptBox.Services;

/// <summary>
/// Provides AI model pricing information.
/// Pricing data is centralized here for easy updates without modifying business logic.
/// 
/// PRICING NOTES:
/// - Costs are blended estimates assuming ~30% input, ~70% output token ratio
/// - Actual costs vary: input tokens are cheaper, output tokens are more expensive
/// - Update this file when provider pricing changes
/// </summary>
public class ModelPricingService : IModelPricingService
{
    /// <summary>
    /// Last updated: December 2025
    /// </summary>
    public string PricingLastUpdated => "December 2025";

    /// <summary>
    /// Default fallback: $2/1M tokens as conservative estimate
    /// </summary>
    public double DefaultCostPer1KTokens => 0.002;

    private readonly Dictionary<string, ModelPricing> _pricingData;

    public ModelPricingService()
    {
        _pricingData = InitializePricingData().ToDictionary(p => p.ModelId);
    }

    public double GetCostPer1KTokens(string modelId)
    {
        return _pricingData.TryGetValue(modelId, out var pricing)
            ? pricing.CostPer1KTokens
            : DefaultCostPer1KTokens;
    }

    public ModelPricing? GetModelPricing(string modelId)
    {
        return _pricingData.TryGetValue(modelId, out var pricing) ? pricing : null;
    }

    public IReadOnlyList<ModelPricing> GetAllPricing()
    {
        return _pricingData.Values.ToList();
    }

    private static List<ModelPricing> InitializePricingData()
    {
        return new List<ModelPricing>
        {
            // OpenAI models (December 2025 pricing)
            new() { ModelId = "gpt-5", CostPer1KTokens = 0.015, Notes = "Premium reasoning model" },
            new() { ModelId = "gpt-5-mini", CostPer1KTokens = 0.005, Notes = "Smaller reasoning model" },
            new() { ModelId = "gpt-4o", CostPer1KTokens = 0.0078, InputCostPer1M = 2.50, OutputCostPer1M = 10.00 },
            new() { ModelId = "gpt-4o-mini", CostPer1KTokens = 0.00047, InputCostPer1M = 0.15, OutputCostPer1M = 0.60 },
            new() { ModelId = "gpt-4-turbo", CostPer1KTokens = 0.024, InputCostPer1M = 10.00, OutputCostPer1M = 30.00 },
            new() { ModelId = "gpt-3.5-turbo", CostPer1KTokens = 0.0012, InputCostPer1M = 0.50, OutputCostPer1M = 1.50 },

            // Anthropic models (December 2025 pricing)
            new() { ModelId = "claude-sonnet-4-5-20250929", CostPer1KTokens = 0.015, Notes = "Smartest Claude - premium pricing" },
            new() { ModelId = "claude-opus-4-20250514", CostPer1KTokens = 0.045, InputCostPer1M = 15.00, OutputCostPer1M = 75.00 },
            new() { ModelId = "claude-sonnet-4-20250514", CostPer1KTokens = 0.0114, InputCostPer1M = 3.00, OutputCostPer1M = 15.00 },
            new() { ModelId = "claude-haiku-4-5-20251001", CostPer1KTokens = 0.0038, Notes = "Fast, cost-effective" },
            new() { ModelId = "claude-3-5-sonnet-20241022", CostPer1KTokens = 0.0114, InputCostPer1M = 3.00, OutputCostPer1M = 15.00 },
            new() { ModelId = "claude-3-5-haiku-20241022", CostPer1KTokens = 0.0038, InputCostPer1M = 1.00, OutputCostPer1M = 5.00 },

            // Google models (December 2025 pricing)
            new() { ModelId = "gemini-2.5-pro", CostPer1KTokens = 0.0075, InputCostPer1M = 1.25, OutputCostPer1M = 10.00, Notes = "<=200K context" },
            new() { ModelId = "gemini-2.5-flash", CostPer1KTokens = 0.0005, InputCostPer1M = 0.15, OutputCostPer1M = 0.60, Notes = "Thinking off" },
            new() { ModelId = "gemini-2.5-flash-lite", CostPer1KTokens = 0.0002, Notes = "Lightweight, very low cost" },
            new() { ModelId = "gemini-2.0-flash", CostPer1KTokens = 0.0003, InputCostPer1M = 0.10, OutputCostPer1M = 0.40 },
            new() { ModelId = "gemini-1.5-pro", CostPer1KTokens = 0.00388, InputCostPer1M = 1.25, OutputCostPer1M = 5.00 },
            new() { ModelId = "gemini-1.5-flash", CostPer1KTokens = 0.000233, InputCostPer1M = 0.075, OutputCostPer1M = 0.30 },

            // Groq models (December 2025 pricing - very competitive)
            new() { ModelId = "llama-3.3-70b-versatile", CostPer1KTokens = 0.00073, InputCostPer1M = 0.59, OutputCostPer1M = 0.79 },
            new() { ModelId = "llama-3.1-8b-instant", CostPer1KTokens = 0.00008, InputCostPer1M = 0.05, OutputCostPer1M = 0.08 },
            new() { ModelId = "meta-llama/llama-4-scout-17b-16e-instruct", CostPer1KTokens = 0.00015, Notes = "Llama 4 Scout" },
            new() { ModelId = "meta-llama/llama-4-maverick-17b-128e-instruct", CostPer1KTokens = 0.0005, Notes = "Llama 4 Maverick" },
            new() { ModelId = "qwen/qwen3-32b", CostPer1KTokens = 0.0003, Notes = "Qwen3 reasoning model" },
            new() { ModelId = "openai/gpt-oss-120b", CostPer1KTokens = 0.0008, Notes = "Large open-source model" },
            new() { ModelId = "moonshotai/kimi-k2-instruct", CostPer1KTokens = 0.0006, Notes = "Moonshot AI model" },
            new() { ModelId = "mixtral-8x7b-32768", CostPer1KTokens = 0.00024, InputCostPer1M = 0.24, OutputCostPer1M = 0.24 },
        };
    }
}
