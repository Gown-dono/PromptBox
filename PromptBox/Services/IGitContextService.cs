using System.Threading.Tasks;

namespace PromptBox.Services;

public interface IGitContextService
{
    Task<string> GetRepositoryInfoAsync(string repoPath);
    Task<string> GetCommitHistoryAsync(string repoPath, int count = 10);
    Task<string> GetBranchInfoAsync(string repoPath);
    Task<string> GetDiffAsync(string repoPath, string? commitSha = null);
}