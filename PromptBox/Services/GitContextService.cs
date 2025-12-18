using System;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using System.Linq;

namespace PromptBox.Services;

public class GitContextService : IGitContextService
{
    public async Task<string> GetRepositoryInfoAsync(string repoPath)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Find the git repository
                var gitDir = Repository.Discover(repoPath);
                if (string.IsNullOrEmpty(gitDir))
                    throw new Exception("Git repository not found");

                using var repo = new Repository(gitDir);
                
                var sb = new StringBuilder();
                sb.AppendLine($"## Repository Info");
                sb.AppendLine($"**Path:** {repo.Info.WorkingDirectory}");
                sb.AppendLine($"**HEAD:** {repo.Head.FriendlyName} ({repo.Head.Tip?.Sha.Substring(0, 7)})");
                sb.AppendLine($"**IsBare:** {repo.Info.IsBare}");
                sb.AppendLine($"**IsShallow:** {repo.Info.IsShallow}");
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting repository info: {ex.Message}";
            }
        });
    }

    public async Task<string> GetCommitHistoryAsync(string repoPath, int count = 10)
    {
        return await Task.Run(() =>
        {
            try
            {
                var gitDir = Repository.Discover(repoPath);
                if (string.IsNullOrEmpty(gitDir))
                    throw new Exception("Git repository not found");

                using var repo = new Repository(gitDir);
                
                var sb = new StringBuilder();
                sb.AppendLine($"## Recent Commits (last {count})");
                
                var commits = repo.Commits.Take(count);
                foreach (var commit in commits)
                {
                    sb.AppendLine($"- `{commit.Sha.Substring(0, 7)}` {commit.MessageShort} ({commit.Author.Name}, {commit.Author.When.LocalDateTime:yyyy-MM-dd HH:mm})");
                }
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting commit history: {ex.Message}";
            }
        });
    }

    public async Task<string> GetBranchInfoAsync(string repoPath)
    {
        return await Task.Run(() =>
        {
            try
            {
                var gitDir = Repository.Discover(repoPath);
                if (string.IsNullOrEmpty(gitDir))
                    throw new Exception("Git repository not found");

                using var repo = new Repository(gitDir);
                
                var sb = new StringBuilder();
                sb.AppendLine("## Branches");
                
                foreach (var branch in repo.Branches.Where(b => b.IsRemote == false))
                {
                    var marker = branch.IsCurrentRepositoryHead ? "**" : "";
                    sb.AppendLine($"- {marker}{branch.FriendlyName}{marker} ({branch.Tip?.Sha.Substring(0, 7)})");
                }
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting branch info: {ex.Message}";
            }
        });
    }

    public async Task<string> GetDiffAsync(string repoPath, string? commitSha = null)
    {
        return await Task.Run(() =>
        {
            try
            {
                var gitDir = Repository.Discover(repoPath);
                if (string.IsNullOrEmpty(gitDir))
                    throw new Exception("Git repository not found");

                using var repo = new Repository(gitDir);
                
                var sb = new StringBuilder();
                sb.AppendLine("## Changes");
                
                TreeChanges changes;
                if (string.IsNullOrEmpty(commitSha))
                {
                    // Compare working directory with HEAD
                    changes = repo.Diff.Compare<TreeChanges>(repo.Head.Tip.Tree, DiffTargets.WorkingDirectory);
                }
                else
                {
                    // Compare with specific commit
                    var commit = repo.Lookup<Commit>(commitSha);
                    if (commit == null)
                        throw new Exception($"Commit {commitSha} not found");
                    
                    changes = repo.Diff.Compare<TreeChanges>(commit.Tree, DiffTargets.WorkingDirectory);
                }
                
                if (changes.Count() == 0)
                {
                    sb.AppendLine("No changes detected.");
                }
                else
                {
                    foreach (var change in changes)
                    {
                        var status = change.Status.ToString();
                        sb.AppendLine($"- {status}: {change.Path}");
                    }
                }
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting diff: {ex.Message}";
            }
        });
    }
}