using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using PromptBox.Models;
using PromptBox.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PromptBox.ViewModels;

/// <summary>
/// Main ViewModel for the application
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IDatabaseService _databaseService;
    private readonly IThemeService _themeService;
    private readonly IExportService _exportService;
    private readonly ISearchService _searchService;
    
    public SnackbarMessageQueue? SnackbarMessageQueue { get; set; }

    [ObservableProperty]
    private ObservableCollection<Prompt> _prompts = new();

    [ObservableProperty]
    private ObservableCollection<Prompt> _filteredPrompts = new();

    [ObservableProperty]
    private ObservableCollection<string> _categories = new();

    [ObservableProperty]
    private ObservableCollection<string> _tags = new();

    [ObservableProperty]
    private Prompt? _selectedPrompt;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = string.Empty;

    [ObservableProperty]
    private string _selectedTag = string.Empty;

    [ObservableProperty]
    private bool _isDarkMode;

    [ObservableProperty]
    private string _editTitle = string.Empty;

    [ObservableProperty]
    private string _editCategory = string.Empty;

    [ObservableProperty]
    private string _editTags = string.Empty;

    [ObservableProperty]
    private string _editContent = string.Empty;

    public MainViewModel(
        IDatabaseService databaseService,
        IThemeService themeService,
        IExportService exportService,
        ISearchService searchService)
    {
        _databaseService = databaseService;
        _themeService = themeService;
        _exportService = exportService;
        _searchService = searchService;
        
        IsDarkMode = _themeService.IsDarkMode;
        
        _ = LoadData();
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedTagChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedPromptChanged(Prompt? value)
    {
        if (value != null)
        {
            EditTitle = value.Title;
            EditCategory = value.Category;
            EditTags = string.Join(", ", value.Tags);
            EditContent = value.Content;
        }
        else
        {
            ClearEditor();
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task LoadData()
    {
        var allPrompts = await _databaseService.GetAllPromptsAsync();
        Prompts = new ObservableCollection<Prompt>(allPrompts);
        
        var categories = await _databaseService.GetAllCategoriesAsync();
        Categories = new ObservableCollection<string>(categories);
        
        var tags = await _databaseService.GetAllTagsAsync();
        Tags = new ObservableCollection<string>(tags);
        
        ApplyFilters();
    }

    [RelayCommand]
    private void NewPrompt()
    {
        SelectedPrompt = null;
        ClearEditor();
    }

    [RelayCommand]
    private void ClearCategoryFilter()
    {
        SelectedCategory = string.Empty;
    }

    [RelayCommand]
    private void ClearTagFilter()
    {
        SelectedTag = string.Empty;
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task SavePrompt()
    {
        if (string.IsNullOrWhiteSpace(EditTitle))
        {
            SnackbarMessageQueue?.Enqueue("⚠️ Please enter a title");
            return;
        }

        var prompt = SelectedPrompt ?? new Prompt();
        prompt.Title = EditTitle;
        prompt.Category = EditCategory;
        prompt.Tags = EditTags.Split(',', System.StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();
        prompt.Content = EditContent;

        await _databaseService.SavePromptAsync(prompt);
        await LoadData();
        
        SnackbarMessageQueue?.Enqueue("✓ Prompt saved successfully!");
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task DeletePrompt()
    {
        if (SelectedPrompt == null)
            return;

        var titleText = new TextBlock
        {
            Text = $"Delete '{SelectedPrompt.Title}'?",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var messageText = new TextBlock
        {
            Text = "This action cannot be undone.",
            Margin = new Thickness(0, 0, 0, 16)
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Style = Application.Current.FindResource("MaterialDesignFlatButton") as Style,
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = false,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var deleteButton = new Button
        {
            Content = "Delete",
            Style = Application.Current.FindResource("MaterialDesignRaisedButton") as Style,
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = true,
            Background = new SolidColorBrush(Color.FromRgb(211, 47, 47))
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(deleteButton);

        var mainPanel = new StackPanel
        {
            Margin = new Thickness(16)
        };
        mainPanel.Children.Add(titleText);
        mainPanel.Children.Add(messageText);
        mainPanel.Children.Add(buttonPanel);

        var result = await DialogHost.Show(mainPanel, "RootDialog");

        if (result is bool confirmed && confirmed)
        {
            await _databaseService.DeletePromptAsync(SelectedPrompt.Id);
            await LoadData();
            ClearEditor();
            SnackbarMessageQueue?.Enqueue("✓ Prompt deleted");
        }
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        if (!string.IsNullOrWhiteSpace(EditContent))
        {
            Clipboard.SetText(EditContent);
            SnackbarMessageQueue?.Enqueue("✓ Copied to clipboard!");
        }
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        _themeService.ToggleTheme();
        IsDarkMode = _themeService.IsDarkMode;
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task ExportAsMarkdown()
    {
        if (string.IsNullOrWhiteSpace(EditTitle))
        {
            SnackbarMessageQueue?.Enqueue("⚠️ Please enter a title before exporting");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Markdown files (*.md)|*.md",
            FileName = $"{EditTitle}.md"
        };

        if (dialog.ShowDialog() == true)
        {
            var tempPrompt = new Prompt
            {
                Title = EditTitle,
                Category = EditCategory,
                Tags = EditTags.Split(',', System.StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList(),
                Content = EditContent,
                CreatedDate = SelectedPrompt?.CreatedDate ?? DateTime.Now
            };

            await _exportService.ExportPromptAsMarkdownAsync(tempPrompt, dialog.FileName);
            SnackbarMessageQueue?.Enqueue("✓ Exported as Markdown!");
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task ExportAsText()
    {
        if (string.IsNullOrWhiteSpace(EditContent))
        {
            SnackbarMessageQueue?.Enqueue("⚠️ No content to export");
            return;
        }

        var fileName = !string.IsNullOrWhiteSpace(EditTitle) ? $"{EditTitle}.txt" : "prompt.txt";

        var dialog = new SaveFileDialog
        {
            Filter = "Text files (*.txt)|*.txt",
            FileName = fileName
        };

        if (dialog.ShowDialog() == true)
        {
            var tempPrompt = new Prompt
            {
                Content = EditContent
            };

            await _exportService.ExportPromptAsTextAsync(tempPrompt, dialog.FileName);
            SnackbarMessageQueue?.Enqueue("✓ Exported as Text!");
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task ExportAllAsJson()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            FileName = "prompts.json"
        };

        if (dialog.ShowDialog() == true)
        {
            await _exportService.ExportAllPromptsAsJsonAsync(Prompts.ToList(), dialog.FileName);
            SnackbarMessageQueue?.Enqueue("✓ Exported all prompts!");
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task ImportFromJson()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json"
        };

        if (dialog.ShowDialog() == true)
        {
            var importedPrompts = await _exportService.ImportPromptsFromJsonAsync(dialog.FileName);
            
            foreach (var prompt in importedPrompts)
            {
                prompt.Id = 0; // Reset ID to create new entries
                await _databaseService.SavePromptAsync(prompt);
            }
            
            await LoadData();
            SnackbarMessageQueue?.Enqueue($"✓ Imported {importedPrompts.Count} prompts!");
        }
    }

    private void ApplyFilters()
    {
        var filtered = Prompts.ToList();

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            filtered = _searchService.Search(filtered, SearchQuery);
        }

        if (!string.IsNullOrWhiteSpace(SelectedCategory))
        {
            filtered = _searchService.FilterByCategory(filtered, SelectedCategory);
        }

        if (!string.IsNullOrWhiteSpace(SelectedTag))
        {
            filtered = _searchService.FilterByTag(filtered, SelectedTag);
        }

        FilteredPrompts = new ObservableCollection<Prompt>(filtered);
    }

    private void ClearEditor()
    {
        EditTitle = string.Empty;
        EditCategory = string.Empty;
        EditTags = string.Empty;
        EditContent = string.Empty;
    }
}
