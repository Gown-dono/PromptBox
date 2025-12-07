using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using PromptBox.Models;
using PromptBox.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PromptBox.Views;

public partial class AISettingsDialog : Window
{
    private readonly ISecureStorageService _secureStorage;
    private readonly IAIService _aiService;
    public ObservableCollection<ProviderViewModel> Providers { get; } = new();

    public AISettingsDialog(ISecureStorageService secureStorage, IAIService aiService)
    {
        InitializeComponent();
        _secureStorage = secureStorage;
        _aiService = aiService;
        
        ProvidersPanel.ItemsSource = Providers;
        Loaded += async (s, e) => await InitializeProvidersAsync();
    }
    
    private void ShowMessage(string message)
    {
        StatusText.Text = message;
    }
    
    private async Task<bool> ShowConfirmationAsync(string message, string title)
    {
        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
            Foreground = (Brush)FindResource("MaterialDesignBody")
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Style = (Style)FindResource("MaterialDesignFlatButton"),
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = false,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var confirmButton = new Button
        {
            Content = "OK",
            Style = (Style)FindResource("MaterialDesignRaisedButton"),
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = true
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(confirmButton);

        var mainPanel = new StackPanel { Margin = new Thickness(16), MinWidth = 300 };
        mainPanel.Children.Add(new TextBlock 
        { 
            Text = title, 
            FontSize = 16, 
            FontWeight = FontWeights.SemiBold, 
            Margin = new Thickness(0, 0, 0, 12),
            Foreground = (Brush)FindResource("MaterialDesignBody")
        });
        mainPanel.Children.Add(messageText);
        mainPanel.Children.Add(buttonPanel);

        var result = await DialogHost.Show(mainPanel, "AISettingsDialog");
        return result is bool confirmed && confirmed;
    }

    private async Task InitializeProvidersAsync()
    {
        try
        {
            var providerInfos = new[]
            {
                (AIProviders.OpenAI, "OpenAI", "Cloud", ""),
                (AIProviders.Anthropic, "Anthropic", "Brain", ""),
                (AIProviders.Google, "Google", "Google", ""),
                (AIProviders.Mistral, "Mistral AI", "Wrench", ""),
                (AIProviders.Groq, "Groq", "Flash", "")
            };

            foreach (var (provider, name, icon, description) in providerInfos)
            {
                bool hasKey = false;
                try
                {
                    hasKey = await _secureStorage.HasApiKeyAsync(provider);
                }
                catch
                {
                    // Ignore errors checking for existing keys
                }
                
                var vm = new ProviderViewModel
                {
                    Provider = provider,
                    Name = name,
                    Icon = icon,
                    Description = description,
                    HasKey = hasKey,
                    StatusText = hasKey ? "Configured" : "Not Set",
                    StatusColor = hasKey ? new SolidColorBrush(Color.FromRgb(76, 175, 80)) : new SolidColorBrush(Color.FromRgb(158, 158, 158))
                };
                
                vm.SaveCommand = new AsyncRelayCommand(async () => await SaveApiKey(vm));
                vm.DeleteCommand = new AsyncRelayCommand(async () => await DeleteApiKey(vm));
                
                Providers.Add(vm);
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"❌ Error loading providers: {ex.Message}");
        }
    }

    private async Task SaveApiKey(ProviderViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(vm.ApiKeyInput))
        {
            ShowMessage("⚠️ Please enter an API key");
            return;
        }

        try
        {
            ShowMessage($"Saving API key for {vm.Name}...");
            
            // Store the key before clearing the input
            var keyToSave = vm.ApiKeyInput;
            
            // Save the key
            await _secureStorage.SaveApiKeyAsync(vm.Provider, keyToSave);
            
            // Verify the key was saved correctly by reading it back
            var savedKey = await _secureStorage.GetApiKeyAsync(vm.Provider);
            if (string.IsNullOrEmpty(savedKey))
            {
                ShowMessage($"❌ Failed to save API key for {vm.Name}. The key could not be stored securely.");
                return;
            }
            
            // Key was saved successfully, now validate it with the API
            ShowMessage($"Validating API key for {vm.Name}...");
            var isValid = await _aiService.ValidateApiKeyAsync(vm.Provider, keyToSave);
            
            // Update UI only after successful save
            vm.HasKey = true;
            vm.ApiKeyInput = string.Empty;
            
            if (isValid)
            {
                vm.StatusText = "Configured";
                vm.StatusColor = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                ShowMessage($"✓ API key for {vm.Name} saved and validated!");
            }
            else
            {
                vm.StatusText = "Saved (unverified)";
                vm.StatusColor = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Yellow/warning
                ShowMessage($"⚠️ API key for {vm.Name} saved but could not be verified. It may still work.");
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"❌ Error saving API key: {ex.Message}");
        }
    }

    private async Task DeleteApiKey(ProviderViewModel vm)
    {
        var confirmed = await ShowConfirmationAsync(
            $"Are you sure you want to delete the API key for {vm.Name}?",
            "Confirm Delete");

        if (confirmed)
        {
            await _secureStorage.DeleteApiKeyAsync(vm.Provider);
            vm.HasKey = false;
            vm.StatusText = "Not Set";
            vm.StatusColor = new SolidColorBrush(Color.FromRgb(158, 158, 158));
            
            ShowMessage($"✓ API key for {vm.Name} deleted");
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}

public partial class ProviderViewModel : ObservableObject
{
    public string Provider { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    [ObservableProperty]
    private bool _hasKey;
    
    [ObservableProperty]
    private string _statusText = string.Empty;
    
    [ObservableProperty]
    private SolidColorBrush _statusColor = new(Colors.Gray);
    
    [ObservableProperty]
    private string _apiKeyInput = string.Empty;
    
    public IAsyncRelayCommand? SaveCommand { get; set; }
    public IAsyncRelayCommand? DeleteCommand { get; set; }
}
