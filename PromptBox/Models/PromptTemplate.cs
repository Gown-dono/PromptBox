using System;
using System.Collections.Generic;

namespace PromptBox.Models;

/// <summary>
/// Represents a pre-defined prompt template from the library
/// </summary>
public class PromptTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string Content { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = "PromptBox";
    public string Version { get; set; } = "1.0";
    
    // Community-related properties
    public int DownloadCount { get; set; } = 0;
    public string SubmittedBy { get; set; } = string.Empty;
    public DateTime SubmittedDate { get; set; } = DateTime.MinValue;
    public DateTime LastUpdated { get; set; } = DateTime.MinValue;
    public bool IsOfficial { get; set; } = false;
    public bool IsCommunity { get; set; } = false;
    public string SourceUrl { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = new();
    public string LicenseType { get; set; } = "MIT";
}
