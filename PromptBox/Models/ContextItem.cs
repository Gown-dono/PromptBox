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
    
    public string Icon => Type switch
    {
        ContextItemType.File => "FileDocumentOutline",
        ContextItemType.Folder => "FolderOutline",
        ContextItemType.Clipboard => "ClipboardOutline",
        ContextItemType.Note => "NoteOutline",
        _ => "FileOutline"
    };
}

public enum ContextItemType
{
    File,
    Folder,
    Clipboard,
    Note
}
