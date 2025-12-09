using CommunityToolkit.Mvvm.ComponentModel;
using PromptBox.Models;
using System;

namespace PromptBox.ViewModels;

/// <summary>
/// ViewModel for displaying test results in the UI
/// </summary>
public partial class TestResultViewModel : ObservableObject
{
    public TestResult Result { get; }

    public TestResultViewModel(TestResult result)
    {
        Result = result;
    }

    public string StatusIcon => Result.Success ? "✓" : "✗";
    public string StatusColor => Result.Success ? "Green" : "Red";
    public bool IsSuccess => Result.Success;
    public bool IsFailed => !Result.Success;

    public string TestCaseName => Result.TestCaseName;
    public string ModelName => Result.ModelName;
    public string Input => Result.Input;

    public string QualityScoreFormatted => $"{Result.QualityScore:F1}";
    public string ClarityScoreFormatted => $"{Result.ClarityScore:F1}";
    public string SpecificityScoreFormatted => $"{Result.SpecificityScore:F1}";
    public string EffectivenessScoreFormatted => $"{Result.EffectivenessScore:F1}";

    public int TokensUsed => Result.TokensUsed;
    public string DurationFormatted => Result.Duration.TotalSeconds < 1
        ? $"{Result.Duration.TotalMilliseconds:F0}ms"
        : $"{Result.Duration.TotalSeconds:F1}s";

    public string OutputPreview => Result.ActualOutput.Length > 100
        ? Result.ActualOutput[..100] + "..."
        : Result.ActualOutput;

    public string FullOutput => Result.ActualOutput;
    public string? FailureReason => Result.FailureReason;
    public DateTime ExecutedAt => Result.ExecutedAt;
}
