using PromptBox.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PromptBox.Services;

/// <summary>
/// Service for exporting and importing prompts, workflows, and version history
/// </summary>
public class ExportService : IExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

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
        var json = JsonSerializer.Serialize(prompts, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<List<Prompt>> ImportPromptsFromJsonAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var prompts = JsonSerializer.Deserialize<List<Prompt>>(json);
        return prompts ?? new List<Prompt>();
    }

    public async Task ExportWorkflowsAsJsonAsync(List<Workflow> workflows, string filePath)
    {
        var json = JsonSerializer.Serialize(workflows, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<List<Workflow>> ImportWorkflowsFromJsonAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var workflows = JsonSerializer.Deserialize<List<Workflow>>(json);
        return workflows ?? new List<Workflow>();
    }

    public async Task ExportPromptsWithHistoryAsJsonAsync(List<Prompt> prompts, List<PromptVersion> versions, string filePath)
    {
        var exportData = new PromptExportWithHistory
        {
            Prompts = prompts,
            Versions = versions,
            ExportedAt = DateTime.Now
        };
        var json = JsonSerializer.Serialize(exportData, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<(List<Prompt> Prompts, List<PromptVersion> Versions)> ImportPromptsWithHistoryFromJsonAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var exportData = JsonSerializer.Deserialize<PromptExportWithHistory>(json);
        return (exportData?.Prompts ?? new List<Prompt>(), exportData?.Versions ?? new List<PromptVersion>());
    }
}

/// <summary>
/// Container for exporting prompts with their version history
/// </summary>
public class PromptExportWithHistory
{
    public List<Prompt> Prompts { get; set; } = new();
    public List<PromptVersion> Versions { get; set; } = new();
    public DateTime ExportedAt { get; set; }
}
