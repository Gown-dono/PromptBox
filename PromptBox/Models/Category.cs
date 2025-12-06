namespace PromptBox.Models;

/// <summary>
/// Represents a category for organizing prompts
/// </summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PromptCount { get; set; }
}
