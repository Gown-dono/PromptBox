namespace PromptBox.Models;

/// <summary>
/// Represents a prompt entry in the system
/// </summary>
public class Prompt
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public System.Collections.Generic.List<string> Tags { get; set; } = new();
    public string Content { get; set; } = string.Empty;
    public System.DateTime CreatedDate { get; set; } = System.DateTime.Now;
    public System.DateTime UpdatedDate { get; set; } = System.DateTime.Now;
}
