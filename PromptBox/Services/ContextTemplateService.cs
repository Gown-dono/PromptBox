using System.Collections.Generic;
using System.Threading.Tasks;
using PromptBox.Models;

namespace PromptBox.Services;

/// <summary>
/// Service for managing context templates using the shared database
/// </summary>
public class ContextTemplateService : IContextTemplateService
{
    private readonly IDatabaseService _databaseService;

    public ContextTemplateService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<List<ContextTemplate>> GetAllTemplatesAsync()
    {
        return await _databaseService.GetAllContextTemplatesAsync();
    }

    public async Task<ContextTemplate?> GetTemplateByIdAsync(int id)
    {
        return await _databaseService.GetContextTemplateByIdAsync(id);
    }

    public async Task<int> SaveTemplateAsync(ContextTemplate template)
    {
        return await _databaseService.SaveContextTemplateAsync(template);
    }

    public async Task<bool> DeleteTemplateAsync(int id)
    {
        return await _databaseService.DeleteContextTemplateAsync(id);
    }
}
