using PromptBox.Models;
using PromptBox.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

namespace PromptBox.Views;

/// <summary>
/// Dialog for browsing and importing prompt templates from the library
/// </summary>
public partial class LibraryBrowserDialog : Window, INotifyPropertyChanged
{
    private readonly IPromptLibraryService _libraryService;
    private readonly IPromptCommunityService? _communityService;
    private ObservableCollection<string> _categories = new();
    private ObservableCollection<PromptTemplate> _filteredTemplates = new();
    private ObservableCollection<string> _sourceFilters = new() { "All", "Local", "Community" };
    private string _selectedCategory = string.Empty;
    private string _selectedSource = "All";
    private string _searchQuery = string.Empty;
    private PromptTemplate? _selectedTemplate;
    private bool _isLoadingCommunity;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Categories
    {
        get => _categories;
        set { _categories = value; OnPropertyChanged(); }
    }

    public ObservableCollection<PromptTemplate> FilteredTemplates
    {
        get => _filteredTemplates;
        set { _filteredTemplates = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> SourceFilters
    {
        get => _sourceFilters;
        set { _sourceFilters = value; OnPropertyChanged(); }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            _selectedCategory = value;
            OnPropertyChanged();
            _ = ApplyFiltersAsync();
        }
    }

    public string SelectedSource
    {
        get => _selectedSource;
        set
        {
            _selectedSource = value;
            OnPropertyChanged();
            _ = ApplyFiltersAsync();
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            _searchQuery = value;
            OnPropertyChanged();
            _ = ApplyFiltersAsync();
        }
    }

    public PromptTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            _selectedTemplate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    public bool HasSelection => SelectedTemplate != null;

    public bool IsLoadingCommunity
    {
        get => _isLoadingCommunity;
        set { _isLoadingCommunity = value; OnPropertyChanged(); }
    }

    public Prompt? ImportedPrompt { get; private set; }

    /// <summary>
    /// Creates a new LibraryBrowserDialog instance.
    /// </summary>
    /// <param name="libraryService">The prompt library service for accessing templates.</param>
    /// <param name="communityService">
    /// Optional community service for accessing community templates.
    /// Pass null to disable community features and show only local built-in templates.
    /// </param>
    public LibraryBrowserDialog(IPromptLibraryService libraryService, IPromptCommunityService? communityService = null)
    {
        _libraryService = libraryService;
        _communityService = communityService;
        InitializeComponent();
        DataContext = this;
        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        IsLoadingCommunity = true;
        try
        {
            var categories = _libraryService.GetCategories();
            var allCategories = new List<string> { "All Categories" };
            allCategories.AddRange(categories);
            
            // Add community categories if available
            if (_communityService != null)
            {
                var communityTemplates = await _communityService.FetchCommunityTemplatesAsync();
                var communityCategories = communityTemplates
                    .Select(t => t.Category)
                    .Where(c => !string.IsNullOrWhiteSpace(c) && !allCategories.Contains(c))
                    .Distinct();
                allCategories.AddRange(communityCategories);
            }
            
            Categories = new ObservableCollection<string>(allCategories.OrderBy(c => c == "All Categories" ? "" : c));
            await ApplyFiltersAsync();
        }
        finally
        {
            IsLoadingCommunity = false;
        }
    }


    private async Task ApplyFiltersAsync()
    {
        var effectiveCategory = SelectedCategory == "All Categories" ? string.Empty : SelectedCategory;
        
        List<PromptTemplate> templates;
        
        // Get templates based on source filter
        if (_communityService != null)
        {
            templates = await _libraryService.SearchTemplatesAsync(SearchQuery, SelectedSource);
        }
        else
        {
            templates = string.IsNullOrWhiteSpace(SearchQuery)
                ? _libraryService.GetAllTemplates()
                : _libraryService.SearchTemplates(SearchQuery);
        }

        // Apply category filter
        if (!string.IsNullOrWhiteSpace(effectiveCategory))
        {
            templates = templates.Where(t => t.Category.Equals(effectiveCategory, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Sort by downloads, then title
        templates = templates
            .OrderByDescending(t => t.DownloadCount)
            .ThenBy(t => t.Title)
            .ToList();

        FilteredTemplates = new ObservableCollection<PromptTemplate>(templates);
        
        if (SelectedTemplate != null && !FilteredTemplates.Contains(SelectedTemplate))
        {
            SelectedTemplate = FilteredTemplates.FirstOrDefault();
        }
        else if (SelectedTemplate == null && FilteredTemplates.Any())
        {
            SelectedTemplate = FilteredTemplates.First();
        }
    }

    private async void RefreshCommunity_Click(object sender, RoutedEventArgs e)
    {
        if (_communityService == null) return;
        
        IsLoadingCommunity = true;
        try
        {
            await _libraryService.RefreshCommunityTemplatesAsync();
            await ApplyFiltersAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error refreshing community templates: {ex.Message}", 
                "Refresh Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsLoadingCommunity = false;
        }
    }

    private void SubmitTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (_communityService == null)
        {
            MessageBox.Show("Community service is not available.", "Submit Template", 
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new TemplateSubmissionDialog(_communityService);
        dialog.Owner = this;
        dialog.ShowDialog();
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTemplate == null) return;

        // Record the download based on template source (this also increments the count)
        if (SelectedTemplate.IsCommunity && _communityService != null)
        {
            await _communityService.RecordDownloadAsync(SelectedTemplate.Id);
        }
        else
        {
            await _libraryService.RecordLocalDownloadAsync(SelectedTemplate.Id);
        }
        
        // Refresh the filtered list to reflect updated download count ordering
        await ApplyFiltersAsync();

        ImportedPrompt = new Prompt
        {
            Title = SelectedTemplate.Title,
            Category = SelectedTemplate.Category,
            Tags = SelectedTemplate.Tags.ToList(),
            Content = SelectedTemplate.Content
        };

        DialogResult = true;
        Close();
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
