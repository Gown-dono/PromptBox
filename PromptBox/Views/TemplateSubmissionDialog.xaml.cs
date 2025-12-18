using PromptBox.Models;
using PromptBox.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace PromptBox.Views;

/// <summary>
/// Dialog for submitting a template to the community
/// </summary>
public partial class TemplateSubmissionDialog : Window, INotifyPropertyChanged
{
    private readonly IPromptCommunityService _communityService;
    private string _title = string.Empty;
    private string _selectedCategory = string.Empty;
    private string _description = string.Empty;
    private string _content = string.Empty;
    private string _tags = string.Empty;
    private string _submitterInfo = string.Empty;
    private string _selectedLicense = "MIT";
    private bool _acceptedTerms;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Categories { get; } = new()
    {
        "Coding", "Writing", "Analysis", "Creative", "Productivity", 
        "Learning", "AI Assistant", "Business", "Communication", "Research", 
        "Career", "Data Science", "DevOps", "Design", "Health & Wellness"
    };

    public ObservableCollection<string> Licenses { get; } = new()
    {
        "MIT", "CC0", "CC-BY", "CC-BY-SA"
    };

    public new string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanSubmit)); }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set { _selectedCategory = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanSubmit)); }
    }

    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanSubmit)); }
    }

    public new string Content
    {
        get => _content;
        set { _content = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanSubmit)); }
    }

    public string Tags
    {
        get => _tags;
        set { _tags = value; OnPropertyChanged(); }
    }

    public string SubmitterInfo
    {
        get => _submitterInfo;
        set { _submitterInfo = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanSubmit)); }
    }

    public string SelectedLicense
    {
        get => _selectedLicense;
        set { _selectedLicense = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanSubmit)); }
    }

    public bool AcceptedTerms
    {
        get => _acceptedTerms;
        set { _acceptedTerms = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanSubmit)); }
    }

    public bool CanSubmit => 
        !string.IsNullOrWhiteSpace(Title) &&
        !string.IsNullOrWhiteSpace(SelectedCategory) &&
        !string.IsNullOrWhiteSpace(Description) &&
        !string.IsNullOrWhiteSpace(Content) &&
        !string.IsNullOrWhiteSpace(SubmitterInfo) &&
        !string.IsNullOrWhiteSpace(SelectedLicense) &&
        AcceptedTerms;

    public TemplateSubmissionDialog(IPromptCommunityService communityService)
    {
        _communityService = communityService;
        InitializeComponent();
        DataContext = this;
    }

    private async void Submit_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSubmit) return;

        var template = new PromptTemplate
        {
            Title = Title.Trim(),
            Category = SelectedCategory.Trim(),
            Description = Description.Trim(),
            Content = Content.Trim(),
            Tags = Tags.Split(',')
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList(),
            Author = SubmitterInfo.Trim(),
            LicenseType = SelectedLicense,
            IsCommunity = true,
            IsOfficial = false
        };

        var (success, message) = await _communityService.SubmitTemplateAsync(template, SubmitterInfo.Trim());

        if (success)
        {
            MessageBox.Show(message, "Submission Prepared", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show(message, "Submission Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
