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
/// Dialog for browsing and importing prompt templates from the library
/// </summary>
public partial class LibraryBrowserDialog : Window, INotifyPropertyChanged
{
    private readonly IPromptLibraryService _libraryService;
    private ObservableCollection<string> _categories = new();
    private ObservableCollection<PromptTemplate> _filteredTemplates = new();
    private string _selectedCategory = string.Empty;
    private string _searchQuery = string.Empty;
    private PromptTemplate? _selectedTemplate;

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

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            _selectedCategory = value;
            OnPropertyChanged();
            ApplyFilters();
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            _searchQuery = value;
            OnPropertyChanged();
            ApplyFilters();
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

    public Prompt? ImportedPrompt { get; private set; }

    public LibraryBrowserDialog(IPromptLibraryService libraryService)
    {
        _libraryService = libraryService;
        InitializeComponent();
        DataContext = this;
        LoadData();
    }

    private void LoadData()
    {
        var categories = _libraryService.GetCategories();
        // Add "All" option at the beginning
        var allCategories = new List<string> { "All Categories" };
        allCategories.AddRange(categories);
        Categories = new ObservableCollection<string>(allCategories);
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var effectiveCategory = SelectedCategory == "All Categories" ? string.Empty : SelectedCategory;
        
        var templates = string.IsNullOrWhiteSpace(effectiveCategory)
            ? _libraryService.GetAllTemplates()
            : _libraryService.GetTemplatesByCategory(effectiveCategory);

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            templates = _libraryService.SearchTemplates(SearchQuery)
                .Where(t => string.IsNullOrWhiteSpace(effectiveCategory) || 
                           t.Category == effectiveCategory)
                .ToList();
        }

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

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTemplate == null) return;

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
