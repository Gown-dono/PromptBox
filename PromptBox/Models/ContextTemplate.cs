using System;
using System.Collections.Generic;

namespace PromptBox.Models;

public class ContextTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ContextItem> Items { get; set; } = new();
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}