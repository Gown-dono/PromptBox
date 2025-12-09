using PromptBox.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PromptBox.Services;

public interface IPromptComparisonService
{
    IAsyncEnumerable<ComparisonProgress> ExecuteComparisonAsync(
        PromptComparisonSession session,
        CancellationToken cancellationToken = default);

    Task<ComparisonResult> ExecuteSingleComparisonAsync(
        PromptVariation variation,
        string input,
        string modelId,
        double temperature,
        int maxTokens);

    List<ComparisonResult> RankResults(List<ComparisonResult> results);
}
