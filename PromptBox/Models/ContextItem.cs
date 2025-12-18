using System.Collections.Generic;

namespace PromptBox.Models;

/// <summary>
/// Represents a context item that can be injected into prompts
/// </summary>
public class ContextItem
{
    public ContextItemType Type { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string SizeText { get; set; } = string.Empty;
    
    // NEW PROPERTIES
    public string? ConnectionString { get; set; }  // For DatabaseQuery
    public string? Url { get; set; }               // For ApiEndpoint, WebPage
    public string? Query { get; set; }             // For DatabaseQuery
    public string? HttpMethod { get; set; }        // For ApiEndpoint
    public Dictionary<string, string>? Headers { get; set; }  // For ApiEndpoint
    public string? RepositoryPath { get; set; }    // For GitRepository
    
    public string Icon => Type switch
    {
        ContextItemType.File => "FileDocumentOutline",
        ContextItemType.Folder => "FolderOutline",
        ContextItemType.Clipboard => "ClipboardOutline",
        ContextItemType.Note => "NoteOutline",
        ContextItemType.GitRepository => "Git",
        ContextItemType.DatabaseQuery => "Database",
        ContextItemType.ApiEndpoint => "Api",
        ContextItemType.WebPage => "Web",
        _ => "FileOutline"
    };
}

public enum ContextItemType
{
    File,
    Folder,
    Clipboard,
    Note,
    GitRepository,
    DatabaseQuery,
    ApiEndpoint,
    WebPage
}
