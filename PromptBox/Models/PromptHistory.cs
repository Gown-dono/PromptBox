using System;

namespace PromptBox.Models;

/// <summary>
/// Represents a prompt history entry for tracking AI interactions
/// </summary>
public class PromptHistory
{
    public int Id { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public int TokensUsed { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool WasSuccessful { get; set; }
}

/// <summary>
/// AI conversation message
/// </summary>
public class AIMessage
{
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
