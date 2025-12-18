using System.Collections.Generic;
using System.Threading.Tasks;

namespace PromptBox.Services;

public interface IApiContextService
{
    Task<string> FetchApiDataAsync(string url, string method, Dictionary<string, string>? headers = null, string? body = null);
    Task<bool> TestEndpointAsync(string url);
}