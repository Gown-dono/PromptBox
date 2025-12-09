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

    public async Task ExportBatchResultsAsCsvAsync(List<BatchResult> results, string filePath)
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Prompt Title,Model Name,Status,Tokens Used,Duration (seconds),Response,Error,Executed At");

        foreach (var result in results)
        {
            var status = result.Success ? "Success" : "Failed";
            var response = EscapeCsvField(result.Response);
            var error = EscapeCsvField(result.Error ?? "");

            csv.AppendLine($"{EscapeCsvField(result.PromptTitle)},{EscapeCsvField(result.ModelName)},{status},{result.TokensUsed},{result.Duration.TotalSeconds:F2},{response},{error},{result.ExecutedAt:yyyy-MM-dd HH:mm:ss}");
        }

        await File.WriteAllTextAsync(filePath, csv.ToString());
    }

    public async Task ExportBatchResultsAsJsonAsync(List<BatchResult> results, string filePath)
    {
        var json = JsonSerializer.Serialize(results, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task ExportTestResultsAsCsvAsync(List<TestResult> results, string filePath)
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Test Case,Model,Status,Quality Score,Clarity,Specificity,Effectiveness,Tokens,Duration (s),Output,Failure Reason,Executed At");

        foreach (var result in results)
        {
            var status = result.Success ? "Pass" : "Fail";
            var output = EscapeCsvField(result.ActualOutput);
            var failureReason = EscapeCsvField(result.FailureReason ?? "");

            csv.AppendLine($"{EscapeCsvField(result.TestCaseName)},{EscapeCsvField(result.ModelName)},{status},{result.QualityScore:F1},{result.ClarityScore:F1},{result.SpecificityScore:F1},{result.EffectivenessScore:F1},{result.TokensUsed},{result.Duration.TotalSeconds:F2},{output},{failureReason},{result.ExecutedAt:yyyy-MM-dd HH:mm:ss}");
        }

        await File.WriteAllTextAsync(filePath, csv.ToString());
    }

    public async Task ExportTestResultsAsJsonAsync(List<TestResult> results, string filePath)
    {
        var json = JsonSerializer.Serialize(results, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task ExportComparisonReportAsync(TestComparison comparison, List<ComparisonResult> results, string filePath)
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine($"# A/B Comparison Report: {comparison.Name}");
        report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine();
        report.AppendLine("## Test Input");
        report.AppendLine($"```\n{comparison.TestInput}\n```");
        report.AppendLine();
        report.AppendLine("## Variations Tested");
        foreach (var variation in comparison.PromptVariations)
        {
            report.AppendLine($"### {variation.Name}");
            if (!string.IsNullOrWhiteSpace(variation.Description))
                report.AppendLine($"*{variation.Description}*");
            report.AppendLine($"```\n{variation.Content}\n```");
            report.AppendLine();
        }
        report.AppendLine("## Results");
        report.AppendLine("| Rank | Variation | Model | Quality Score | Tokens | Duration |");
        report.AppendLine("|------|-----------|-------|---------------|--------|----------|");
        foreach (var result in results.OrderBy(r => r.Ranking))
        {
            var duration = result.Duration.TotalSeconds < 1
                ? $"{result.Duration.TotalMilliseconds:F0}ms"
                : $"{result.Duration.TotalSeconds:F1}s";
            report.AppendLine($"| {result.Ranking} | {result.VariationName} | {result.ModelName} | {result.QualityScore:F1} | {result.TokensUsed} | {duration} |");
        }
        report.AppendLine();
        var winner = results.OrderBy(r => r.Ranking).FirstOrDefault();
        if (winner != null)
        {
            report.AppendLine("## Winner Analysis");
            report.AppendLine($"**Best Performing:** {winner.VariationName} with {winner.ModelName}");
            report.AppendLine($"**Quality Score:** {winner.QualityScore:F1}");
            report.AppendLine();
            report.AppendLine("### Winning Output");
            report.AppendLine($"```\n{winner.Output}\n```");
        }

        await File.WriteAllTextAsync(filePath, report.ToString());
    }

    public async Task ExportPromptComparisonReportAsync(PromptComparisonSession session, List<ComparisonResult> results, string filePath)
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine($"# Prompt Comparison Report: {session.Name}");
        report.AppendLine($"**Generated:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        if (!string.IsNullOrWhiteSpace(session.Description))
            report.AppendLine($"**Description:** {session.Description}");
        report.AppendLine();

        report.AppendLine("## Shared Input");
        report.AppendLine("```");
        report.AppendLine(session.SharedInput);
        report.AppendLine("```");
        report.AppendLine();

        report.AppendLine("## Variations Tested");
        report.AppendLine("| Name | Description |");
        report.AppendLine("|------|-------------|");
        foreach (var variation in session.PromptVariations)
        {
            var desc = string.IsNullOrWhiteSpace(variation.Description) ? "-" : variation.Description;
            report.AppendLine($"| {variation.Name} | {desc} |");
        }
        report.AppendLine();

        report.AppendLine("## Models Tested");
        foreach (var modelId in session.ModelIds)
        {
            var modelResult = results.FirstOrDefault(r => r.ModelId == modelId);
            var modelName = modelResult?.ModelName ?? modelId;
            report.AppendLine($"- {modelName}");
        }
        report.AppendLine();

        report.AppendLine("## Results Summary");
        report.AppendLine("| Rank | Variation | Model | Quality | Clarity | Specificity | Tokens | Duration |");
        report.AppendLine("|------|-----------|-------|---------|---------|-------------|--------|----------|");
        foreach (var result in results.OrderBy(r => r.Ranking))
        {
            var rankIcon = result.Ranking switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => $"#{result.Ranking}" };
            var duration = result.Duration.TotalSeconds < 1
                ? $"{result.Duration.TotalMilliseconds:F0}ms"
                : $"{result.Duration.TotalSeconds:F1}s";
            var status = result.Success ? "" : " ❌";
            report.AppendLine($"| {rankIcon} | {result.VariationName}{status} | {result.ModelName} | {result.QualityScore:F1} | {result.ClarityScore:F1} | {result.SpecificityScore:F1} | {result.TokensUsed} | {duration} |");
        }
        report.AppendLine();

        var winner = results.OrderBy(r => r.Ranking).FirstOrDefault(r => r.Success);
        if (winner != null)
        {
            report.AppendLine("## Winner Analysis");
            report.AppendLine($"**Best Performing:** {winner.VariationName} with {winner.ModelName}");
            report.AppendLine($"**Quality Score:** {winner.QualityScore:F1}");
            report.AppendLine($"**Clarity Score:** {winner.ClarityScore:F1}");
            report.AppendLine($"**Specificity Score:** {winner.SpecificityScore:F1}");
            report.AppendLine();
            report.AppendLine("### Winning Output");
            report.AppendLine("```");
            report.AppendLine(winner.Output);
            report.AppendLine("```");
            report.AppendLine();
        }

        report.AppendLine("## Detailed Outputs");
        foreach (var variation in session.PromptVariations)
        {
            report.AppendLine($"### {variation.Name}");
            report.AppendLine($"**Prompt:**");
            report.AppendLine("```");
            report.AppendLine(variation.Content);
            report.AppendLine("```");
            report.AppendLine();

            var variationResults = results.Where(r => r.VariationName == variation.Name).OrderBy(r => r.Ranking);
            foreach (var result in variationResults)
            {
                report.AppendLine($"#### {result.ModelName}");
                if (result.Success)
                {
                    report.AppendLine($"- Quality: {result.QualityScore:F1} | Clarity: {result.ClarityScore:F1} | Specificity: {result.SpecificityScore:F1}");
                    report.AppendLine($"- Tokens: {result.TokensUsed} | Duration: {result.Duration.TotalSeconds:F2}s");
                    report.AppendLine("```");
                    report.AppendLine(result.Output);
                    report.AppendLine("```");
                }
                else
                {
                    report.AppendLine($"**Error:** {result.Error}");
                }
                report.AppendLine();
            }
        }

        report.AppendLine("## Recommendations");
        if (winner != null)
        {
            var avgQuality = results.Where(r => r.Success).Average(r => r.QualityScore);
            var avgTokens = results.Where(r => r.Success).Average(r => r.TokensUsed);

            report.AppendLine($"- **{winner.VariationName}** achieved the highest quality score ({winner.QualityScore:F1})");

            var mostEfficient = results.Where(r => r.Success).OrderBy(r => r.TokensUsed).FirstOrDefault();
            if (mostEfficient != null && mostEfficient.VariationName != winner.VariationName)
            {
                report.AppendLine($"- **{mostEfficient.VariationName}** is most token-efficient ({mostEfficient.TokensUsed} tokens)");
            }

            if (winner.QualityScore > avgQuality * 1.1)
            {
                report.AppendLine($"- The winner significantly outperforms the average quality ({avgQuality:F1})");
            }
        }

        await File.WriteAllTextAsync(filePath, report.ToString());
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        
        // If field contains comma, quote, or newline, wrap in quotes and escape quotes
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
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
