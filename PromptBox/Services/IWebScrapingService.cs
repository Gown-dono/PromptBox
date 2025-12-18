using System.Threading.Tasks;

namespace PromptBox.Services;

public interface IWebScrapingService
{
    Task<string> ScrapeWebPageAsync(string url, bool includeHtml = false);
    Task<string> ExtractTextContentAsync(string url);
    Task<string> ExtractLinksAsync(string url);
}