using PromptBox.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace PromptBox.Services;

/// <summary>
/// Service for interacting with various AI models
/// </summary>
public class AIService : IAIService
{
    private readonly ISecureStorageService _secureStorage;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromMinutes(5) };

    public AIService(ISecureStorageService secureStorage)
    {
        _secureStorage = secureStorage;
    }

    public async Task<AIResponse> GenerateAsync(string prompt, AIGenerationSettings settings)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var model = AIProviders.AvailableModels.FirstOrDefault(m => m.Id == settings.ModelId);
            if (model == null)
                return new AIResponse { Success = false, Error = "Model not found" };

            var apiKey = await _secureStorage.GetApiKeyAsync(model.Provider);
            if (string.IsNullOrEmpty(apiKey))
                return new AIResponse { Success = false, Error = $"No API key configured for {model.Provider}. Please go to Settings and enter your API key." };

            await _secureStorage.UpdateLastUsedAsync(model.Provider);

            var response = model.Provider switch
            {
                AIProviders.OpenAI or AIProviders.Groq => await CallOpenAICompatibleAsync(model, apiKey, prompt, settings),
                AIProviders.Anthropic => await CallAnthropicAsync(model, apiKey, prompt, settings),
                AIProviders.Google => await CallGoogleAsync(model, apiKey, prompt, settings),
                _ => new AIResponse { Success = false, Error = "Unsupported provider" }
            };

            stopwatch.Stop();
            response.Duration = stopwatch.Elapsed;
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new AIResponse { Success = false, Error = ex.Message, Duration = stopwatch.Elapsed };
        }
    }

    public async IAsyncEnumerable<string> GenerateStreamAsync(string prompt, AIGenerationSettings settings, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var model = AIProviders.AvailableModels.FirstOrDefault(m => m.Id == settings.ModelId);
        if (model == null)
        {
            yield return "[Error: Model not found]";
            yield break;
        }

        var apiKey = await _secureStorage.GetApiKeyAsync(model.Provider);
        if (string.IsNullOrEmpty(apiKey))
        {
            yield return $"[Error: No API key configured for {model.Provider}. Please go to Settings and enter your API key.]";
            yield break;
        }

        await _secureStorage.UpdateLastUsedAsync(model.Provider);

        IAsyncEnumerable<string>? streamEnumerable = model.Provider switch
        {
            AIProviders.OpenAI or AIProviders.Groq => 
                StreamOpenAICompatibleAsync(model, apiKey, prompt, settings, cancellationToken),
            AIProviders.Anthropic => 
                StreamAnthropicAsync(model, apiKey, prompt, settings, cancellationToken),
            AIProviders.Google => 
                StreamGoogleAsync(model, apiKey, prompt, settings, cancellationToken),
            _ => null
        };

        if (streamEnumerable == null)
        {
            yield return "[Error: Unsupported provider for streaming]";
            yield break;
        }

        await foreach (var chunk in streamEnumerable.WithCancellation(cancellationToken))
        {
            yield return chunk;
        }
    }

    public async Task<string> EnhancePromptAsync(string prompt, string enhancementType, AIGenerationSettings settings)
    {
        var systemPrompt = enhancementType switch
        {
            "clarity" => "You are a prompt engineering expert. Improve the clarity of the following prompt while maintaining its intent. Make it more specific and unambiguous. Return only the improved prompt, no explanations.",
            "detail" => "You are a prompt engineering expert. Add more detail and context to the following prompt to get better AI responses. Include relevant constraints, format requirements, and examples if helpful. Return only the improved prompt, no explanations.",
            "concise" => "You are a prompt engineering expert. Make the following prompt more concise while keeping all essential information. Remove redundancy and unnecessary words. Return only the improved prompt, no explanations.",
            "professional" => "You are a prompt engineering expert. Rewrite the following prompt in a more professional and formal tone. Return only the improved prompt, no explanations.",
            "creative" => "You are a prompt engineering expert. Make the following prompt more creative and engaging while maintaining its core purpose. Return only the improved prompt, no explanations.",
            "structured" => "You are a prompt engineering expert. Restructure the following prompt with clear sections, numbered steps if applicable, and better organization. Return only the improved prompt, no explanations.",
            _ => "You are a prompt engineering expert. Improve the following prompt. Return only the improved prompt, no explanations."
        };

        var enhancedSettings = new AIGenerationSettings
        {
            ModelId = settings.ModelId,
            Temperature = 0.5,
            MaxOutputTokens = settings.MaxOutputTokens,
            SystemPrompt = systemPrompt
        };

        var response = await GenerateAsync(prompt, enhancedSettings);
        return response.Success ? response.Content : prompt;
    }

    public async Task<PromptAnalysis> AnalyzePromptAsync(string prompt, AIGenerationSettings settings)
    {
        var analysisPrompt = $@"Analyze the following prompt and provide a JSON response with this exact structure:
{{
    ""qualityScore"": <number 0-100>,
    ""summary"": ""<brief summary of the prompt's purpose>"",
    ""strengths"": [""<strength 1>"", ""<strength 2>""],
    ""improvements"": [""<improvement 1>"", ""<improvement 2>""],
    ""suggestedAdditions"": [""<suggestion 1>"", ""<suggestion 2>""],
    ""clarityRating"": ""<Low/Medium/High>"",
    ""specificityRating"": ""<Low/Medium/High>""
}}

Prompt to analyze:
{prompt}

Return only valid JSON, no markdown or explanations.";

        var analysisSettings = new AIGenerationSettings
        {
            ModelId = settings.ModelId,
            Temperature = 0.3,
            MaxOutputTokens = 1024,
            SystemPrompt = "You are a prompt engineering expert. Analyze prompts and return structured JSON feedback. Return ONLY raw JSON, no markdown code blocks."
        };

        var response = await GenerateAsync(analysisPrompt, analysisSettings);
        
        if (!response.Success)
        {
            return new PromptAnalysis
            {
                QualityScore = 50,
                Summary = "Unable to analyze prompt",
                Improvements = new List<string> { response.Error ?? "Analysis failed" }
            };
        }

        try
        {
            // Extract JSON from potential markdown code blocks
            var jsonContent = ExtractJsonFromResponse(response.Content);
            
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                // AI didn't return valid JSON, try to extract useful info from text response
                return new PromptAnalysis
                {
                    QualityScore = 50,
                    Summary = "Analysis completed (non-JSON response)",
                    Improvements = new List<string> { response.Content.Length > 200 ? response.Content.Substring(0, 200) + "..." : response.Content }
                };
            }
            
            var json = JsonNode.Parse(jsonContent);
            if (json == null) throw new JsonException("Invalid JSON structure");

            return new PromptAnalysis
            {
                QualityScore = json["qualityScore"]?.GetValue<int>() ?? 50,
                Summary = json["summary"]?.GetValue<string>() ?? "",
                Strengths = json["strengths"]?.AsArray().Select(n => n?.GetValue<string>() ?? "").ToList() ?? new(),
                Improvements = json["improvements"]?.AsArray().Select(n => n?.GetValue<string>() ?? "").ToList() ?? new(),
                SuggestedAdditions = json["suggestedAdditions"]?.AsArray().Select(n => n?.GetValue<string>() ?? "").ToList() ?? new(),
                ClarityRating = json["clarityRating"]?.GetValue<string>() ?? "Medium",
                SpecificityRating = json["specificityRating"]?.GetValue<string>() ?? "Medium"
            };
        }
        catch (JsonException)
        {
            // JSON parsing failed, return the raw response as feedback
            return new PromptAnalysis
            {
                QualityScore = 50,
                Summary = "Analysis completed (format issue)",
                Improvements = new List<string> { response.Content.Length > 300 ? response.Content.Substring(0, 300) + "..." : response.Content }
            };
        }
        catch (Exception ex)
        {
            return new PromptAnalysis
            {
                QualityScore = 50,
                Summary = "Analysis parsing failed",
                Improvements = new List<string> { $"Error: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// Extracts JSON content from AI response, handling markdown code blocks.
    /// Uses brace-counting as a last resort for responses with extra commentary around JSON.
    /// Note: This approach may still fail on malformed responses or JSON within strings.
    /// </summary>
    private static string? ExtractJsonFromResponse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var trimmed = content.Trim();
        
        // Handle ```json ... ``` format
        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            var startIndex = trimmed.IndexOf('\n') + 1;
            var endIndex = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (startIndex > 0 && endIndex > startIndex)
            {
                return trimmed.Substring(startIndex, endIndex - startIndex).Trim();
            }
        }
        
        // Handle ``` ... ``` format (without language specifier)
        if (trimmed.StartsWith("```") && !trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            var startIndex = trimmed.IndexOf('\n') + 1;
            var endIndex = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (startIndex > 0 && endIndex > startIndex)
            {
                return trimmed.Substring(startIndex, endIndex - startIndex).Trim();
            }
        }
        
        // Last resort: Use brace-counting to find a balanced JSON object.
        // This handles responses with extra commentary before/after the JSON.
        var firstBrace = trimmed.IndexOf('{');
        if (firstBrace >= 0)
        {
            int braceCount = 0;
            bool inString = false;
            bool escape = false;
            
            for (int i = firstBrace; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                
                if (escape)
                {
                    escape = false;
                    continue;
                }
                
                if (c == '\\' && inString)
                {
                    escape = true;
                    continue;
                }
                
                if (c == '"' && !escape)
                {
                    inString = !inString;
                    continue;
                }
                
                if (!inString)
                {
                    if (c == '{')
                        braceCount++;
                    else if (c == '}')
                        braceCount--;
                    
                    // Found balanced JSON object
                    if (braceCount == 0)
                    {
                        return trimmed.Substring(firstBrace, i - firstBrace + 1);
                    }
                }
            }
        }

        // No valid JSON found
        return null;
    }


    public async Task<List<string>> GenerateVariationsAsync(string prompt, int count, AIGenerationSettings settings)
    {
        var variationPrompt = $@"Generate {count} different variations of the following prompt. Each variation should maintain the core intent but approach it differently (different tone, structure, or emphasis).

Original prompt:
{prompt}

Return each variation on a new line, numbered 1-{count}. No explanations, just the variations.";

        var variationSettings = new AIGenerationSettings
        {
            ModelId = settings.ModelId,
            Temperature = 0.8,
            MaxOutputTokens = settings.MaxOutputTokens * count,
            SystemPrompt = "You are a prompt engineering expert. Generate creative variations of prompts."
        };

        var response = await GenerateAsync(variationPrompt, variationSettings);
        
        if (!response.Success)
            return new List<string> { prompt };

        var lines = response.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var variations = new List<string>();
        
        foreach (var line in lines)
        {
            var cleaned = System.Text.RegularExpressions.Regex.Replace(line.Trim(), @"^\d+[\.\)]\s*", "");
            if (!string.IsNullOrWhiteSpace(cleaned))
                variations.Add(cleaned);
        }

        return variations.Take(count).ToList();
    }

    public async Task<List<string>> GetSmartSuggestionsAsync(string prompt, AIGenerationSettings settings)
    {
        var (suggestions, _) = await GetSmartSuggestionsWithErrorAsync(prompt, settings);
        return suggestions;
    }

    public async Task<(List<string> Suggestions, string? Error)> GetSmartSuggestionsWithErrorAsync(string prompt, AIGenerationSettings settings)
    {
        var suggestionPrompt = $@"Analyze the following prompt and generate 3-5 improved versions. Each suggestion should be a complete, ready-to-use prompt that improves upon the original in different ways:
- Better clarity and specificity
- More detailed instructions
- Better structure and organization
- Added context or constraints

Original prompt:
{prompt}

Return each improved prompt on a separate line, numbered 1-5. Each should be a complete, standalone prompt. No explanations, just the improved prompts.";

        var suggestionSettings = new AIGenerationSettings
        {
            ModelId = settings.ModelId,
            Temperature = 0.7,
            MaxOutputTokens = 2048,
            SystemPrompt = "You are a prompt engineering expert. Generate improved versions of prompts that are clearer, more specific, and more effective."
        };

        var response = await GenerateAsync(suggestionPrompt, suggestionSettings);
        
        if (!response.Success)
            return (new List<string>(), response.Error);

        var lines = response.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var suggestions = new List<string>();
        
        foreach (var line in lines)
        {
            var cleaned = System.Text.RegularExpressions.Regex.Replace(line.Trim(), @"^\d+[\.\)]\s*", "");
            if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length > 10)
                suggestions.Add(cleaned);
        }

        return (suggestions.Take(5).ToList(), null);
    }

    public async Task<bool> ValidateApiKeyAsync(string provider, string apiKey)
    {
        try
        {
            var model = AIProviders.AvailableModels.FirstOrDefault(m => m.Provider == provider);
            if (model == null) return false;

            var endpoint = GetValidationEndpoint(provider);
            if (string.IsNullOrEmpty(endpoint)) return false;

            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            AddAuthHeaders(request, provider, apiKey);

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<AIModel>> GetAvailableModelsAsync()
    {
        var storedProviders = await _secureStorage.GetStoredProvidersAsync();
        return AIProviders.AvailableModels
            .Where(m => storedProviders.Contains(m.Provider))
            .ToList();
    }

    private string GetValidationEndpoint(string provider) => provider switch
    {
        AIProviders.OpenAI => "https://api.openai.com/v1/models",
        AIProviders.Anthropic => "https://api.anthropic.com/v1/models",
        AIProviders.Google => "https://generativelanguage.googleapis.com/v1beta/models",
        AIProviders.Groq => "https://api.groq.com/openai/v1/models",
        _ => ""
    };

    private void AddAuthHeaders(HttpRequestMessage request, string provider, string apiKey)
    {
        switch (provider)
        {
            case AIProviders.OpenAI:
            case AIProviders.Groq:
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                break;
            case AIProviders.Anthropic:
                request.Headers.Add("x-api-key", apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                break;
            case AIProviders.Google:
                // Google uses query parameter for API key
                var uriBuilder = new UriBuilder(request.RequestUri!);
                uriBuilder.Query = $"key={apiKey}";
                request.RequestUri = uriBuilder.Uri;
                break;
        }
    }


    private async Task<AIResponse> CallOpenAICompatibleAsync(AIModel model, string apiKey, string prompt, AIGenerationSettings settings)
    {
        var messages = new List<object>();
        
        if (!string.IsNullOrEmpty(settings.SystemPrompt))
            messages.Add(new { role = "system", content = settings.SystemPrompt });
        
        messages.Add(new { role = "user", content = prompt });

        var requestBody = new
        {
            model = model.Id,
            messages = messages,
            temperature = settings.Temperature,
            max_tokens = settings.MaxOutputTokens
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, model.ApiEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return new AIResponse { Success = false, Error = $"API Error: {content}" };

        var json = JsonNode.Parse(content);
        var messageContent = json?["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? "";
        var tokensUsed = json?["usage"]?["total_tokens"]?.GetValue<int>() ?? 0;

        return new AIResponse { Success = true, Content = messageContent, TokensUsed = tokensUsed };
    }

    private async Task<AIResponse> CallAnthropicAsync(AIModel model, string apiKey, string prompt, AIGenerationSettings settings)
    {
        var requestBody = new
        {
            model = model.Id,
            max_tokens = settings.MaxOutputTokens,
            system = settings.SystemPrompt ?? "",
            messages = new[] { new { role = "user", content = prompt } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, model.ApiEndpoint);
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return new AIResponse { Success = false, Error = $"API Error: {content}" };

        var json = JsonNode.Parse(content);
        var messageContent = json?["content"]?[0]?["text"]?.GetValue<string>() ?? "";
        var inputTokens = json?["usage"]?["input_tokens"]?.GetValue<int>() ?? 0;
        var outputTokens = json?["usage"]?["output_tokens"]?.GetValue<int>() ?? 0;

        return new AIResponse { Success = true, Content = messageContent, TokensUsed = inputTokens + outputTokens };
    }

    private async Task<AIResponse> CallGoogleAsync(AIModel model, string apiKey, string prompt, AIGenerationSettings settings)
    {
        var endpoint = $"{model.ApiEndpoint}/{model.Id}:generateContent?key={apiKey}";
        
        var contents = new List<object>();
        
        if (!string.IsNullOrEmpty(settings.SystemPrompt))
        {
            contents.Add(new { role = "user", parts = new[] { new { text = settings.SystemPrompt } } });
            contents.Add(new { role = "model", parts = new[] { new { text = "Understood. I will follow these instructions." } } });
        }
        
        contents.Add(new { role = "user", parts = new[] { new { text = prompt } } });

        var requestBody = new
        {
            contents = contents,
            generationConfig = new
            {
                temperature = settings.Temperature,
                maxOutputTokens = settings.MaxOutputTokens
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return new AIResponse { Success = false, Error = $"API Error: {content}" };

        var json = JsonNode.Parse(content);
        var messageContent = json?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>() ?? "";
        var tokensUsed = json?["usageMetadata"]?["totalTokenCount"]?.GetValue<int>() ?? 0;

        return new AIResponse { Success = true, Content = messageContent, TokensUsed = tokensUsed };
    }

    private async IAsyncEnumerable<string> StreamOpenAICompatibleAsync(AIModel model, string apiKey, string prompt, AIGenerationSettings settings, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var messages = new List<object>();
        
        if (!string.IsNullOrEmpty(settings.SystemPrompt))
            messages.Add(new { role = "system", content = settings.SystemPrompt });
        
        messages.Add(new { role = "user", content = prompt });

        var requestBody = new
        {
            model = model.Id,
            messages = messages,
            temperature = settings.Temperature,
            max_tokens = settings.MaxOutputTokens,
            stream = true
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, model.ApiEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            yield return $"[Error: {await response.Content.ReadAsStringAsync(cancellationToken)}]";
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new System.IO.StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;
            
            var data = line[6..];
            if (data == "[DONE]") break;

            var content = ParseOpenAIStreamChunk(data);
            if (!string.IsNullOrEmpty(content))
                yield return content;
        }
    }

    private static string? ParseOpenAIStreamChunk(string data)
    {
        try
        {
            var json = JsonNode.Parse(data);
            return json?["choices"]?[0]?["delta"]?["content"]?.GetValue<string>();
        }
        catch (JsonException)
        {
            // Skip malformed JSON chunks - they occasionally occur in streaming
            return null;
        }
    }

    private async IAsyncEnumerable<string> StreamAnthropicAsync(AIModel model, string apiKey, string prompt, AIGenerationSettings settings, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = model.Id,
            max_tokens = settings.MaxOutputTokens,
            system = settings.SystemPrompt ?? "",
            messages = new[] { new { role = "user", content = prompt } },
            stream = true
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, model.ApiEndpoint);
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            yield return $"[Error: {await response.Content.ReadAsStringAsync(cancellationToken)}]";
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new System.IO.StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;
            
            var data = line[6..];
            
            var content = ParseAnthropicStreamChunk(data);
            if (!string.IsNullOrEmpty(content))
                yield return content;
        }
    }

    private static string? ParseAnthropicStreamChunk(string data)
    {
        try
        {
            var json = JsonNode.Parse(data);
            
            if (json?["type"]?.GetValue<string>() == "content_block_delta")
            {
                return json["delta"]?["text"]?.GetValue<string>();
            }
            return null;
        }
        catch (JsonException)
        {
            // Skip malformed JSON chunks
            return null;
        }
    }

    private async IAsyncEnumerable<string> StreamGoogleAsync(AIModel model, string apiKey, string prompt, AIGenerationSettings settings, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var endpoint = $"{model.ApiEndpoint}/{model.Id}:streamGenerateContent?alt=sse&key={apiKey}";
        
        var contents = new List<object>();
        
        if (!string.IsNullOrEmpty(settings.SystemPrompt))
        {
            contents.Add(new { role = "user", parts = new[] { new { text = settings.SystemPrompt } } });
            contents.Add(new { role = "model", parts = new[] { new { text = "Understood. I will follow these instructions." } } });
        }
        
        contents.Add(new { role = "user", parts = new[] { new { text = prompt } } });

        var requestBody = new
        {
            contents = contents,
            generationConfig = new
            {
                temperature = settings.Temperature,
                maxOutputTokens = settings.MaxOutputTokens
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            yield return $"[Error: {await response.Content.ReadAsStringAsync(cancellationToken)}]";
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new System.IO.StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;
            
            var data = line[6..];
            
            var content = ParseGoogleStreamChunk(data);
            if (!string.IsNullOrEmpty(content))
                yield return content;
        }
    }

    private static string? ParseGoogleStreamChunk(string data)
    {
        try
        {
            var json = JsonNode.Parse(data);
            return json?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();
        }
        catch (JsonException)
        {
            // Skip malformed JSON chunks
            return null;
        }
    }
}
