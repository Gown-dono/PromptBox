namespace PromptBox.Models;

/// <summary>
/// Represents a historical version of a prompt
/// </summary>
public class PromptVersion
{
    public int Id { get; set; }
    public int PromptId { get; set; }
    public int VersionNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public System.Collections.Generic.List<string> Tags { get; set; } = new();
    public string Content { get; set; } = string.Empty;
    public System.DateTime SavedDate { get; set; } = System.DateTime.Now;
    public string ChangeDescription { get; set; } = string.Empty;
}
