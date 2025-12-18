using System;

namespace PromptBox.Models;

/// <summary>
/// Represents a cached community template for offline access
/// </summary>
public class CachedCommunityTemplate
{
    public string TemplateId { get; set; } = string.Empty;
    public string JsonData { get; set; } = string.Empty; // Serialized PromptTemplate
    public DateTime CachedDate { get; set; } = DateTime.Now;
    public DateTime ExpiresAt { get; set; } = DateTime.Now.AddHours(24); // TTL: 24 hours
    public string ETag { get; set; } = string.Empty; // For conditional fetching
}
