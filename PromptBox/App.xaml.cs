using Microsoft.Extensions.DependencyInjection;
using PromptBox.Services;
using PromptBox.ViewModels;
using PromptBox.Views;
using System.Windows;

namespace PromptBox;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Error starting application: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<ISearchService, SearchService>();
        services.AddSingleton<IPromptLibraryService, PromptLibraryService>();
        services.AddSingleton<IVersioningService, VersioningService>();
        services.AddSingleton<ISecureStorageService, SecureStorageService>();
        services.AddSingleton<IAIService, AIService>();
        services.AddSingleton<IModelPricingService, ModelPricingService>();
        services.AddSingleton<IPromptSuggestionService, PromptSuggestionService>();
        services.AddSingleton<IWorkflowService, WorkflowService>();
        services.AddSingleton<IBatchProcessingService, BatchProcessingService>();
        services.AddSingleton<IPromptTestingService, PromptTestingService>();
        services.AddSingleton<IPromptComparisonService, PromptComparisonService>();

        // ViewModels
        services.AddTransient<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
