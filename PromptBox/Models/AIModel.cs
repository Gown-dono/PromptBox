using System;

namespace PromptBox.Models;

/// <summary>
/// Represents an AI model provider and its configuration
/// </summary>
public class AIModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ApiEndpoint { get; set; } = string.Empty;
    public int MaxTokens { get; set; } = 4096;
    public double DefaultTemperature { get; set; } = 0.7;
    public bool SupportsStreaming { get; set; } = true;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Supported AI providers
/// </summary>
public static class AIProviders
{
    public const string OpenAI = "OpenAI";
    public const string Anthropic = "Anthropic";
    public const string Google = "Google";
    public const string Groq = "Groq";
    
    public static readonly AIModel[] AvailableModels = new[]
    {
        // OpenAI Models (December 2025) - Reasoning models
        new AIModel { Id = "gpt-5", Name = "GPT-5", Provider = OpenAI, ApiEndpoint = "https://api.openai.com/v1/chat/completions", MaxTokens = 256000, Description = "Full-size reasoning model" },
        new AIModel { Id = "gpt-5-mini", Name = "GPT-5 Mini", Provider = OpenAI, ApiEndpoint = "https://api.openai.com/v1/chat/completions", MaxTokens = 128000, Description = "Smaller, faster reasoning model" },
        new AIModel { Id = "gpt-4o", Name = "GPT-4o", Provider = OpenAI, ApiEndpoint = "https://api.openai.com/v1/chat/completions", MaxTokens = 128000, Description = "Multimodal model" },
        new AIModel { Id = "gpt-4o-mini", Name = "GPT-4o Mini", Provider = OpenAI, ApiEndpoint = "https://api.openai.com/v1/chat/completions", MaxTokens = 128000, Description = "Cost-effective multimodal" },
        
        // Anthropic Models (December 2025)
        new AIModel { Id = "claude-sonnet-4-5-20250929", Name = "Claude Sonnet 4.5", Provider = Anthropic, ApiEndpoint = "https://api.anthropic.com/v1/messages", MaxTokens = 200000, Description = "Smartest Claude model" },
        new AIModel { Id = "claude-opus-4-20250514", Name = "Claude Opus 4", Provider = Anthropic, ApiEndpoint = "https://api.anthropic.com/v1/messages", MaxTokens = 200000, Description = "Most powerful Claude" },
        new AIModel { Id = "claude-sonnet-4-20250514", Name = "Claude Sonnet 4", Provider = Anthropic, ApiEndpoint = "https://api.anthropic.com/v1/messages", MaxTokens = 200000, Description = "Balanced performance" },
        new AIModel { Id = "claude-haiku-4-5-20251001", Name = "Claude Haiku 4.5", Provider = Anthropic, ApiEndpoint = "https://api.anthropic.com/v1/messages", MaxTokens = 200000, Description = "Fast, cost-effective" },
        new AIModel { Id = "claude-3-5-sonnet-20241022", Name = "Claude 3.5 Sonnet", Provider = Anthropic, ApiEndpoint = "https://api.anthropic.com/v1/messages", MaxTokens = 200000, Description = "Best for complex tasks" },
        
        // Google Models (December 2025)
        new AIModel { Id = "gemini-2.5-pro", Name = "Gemini 2.5 Pro", Provider = Google, ApiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models", MaxTokens = 1048576, Description = "State-of-the-art thinking model" },
        new AIModel { Id = "gemini-2.5-flash", Name = "Gemini 2.5 Flash", Provider = Google, ApiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models", MaxTokens = 1048576, Description = "Fast with thinking support" },
        new AIModel { Id = "gemini-2.5-flash-lite", Name = "Gemini 2.5 Flash-Lite", Provider = Google, ApiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models", MaxTokens = 1048576, Description = "Lightweight, efficient" },
        new AIModel { Id = "gemini-2.0-flash", Name = "Gemini 2.0 Flash", Provider = Google, ApiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models", MaxTokens = 1048576, Description = "1M context, fast" },
        
        // Groq Models (fast inference) - December 2025
        new AIModel { Id = "llama-3.3-70b-versatile", Name = "Llama 3.3 70B", Provider = Groq, ApiEndpoint = "https://api.groq.com/openai/v1/chat/completions", MaxTokens = 128000, Description = "Fast Llama inference" },
        new AIModel { Id = "llama-3.1-8b-instant", Name = "Llama 3.1 8B", Provider = Groq, ApiEndpoint = "https://api.groq.com/openai/v1/chat/completions", MaxTokens = 131072, Description = "Fast, lightweight inference" },
        new AIModel { Id = "meta-llama/llama-4-scout-17b-16e-instruct", Name = "Llama 4 Scout", Provider = Groq, ApiEndpoint = "https://api.groq.com/openai/v1/chat/completions", MaxTokens = 128000, Description = "Multimodal, 750 tps" },
        new AIModel { Id = "meta-llama/llama-4-maverick-17b-128e-instruct", Name = "Llama 4 Maverick", Provider = Groq, ApiEndpoint = "https://api.groq.com/openai/v1/chat/completions", MaxTokens = 128000, Description = "Multimodal, 600 tps" },
        new AIModel { Id = "qwen/qwen3-32b", Name = "Qwen3 32B", Provider = Groq, ApiEndpoint = "https://api.groq.com/openai/v1/chat/completions", MaxTokens = 32768, Description = "Reasoning model" },
        new AIModel { Id = "openai/gpt-oss-120b", Name = "GPT-OSS 120B", Provider = Groq, ApiEndpoint = "https://api.groq.com/openai/v1/chat/completions", MaxTokens = 128000, Description = "Large open-source model" },
        new AIModel { Id = "moonshotai/kimi-k2-instruct", Name = "Kimi K2", Provider = Groq, ApiEndpoint = "https://api.groq.com/openai/v1/chat/completions", MaxTokens = 131072, Description = "Moonshot AI model" },
    };
}

/// <summary>
/// Stored API key configuration
/// </summary>
public class APIKeyConfig
{
    public int Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string EncryptedKey { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? LastUsedDate { get; set; }
    public bool IsValid { get; set; } = true;
}

/// <summary>
/// AI generation settings
/// </summary>
public class AIGenerationSettings
{
    public string ModelId { get; set; } = "gpt-4o-mini";
    public double Temperature { get; set; } = 0.7;
    public int MaxOutputTokens { get; set; } = 2048;
    public string SystemPrompt { get; set; } = string.Empty;
}
