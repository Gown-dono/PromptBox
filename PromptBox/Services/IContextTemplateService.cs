using System.Collections.Generic;
using System.Threading.Tasks;
using PromptBox.Models;

namespace PromptBox.Services;

public interface IContextTemplateService
{
    Task<List<ContextTemplate>> GetAllTemplatesAsync();
    Task<ContextTemplate?> GetTemplateByIdAsync(int id);
    Task<int> SaveTemplateAsync(ContextTemplate template);
    Task<bool> DeleteTemplateAsync(int id);
}