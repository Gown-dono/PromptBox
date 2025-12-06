namespace PromptBox.Models;

/// <summary>
/// Represents a tag for categorizing prompts
/// </summary>
public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}
