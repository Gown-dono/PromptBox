using LiteDB;
using PromptBox.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PromptBox.Services;

/// <summary>
/// Service for managing prompt version history
/// </summary>
public class VersioningService : IVersioningService
{
    private readonly string _connectionString;

    public VersioningService()
    {
        var dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        Directory.CreateDirectory(dataFolder);
        var dbPath = Path.Combine(dataFolder, "promptbox.db");
        _connectionString = $"Filename={dbPath};Connection=shared";
    }

    public async Task SaveVersionAsync(Prompt prompt)
    {
        if (prompt.Id == 0) return; // Don't save versions for new prompts

        await Task.Run(() =>
        {
            try
            {
                using var db = new LiteDatabase(_connectionString);
                var collection = db.GetCollection<PromptVersion>("versions");
                
                // Get the next version number for this prompt
                var existingVersions = collection.Find(v => v.PromptId == prompt.Id).ToList();
                var nextVersion = existingVersions.Any() ? existingVersions.Max(v => v.VersionNumber) + 1 : 1;

                var version = new PromptVersion
                {
                    PromptId = prompt.Id,
                    VersionNumber = nextVersion,
                    Title = prompt.Title,
                    Category = prompt.Category,
                    Tags = prompt.Tags?.ToList() ?? new List<string>(),
                    Content = prompt.Content ?? string.Empty,
                    SavedDate = DateTime.Now,
                    ChangeDescription = $"Version {nextVersion}"
                };

                collection.Insert(version);

                // Keep only the last 50 versions per prompt to prevent database bloat
                var versionsToDelete = existingVersions
                    .OrderByDescending(v => v.VersionNumber)
                    .Skip(49)
                    .Select(v => v.Id)
                    .ToList();

                foreach (var id in versionsToDelete)
                {
                    collection.Delete(id);
                }
            }
            catch (Exception)
            {
                // Silently fail version saving - don't block the main save operation
            }
        });
    }

    public async Task<List<PromptVersion>> GetVersionsAsync(int promptId)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<PromptVersion>("versions");
            return collection.Find(v => v.PromptId == promptId)
                .OrderByDescending(v => v.VersionNumber)
                .ToList();
        });
    }

    public async Task<PromptVersion?> GetVersionAsync(int versionId)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<PromptVersion>("versions");
            return collection.FindById(versionId);
        });
    }

    public async Task DeleteVersionsForPromptAsync(int promptId)
    {
        await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<PromptVersion>("versions");
            collection.DeleteMany(v => v.PromptId == promptId);
        });
    }

    public string GetDiff(string oldContent, string newContent)
    {
        if (string.IsNullOrEmpty(oldContent) && string.IsNullOrEmpty(newContent))
            return "No changes";

        if (string.IsNullOrEmpty(oldContent))
            return FormatAsAddition(newContent);

        if (string.IsNullOrEmpty(newContent))
            return FormatAsDeletion(oldContent);

        var oldLines = oldContent.Split('\n');
        var newLines = newContent.Split('\n');
        var diff = new StringBuilder();

        // Simple line-by-line diff using LCS approach
        var lcs = ComputeLCS(oldLines, newLines);
        int oldIndex = 0, newIndex = 0, lcsIndex = 0;

        while (oldIndex < oldLines.Length || newIndex < newLines.Length)
        {
            if (lcsIndex < lcs.Count && oldIndex < oldLines.Length && newIndex < newLines.Length)
            {
                if (oldLines[oldIndex] == lcs[lcsIndex] && newLines[newIndex] == lcs[lcsIndex])
                {
                    diff.AppendLine($"  {oldLines[oldIndex]}");
                    oldIndex++;
                    newIndex++;
                    lcsIndex++;
                }
                else if (oldLines[oldIndex] != lcs[lcsIndex])
                {
                    diff.AppendLine($"- {oldLines[oldIndex]}");
                    oldIndex++;
                }
                else
                {
                    diff.AppendLine($"+ {newLines[newIndex]}");
                    newIndex++;
                }
            }
            else if (oldIndex < oldLines.Length)
            {
                diff.AppendLine($"- {oldLines[oldIndex]}");
                oldIndex++;
            }
            else if (newIndex < newLines.Length)
            {
                diff.AppendLine($"+ {newLines[newIndex]}");
                newIndex++;
            }
        }

        return diff.ToString().TrimEnd();
    }

    private List<string> ComputeLCS(string[] oldLines, string[] newLines)
    {
        int m = oldLines.Length;
        int n = newLines.Length;
        var dp = new int[m + 1, n + 1];

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (oldLines[i - 1] == newLines[j - 1])
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                else
                    dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
            }
        }

        // Backtrack to find LCS
        var lcs = new List<string>();
        int x = m, y = n;
        while (x > 0 && y > 0)
        {
            if (oldLines[x - 1] == newLines[y - 1])
            {
                lcs.Insert(0, oldLines[x - 1]);
                x--;
                y--;
            }
            else if (dp[x - 1, y] > dp[x, y - 1])
                x--;
            else
                y--;
        }

        return lcs;
    }

    private string FormatAsAddition(string content)
    {
        var lines = content.Split('\n');
        return string.Join("\n", lines.Select(l => $"+ {l}"));
    }

    private string FormatAsDeletion(string content)
    {
        var lines = content.Split('\n');
        return string.Join("\n", lines.Select(l => $"- {l}"));
    }
}
