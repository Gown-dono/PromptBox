using CommunityToolkit.Mvvm.ComponentModel;
using PromptBox.Models;

namespace PromptBox.ViewModels;

/// <summary>
/// ViewModel for displaying prompt variations in the UI
/// </summary>
public partial class PromptVariationViewModel : ObservableObject
{
    public PromptVariation Variation { get; }

    public PromptVariationViewModel(PromptVariation variation)
    {
        Variation = variation;
    }

    public PromptVariationViewModel(string name, string content, string description = "")
    {
        Variation = new PromptVariation
        {
            Name = name,
            Content = content,
            Description = description
        };
    }

    public string Name
    {
        get => Variation.Name;
        set
        {
            if (Variation.Name != value)
            {
                Variation.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public string Content
    {
        get => Variation.Content;
        set
        {
            if (Variation.Content != value)
            {
                Variation.Content = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ContentPreview));
            }
        }
    }

    public string Description
    {
        get => Variation.Description;
        set
        {
            if (Variation.Description != value)
            {
                Variation.Description = value;
                OnPropertyChanged();
            }
        }
    }

    public string ContentPreview => Variation.Content.Length > 100
        ? Variation.Content[..100] + "..."
        : Variation.Content;

    [ObservableProperty]
    private bool _isSelected;
}
