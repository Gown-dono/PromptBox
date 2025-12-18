using System;

namespace PromptBox.Models;

/// <summary>
/// Represents a user rating for a prompt template
/// </summary>
public class TemplateRating
{
    public int Id { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public int Rating { get; set; } // 1-5 stars
    public string Comment { get; set; } = string.Empty; // Optional review text, max 500 chars
    public string UserIdentifier { get; set; } = string.Empty; // Anonymous hash or username
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public bool IsSynced { get; set; } = false; // Whether uploaded to community API
}
