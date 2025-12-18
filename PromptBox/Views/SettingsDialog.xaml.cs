using Microsoft.Win32;
using PromptBox.Models;
using PromptBox.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;

namespace PromptBox.Views;

public partial class SettingsDialog : Window, INotifyPropertyChanged
{
    private readonly IExportService _exportService;
    private readonly IDatabaseService _databaseService;
    private readonly IVersioningService _versioningService;
    private readonly IWorkflowService _workflowService;
    private readonly IThemeService _themeService;
    
    private bool _enableCommunityTemplates = true;
    private bool _autoRefreshCommunity = false;
    private int _cacheDurationHours = 24;
    
    public bool DataChanged { get; private set; }
    
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool EnableCommunityTemplates
    {
        get => _enableCommunityTemplates;
        set { _enableCommunityTemplates = value; OnPropertyChanged(); SaveCommunitySettings(); }
    }

    public bool AutoRefreshCommunity
    {
        get => _autoRefreshCommunity;
        set { _autoRefreshCommunity = value; OnPropertyChanged(); SaveCommunitySettings(); }
    }

    public int CacheDurationHours
    {
        get => _cacheDurationHours;
        set { _cacheDurationHours = value; OnPropertyChanged(); SaveCommunitySettings(); }
    }

    public SettingsDialog(
        IExportService exportService,
        IDatabaseService databaseService,
        IVersioningService versioningService,
        IWorkflowService workflowService,
        IThemeService themeService)
    {
        InitializeComponent();
        _exportService = exportService;
        _databaseService = databaseService;
        _versioningService = versioningService;
        _workflowService = workflowService;
        _themeService = themeService;
        
        DataContext = this;
        DarkModeToggle.IsChecked = _themeService.IsDarkMode;
        LoadCommunitySettings();
    }
    
    private void LoadCommunitySettings()
    {
        EnableCommunityTemplates = Properties.Settings.Default.EnableCommunityTemplates;
        AutoRefreshCommunity = Properties.Settings.Default.AutoRefreshCommunity;
        CacheDurationHours = Properties.Settings.Default.CacheDurationHours;
    }
    
    private void SaveCommunitySettings()
    {
        Properties.Settings.Default.EnableCommunityTemplates = EnableCommunityTemplates;
        Properties.Settings.Default.AutoRefreshCommunity = AutoRefreshCommunity;
        Properties.Settings.Default.CacheDurationHours = CacheDurationHours;
        Properties.Settings.Default.Save();
    }
    
    private async void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        await _databaseService.DeleteExpiredCacheAsync();
        ShowStatus("✓ Community cache cleared!");
    }
    
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    private void DarkModeToggle_Click(object sender, RoutedEventArgs e)
    {
        _themeService.ToggleTheme();
    }

    private void ShowStatus(string message)
    {
        StatusText.Text = message;
    }

    private async void ExportPrompts_Click(object sender, RoutedEventArgs e)
    {
        var prompts = await _databaseService.GetAllPromptsAsync();
        if (!prompts.Any())
        {
            ShowStatus("⚠️ No prompts to export");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            FileName = "prompts.json"
        };

        if (dialog.ShowDialog() == true)
        {
            await _exportService.ExportAllPromptsAsJsonAsync(prompts, dialog.FileName);
            ShowStatus($"✓ Exported {prompts.Count} prompts!");
        }
    }

    private async void ImportPrompts_Click(object sender, RoutedEventArgs e)
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
                prompt.Id = 0;
                await _databaseService.SavePromptAsync(prompt);
            }
            
            DataChanged = true;
            ShowStatus($"✓ Imported {importedPrompts.Count} prompts!");
        }
    }

    private async void ExportWithHistory_Click(object sender, RoutedEventArgs e)
    {
        var prompts = await _databaseService.GetAllPromptsAsync();
        if (!prompts.Any())
        {
            ShowStatus("⚠️ No prompts to export");
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
            await _exportService.ExportPromptsWithHistoryAsJsonAsync(prompts, allVersions, dialog.FileName);
            ShowStatus($"✓ Exported {prompts.Count} prompts with {allVersions.Count} versions!");
        }
    }

    private async void ImportWithHistory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json"
        };

        if (dialog.ShowDialog() == true)
        {
            var (importedPrompts, importedVersions) = await _exportService.ImportPromptsWithHistoryFromJsonAsync(dialog.FileName);
            
            var idMapping = new Dictionary<int, int>();
            
            foreach (var prompt in importedPrompts)
            {
                var oldId = prompt.Id;
                prompt.Id = 0;
                await _databaseService.SavePromptAsync(prompt);
                idMapping[oldId] = prompt.Id;
            }
            
            foreach (var version in importedVersions)
            {
                if (idMapping.TryGetValue(version.PromptId, out var newId))
                {
                    version.PromptId = newId;
                }
            }
            await _versioningService.SaveVersionsAsync(importedVersions);
            
            DataChanged = true;
            ShowStatus($"✓ Imported {importedPrompts.Count} prompts with {importedVersions.Count} versions!");
        }
    }

    private async void ExportWorkflows_Click(object sender, RoutedEventArgs e)
    {
        var customWorkflows = await _workflowService.GetCustomWorkflowsAsync();
        
        if (!customWorkflows.Any())
        {
            ShowStatus("⚠️ No custom workflows to export");
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
            ShowStatus($"✓ Exported {customWorkflows.Count} workflows!");
        }
    }

    private async void ImportWorkflows_Click(object sender, RoutedEventArgs e)
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
                workflow.Id = 0;
                workflow.IsBuiltIn = false;
                await _workflowService.SaveWorkflowAsync(workflow);
            }
            
            ShowStatus($"✓ Imported {importedWorkflows.Count} workflows!");
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }
}
