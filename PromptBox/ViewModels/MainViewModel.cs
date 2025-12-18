using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using PromptBox.Models;
using PromptBox.Services;
using PromptBox.Views;
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
    private readonly IPromptLibraryService _promptLibraryService;
    private readonly IVersioningService _versioningService;
    private readonly ISecureStorageService _secureStorageService;
    private readonly IAIService _aiService;
    private readonly IPromptSuggestionService _promptSuggestionService;
    private readonly IWorkflowService _workflowService;
    private readonly IBatchProcessingService _batchProcessingService;
    private readonly IPromptTestingService _promptTestingService;
    private readonly IPromptComparisonService _promptComparisonService;
    private readonly IGitContextService _gitContextService;
    private readonly IDatabaseContextService _databaseContextService;
    private readonly IApiContextService _apiContextService;
    private readonly IWebScrapingService _webScrapingService;
    private readonly IContextTemplateService _contextTemplateService;
    private readonly IPromptCommunityService _promptCommunityService;
    
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
        ISearchService searchService,
        IPromptLibraryService promptLibraryService,
        IVersioningService versioningService,
        ISecureStorageService secureStorageService,
        IAIService aiService,
        IPromptSuggestionService promptSuggestionService,
        IWorkflowService workflowService,
        IBatchProcessingService batchProcessingService,
        IPromptTestingService promptTestingService,
        IPromptComparisonService promptComparisonService,
        IGitContextService gitContextService,
        IDatabaseContextService databaseContextService,
        IApiContextService apiContextService,
        IWebScrapingService webScrapingService,
        IContextTemplateService contextTemplateService,
        IPromptCommunityService promptCommunityService)
    {
        _databaseService = databaseService;
        _themeService = themeService;
        _exportService = exportService;
        _searchService = searchService;
        _promptLibraryService = promptLibraryService;
        _versioningService = versioningService;
        _secureStorageService = secureStorageService;
        _aiService = aiService;
        _promptSuggestionService = promptSuggestionService;
        _workflowService = workflowService;
        _batchProcessingService = batchProcessingService;
        _promptTestingService = promptTestingService;
        _promptComparisonService = promptComparisonService;
        _gitContextService = gitContextService;
        _databaseContextService = databaseContextService;
        _apiContextService = apiContextService;
        _webScrapingService = webScrapingService;
        _contextTemplateService = contextTemplateService;
        _promptCommunityService = promptCommunityService;
        
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
        
        // Save version before updating (only for existing prompts with changes)
        if (prompt.Id != 0 && HasChanges(prompt))
        {
            await _versioningService.SaveVersionAsync(prompt);
        }
        
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
    
    private bool HasChanges(Prompt prompt)
    {
        var currentTags = string.Join(", ", prompt.Tags);
        var editTagsNormalized = string.Join(", ", EditTags
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t)));
        
        return prompt.Title != EditTitle ||
               prompt.Category != EditCategory ||
               prompt.Content != EditContent ||
               currentTags != editTagsNormalized;
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
            await _versioningService.DeleteVersionsForPromptAsync(SelectedPrompt.Id);
            await _databaseService.DeletePromptAsync(SelectedPrompt.Id);
            await LoadData();
            ClearEditor();
            SnackbarMessageQueue?.Enqueue("✓ Prompt deleted");
        }
    }

    [RelayCommand]
    private void ShareToCommunity()
    {
        if (string.IsNullOrWhiteSpace(EditTitle) || string.IsNullOrWhiteSpace(EditContent))
        {
            SnackbarMessageQueue?.Enqueue("⚠️ Please enter a title and content before sharing");
            return;
        }

        var dialog = new TemplateSubmissionDialog(_promptCommunityService)
        {
            Owner = Application.Current.MainWindow
        };

        // Pre-populate with current prompt data
        dialog.TemplateTitle = EditTitle;
        dialog.TemplateContent = EditContent;
        dialog.SelectedCategory = string.IsNullOrWhiteSpace(EditCategory) ? "Coding" : EditCategory;
        dialog.Tags = EditTags;

        dialog.ShowDialog();
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

    [RelayCommand]
    private async System.Threading.Tasks.Task ExportWorkflows()
    {
        var customWorkflows = await _workflowService.GetCustomWorkflowsAsync();
        
        if (!customWorkflows.Any())
        {
            SnackbarMessageQueue?.Enqueue("⚠️ No custom workflows to export");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            FileName = "workflows.json"
        };

        if (dialog.ShowDialog() == true)
        {
            await _exportService.ExportWorkflowsAsJsonAsync(customWorkflows, dialog.FileName);
            SnackbarMessageQueue?.Enqueue($"✓ Exported {customWorkflows.Count} workflows!");
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task ImportWorkflows()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json"
        };

        if (dialog.ShowDialog() == true)
        {
            var importedWorkflows = await _exportService.ImportWorkflowsFromJsonAsync(dialog.FileName);
            
            foreach (var workflow in importedWorkflows)
            {
                workflow.Id = 0; // Reset ID to create new entries
                workflow.IsBuiltIn = false;
                await _workflowService.SaveWorkflowAsync(workflow);
            }
            
            SnackbarMessageQueue?.Enqueue($"✓ Imported {importedWorkflows.Count} workflows!");
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task ExportWithHistory()
    {
        if (!Prompts.Any())
        {
            SnackbarMessageQueue?.Enqueue("⚠️ No prompts to export");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            FileName = "prompts_with_history.json"
        };

        if (dialog.ShowDialog() == true)
        {
            var allVersions = await _versioningService.GetAllVersionsAsync();
            await _exportService.ExportPromptsWithHistoryAsJsonAsync(Prompts.ToList(), allVersions, dialog.FileName);
            SnackbarMessageQueue?.Enqueue($"✓ Exported {Prompts.Count} prompts with {allVersions.Count} versions!");
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task ImportWithHistory()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json"
        };

        if (dialog.ShowDialog() == true)
        {
            var (importedPrompts, importedVersions) = await _exportService.ImportPromptsWithHistoryFromJsonAsync(dialog.FileName);
            
            // Create a mapping from old prompt IDs to new prompt IDs
            var idMapping = new System.Collections.Generic.Dictionary<int, int>();
            
            foreach (var prompt in importedPrompts)
            {
                var oldId = prompt.Id;
                prompt.Id = 0; // Reset ID to create new entries
                var newId = await _databaseService.SavePromptAsync(prompt);
                prompt.Id = newId; // Update the in-memory object with the new ID
                idMapping[oldId] = newId;
            }
            
            // Update version PromptIds to match new prompt IDs and save
            foreach (var version in importedVersions)
            {
                if (idMapping.TryGetValue(version.PromptId, out var newId))
                {
                    version.PromptId = newId;
                }
            }
            await _versioningService.SaveVersionsAsync(importedVersions);
            
            await LoadData();
            SnackbarMessageQueue?.Enqueue($"✓ Imported {importedPrompts.Count} prompts with {importedVersions.Count} versions!");
        }
    }

    [RelayCommand]
    private void BrowseLibrary()
    {
        var dialog = new LibraryBrowserDialog(_promptLibraryService, _promptCommunityService)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true && dialog.ImportedPrompt != null)
        {
            // Load the imported template into the editor
            SelectedPrompt = null;
            EditTitle = dialog.ImportedPrompt.Title;
            EditCategory = dialog.ImportedPrompt.Category;
            EditTags = string.Join(", ", dialog.ImportedPrompt.Tags);
            EditContent = dialog.ImportedPrompt.Content;
            
            SnackbarMessageQueue?.Enqueue("✓ Template loaded! Edit and save to add to your prompts.");
        }
    }

    [RelayCommand]
    private void OpenAdminPanel()
    {
        // Secret admin access - only accessible via keyboard shortcut or hidden menu
        var dialog = new AdminModerationDialog(_promptCommunityService)
        {
            Owner = Application.Current.MainWindow
        };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void ViewHistory()
    {
        if (SelectedPrompt == null || SelectedPrompt.Id == 0)
        {
            SnackbarMessageQueue?.Enqueue("⚠️ Please select a saved prompt to view history");
            return;
        }

        var dialog = new VersionHistoryDialog(_versioningService, SelectedPrompt)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true && dialog.RestoredVersion != null)
        {
            // Restore the selected version to the editor
            EditTitle = dialog.RestoredVersion.Title;
            EditCategory = dialog.RestoredVersion.Category;
            EditTags = string.Join(", ", dialog.RestoredVersion.Tags);
            EditContent = dialog.RestoredVersion.Content;
            
            SnackbarMessageQueue?.Enqueue("✓ Version restored! Click Save to apply changes.");
        }
    }

    [RelayCommand]
    private void OpenPromptBuilder()
    {
        var dialog = new PromptBuilderDialog(
            _aiService, 
            _secureStorageService, 
            _promptSuggestionService,
            _gitContextService,
            _databaseContextService,
            _apiContextService,
            _webScrapingService,
            _contextTemplateService)
        {
            Owner = Application.Current.MainWindow,
            InitialPrompt = EditContent
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.ResultPrompt))
        {
            EditContent = dialog.ResultPrompt;
            SnackbarMessageQueue?.Enqueue("✓ Prompt loaded from AI Builder");
        }
    }

    [RelayCommand]
    private void OpenWorkflows()
    {
        var dialog = new WorkflowDialog(_workflowService, _aiService, _secureStorageService)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.ResultPrompt))
        {
            EditContent = dialog.ResultPrompt;
            SnackbarMessageQueue?.Enqueue("✓ Workflow result loaded");
        }
    }

    [RelayCommand]
    private void OpenBatchProcessing()
    {
        var dialog = new BatchProcessingDialog(_batchProcessingService, _databaseService, _aiService, _exportService)
        {
            Owner = Application.Current.MainWindow
        };

        dialog.ShowDialog();
        
        // Show appropriate message based on batch outcome
        switch (dialog.Outcome)
        {
            case BatchOutcome.Completed:
                SnackbarMessageQueue?.Enqueue($"✓ Batch processing completed ({dialog.ResultCount} results)");
                break;
            case BatchOutcome.Cancelled:
                if (dialog.ResultCount > 0)
                    SnackbarMessageQueue?.Enqueue($"Batch cancelled ({dialog.ResultCount} results before cancellation)");
                break;
            case BatchOutcome.Failed:
                SnackbarMessageQueue?.Enqueue("❌ Batch processing failed");
                break;
            // BatchOutcome.None - user closed without running, no message needed
        }
    }

    [RelayCommand]
    private void OpenPromptTesting()
    {
        var dialog = new PromptTestingDialog(_promptTestingService, _databaseService, _aiService, _exportService)
        {
            Owner = Application.Current.MainWindow
        };

        dialog.ShowDialog();

        var outcome = dialog.Outcome;
        if (outcome.TestsExecuted > 0)
        {
            SnackbarMessageQueue?.Enqueue($"✓ Executed {outcome.TestsExecuted} tests ({outcome.PassedTests} passed, {outcome.FailedTests} failed)");
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task OpenPromptComparison()
    {
        var dialog = new PromptComparisonDialog(
            _promptComparisonService,
            _databaseService,
            _aiService,
            _exportService,
            _versioningService)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            // Refresh prompts if a winner was saved
            await LoadData();
            SnackbarMessageQueue?.Enqueue("✓ Winner saved to prompt library");
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task OpenSettings()
    {
        var dialog = new SettingsDialog(_exportService, _databaseService, _versioningService, _workflowService, _themeService)
        {
            Owner = Application.Current.MainWindow
        };

        dialog.ShowDialog();
        
        // Sync theme state after dialog closes
        IsDarkMode = _themeService.IsDarkMode;
        
        if (dialog.DataChanged)
        {
            await LoadData();
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
