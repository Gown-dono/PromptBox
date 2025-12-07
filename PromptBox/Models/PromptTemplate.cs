namespace PromptBox.Models;

/// <summary>
/// Represents a pre-defined prompt template from the library
/// </summary>
public class PromptTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public System.Collections.Generic.List<string> Tags { get; set; } = new();
    public string Content { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = "PromptBox";
    public string Version { get; set; } = "1.0";
}
