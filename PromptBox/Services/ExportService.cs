using PromptBox.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PromptBox.Services;

/// <summary>
/// Service for exporting and importing prompts
/// </summary>
public class ExportService : IExportService
{
    public async Task ExportPromptAsMarkdownAsync(Prompt prompt, string filePath)
    {
        var markdown = $"# {prompt.Title}\n\n";
        markdown += $"**Category:** {prompt.Category}\n\n";
        markdown += $"**Tags:** {string.Join(", ", prompt.Tags)}\n\n";
        markdown += $"**Created:** {prompt.CreatedDate:yyyy-MM-dd}\n\n";
        markdown += $"---\n\n{prompt.Content}";

        await File.WriteAllTextAsync(filePath, markdown);
    }

    public async Task ExportPromptAsTextAsync(Prompt prompt, string filePath)
    {
        await File.WriteAllTextAsync(filePath, prompt.Content);
    }

    public async Task ExportAllPromptsAsJsonAsync(List<Prompt> prompts, string filePath)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(prompts, options);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<List<Prompt>> ImportPromptsFromJsonAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var prompts = JsonSerializer.Deserialize<List<Prompt>>(json);
        return prompts ?? new List<Prompt>();
    }
}
