using CommunityToolkit.Mvvm.ComponentModel;
using PromptBox.Models;

namespace PromptBox.ViewModels;

/// <summary>
/// ViewModel for displaying comparison results in the UI
/// </summary>
public partial class ComparisonResultViewModel : ObservableObject
{
    public ComparisonResult Result { get; }

    public ComparisonResultViewModel(ComparisonResult result)
    {
        Result = result;
    }

    public string VariationName => Result.VariationName;
    public string ModelName => Result.ModelName;
    public string ModelId => Result.ModelId;
    public int Ranking => Result.Ranking;

    public string RankingColor => Result.Ranking switch
    {
        1 => "#4CAF50",  // Green for best
        2 => "#FFC107",  // Yellow for second
        3 => "#FF9800",  // Orange for third
        _ => "#9E9E9E"   // Gray for others
    };

    public string RankingIcon => Result.Ranking switch
    {
        1 => "🥇",
        2 => "🥈",
        3 => "🥉",
        _ => $"#{Result.Ranking}"
    };

    public string QualityScoreFormatted => $"{Result.QualityScore:F1}";
    public string ClarityScoreFormatted => $"{Result.ClarityScore:F1}";
    public string SpecificityScoreFormatted => $"{Result.SpecificityScore:F1}";
    public string TokensFormatted => Result.TokensUsed.ToString("N0");
    public string DurationFormatted => Result.Duration.TotalSeconds < 1
        ? $"{Result.Duration.TotalMilliseconds:F0}ms"
        : $"{Result.Duration.TotalSeconds:F1}s";

    public string OutputPreview => Result.Output.Length > 150
        ? Result.Output[..150] + "..."
        : Result.Output;

    public string FullOutput => Result.Output;

    public bool IsSuccess => Result.Success;
    public bool IsFailed => !Result.Success;
    public string? Error => Result.Error;

    public string StatusIcon => Result.Success ? "CheckCircle" : "AlertCircle";
    public string StatusColor => Result.Success ? "#4CAF50" : "#F44336";
}
