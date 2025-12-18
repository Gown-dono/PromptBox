using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace PromptBox.Services;

public class WebScrapingService : IWebScrapingService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public async Task<string> ScrapeWebPageAsync(string url, bool includeHtml = false)
    {
        try
        {
            // Validate URL
            if (string.IsNullOrWhiteSpace(url))
                return "Error: URL is required";

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                return "Error: Invalid URL format";

            var doc = await LoadWithTimeoutAsync(url);
            if (doc == null)
                return "Error: Web page request timed out. The server took too long to respond.";

            var sb = new StringBuilder();
            sb.AppendLine($"## Web Page Content");
            sb.AppendLine($"**URL:** {url}");
            sb.AppendLine();

            // Extract title
            var titleNode = doc.DocumentNode.SelectSingleNode("//head/title");
            var title = titleNode?.InnerText ?? "No title found";
            sb.AppendLine($"### Title");
            sb.AppendLine(title);
            sb.AppendLine();

            // Extract meta description
            var metaDescNode = doc.DocumentNode.SelectSingleNode("//meta[@name='description']");
            var metaDesc = metaDescNode?.GetAttributeValue("content", "") ?? "";
            if (!string.IsNullOrEmpty(metaDesc))
            {
                sb.AppendLine($"### Description");
                sb.AppendLine(metaDesc);
                sb.AppendLine();
            }

            // Extract main content
            sb.AppendLine($"### Content");

            // Try to find main content areas
            var contentNodes = doc.DocumentNode.SelectNodes("//main") ??
                              doc.DocumentNode.SelectNodes("//article") ??
                              doc.DocumentNode.SelectNodes("//div[@class='content']") ??
                              doc.DocumentNode.SelectNodes("//div[@id='content']") ??
                              doc.DocumentNode.SelectNodes("//body//p");

            if (contentNodes != null)
            {
                foreach (var node in contentNodes.Take(20)) // Limit to first 20 nodes to avoid huge content
                {
                    var text = CleanText(node.InnerText);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                    }
                }
            }
            else
            {
                // Fallback to body text
                var bodyNode = doc.DocumentNode.SelectSingleNode("//body");
                if (bodyNode != null)
                {
                    var text = CleanText(bodyNode.InnerText);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                    }
                }
            }

            // Include raw HTML if requested
            if (includeHtml)
            {
                sb.AppendLine();
                sb.AppendLine("### Raw HTML");
                sb.AppendLine("```html");
                sb.AppendLine(doc.DocumentNode.OuterHtml);
                sb.AppendLine("```");
            }

            return sb.ToString();
        }
        catch (TaskCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("Web page scraping timed out");
            return "Error: Web page request timed out. The server took too long to respond.";
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("Web page scraping was cancelled");
            return "Error: Web page request was cancelled.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Web page scraping failed: {ex.Message}");
            return $"Error scraping web page: {ex.Message}";
        }
    }

    public async Task<string> ExtractTextContentAsync(string url)
    {
        try
        {
            // Validate URL
            if (string.IsNullOrWhiteSpace(url))
                return "Error: URL is required";

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                return "Error: Invalid URL format";

            var doc = await LoadWithTimeoutAsync(url);
            if (doc == null)
                return "Error: Web page request timed out. The server took too long to respond.";

            var sb = new StringBuilder();

            // Extract all text content
            var textNodes = doc.DocumentNode.SelectNodes("//text()");
            if (textNodes != null)
            {
                foreach (var node in textNodes)
                {
                    var text = CleanText(node.InnerText);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                    }
                }
            }

            return sb.ToString();
        }
        catch (TaskCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("Text content extraction timed out");
            return "Error: Web page request timed out. The server took too long to respond.";
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("Text content extraction was cancelled");
            return "Error: Web page request was cancelled.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Text content extraction failed: {ex.Message}");
            return $"Error extracting text content: {ex.Message}";
        }
    }

    public async Task<string> ExtractLinksAsync(string url)
    {
        try
        {
            // Validate URL
            if (string.IsNullOrWhiteSpace(url))
                return "Error: URL is required";

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                return "Error: Invalid URL format";

            var doc = await LoadWithTimeoutAsync(url);
            if (doc == null)
                return "Error: Web page request timed out. The server took too long to respond.";

            var sb = new StringBuilder();
            sb.AppendLine($"## Links from {url}");
            sb.AppendLine();

            // Extract all links
            var linkNodes = doc.DocumentNode.SelectNodes("//a[@href]");
            if (linkNodes != null)
            {
                foreach (var link in linkNodes)
                {
                    var href = link.GetAttributeValue("href", "");
                    var text = CleanText(link.InnerText);

                    if (!string.IsNullOrWhiteSpace(href))
                    {
                        sb.AppendLine($"- [{text}]({href})");
                    }
                }
            }

            return sb.ToString();
        }
        catch (TaskCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("Link extraction timed out");
            return "Error: Web page request timed out. The server took too long to respond.";
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("Link extraction was cancelled");
            return "Error: Web page request was cancelled.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Link extraction failed: {ex.Message}");
            return $"Error extracting links: {ex.Message}";
        }
    }

    /// <summary>
    /// Loads a web page with a timeout to prevent hanging on slow networks
    /// </summary>
    private async Task<HtmlDocument?> LoadWithTimeoutAsync(string url)
    {
        using var cts = new CancellationTokenSource(DefaultTimeout);

        try
        {
            var web = new HtmlWeb();
            var loadTask = web.LoadFromWebAsync(url, cts.Token);
            var completedTask = await Task.WhenAny(loadTask, Task.Delay(DefaultTimeout, cts.Token));

            if (completedTask == loadTask)
            {
                return await loadTask;
            }
            else
            {
                // Timeout occurred
                return null;
            }
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Remove extra whitespace and normalize
        return System.Text.RegularExpressions.Regex.Replace(text.Trim(), @"\s+", " ");
    }
}
