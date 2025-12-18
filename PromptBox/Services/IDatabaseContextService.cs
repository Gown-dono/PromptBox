using System.Collections.Generic;
using System.Threading.Tasks;

namespace PromptBox.Services;

public interface IDatabaseContextService
{
    Task<string> ExecuteQueryAsync(string connectionString, string query, string dbType);
    Task<bool> TestConnectionAsync(string connectionString, string dbType);
    List<string> GetSupportedDatabaseTypes();
}