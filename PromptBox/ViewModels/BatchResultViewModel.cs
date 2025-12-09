using CommunityToolkit.Mvvm.ComponentModel;
using PromptBox.Models;

namespace PromptBox.ViewModels;

/// <summary>
/// View model wrapper for BatchResult with computed display properties
/// </summary>
public partial class BatchResultViewModel : ObservableObject
{
    public BatchResult Result { get; }

    public BatchResultViewModel(BatchResult result)
    {
        Result = result;
    }

    public string PromptTitle => Result.PromptTitle;
    public string ModelName => Result.ModelName;
    public bool IsSuccess => Result.Success;
    public bool IsFailed => !Result.Success;
    public int TokensUsed => Result.TokensUsed;
    
    public string StatusIcon => Result.Success ? "CheckCircle" : "CloseCircle";
    
    public string StatusColor => Result.Success ? "#4CAF50" : "#F44336";
    
    public string DurationFormatted => $"{Result.Duration.TotalSeconds:F1}s";
    
    public string ResponsePreview => string.IsNullOrEmpty(Result.Response) 
        ? (Result.Error ?? "No response") 
        : (Result.Response.Length > 100 
            ? Result.Response.Substring(0, 100) + "..." 
            : Result.Response);
    
    public string FullResponse => Result.Response;
    
    public string? Error => Result.Error;
}
