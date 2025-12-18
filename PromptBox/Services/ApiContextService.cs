using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PromptBox.Services;

public class ApiContextService : IApiContextService
{
    private static readonly HttpClient _httpClient;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    static ApiContextService()
    {
        _httpClient = new HttpClient
        {
            Timeout = DefaultTimeout
        };
    }

    public async Task<bool> TestEndpointAsync(string url)
    {
        try
        {
            // Validate URL
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                return false;

            var response = await _httpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch (TaskCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("API endpoint test timed out");
            return false;
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("API endpoint test was cancelled");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"API endpoint test failed: {ex.Message}");
            return false;
        }
    }

    public async Task<string> FetchApiDataAsync(string url, string method, Dictionary<string, string>? headers = null, string? body = null)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(url))
                return "Error: URL is required";

            if (string.IsNullOrWhiteSpace(method))
                return "Error: HTTP method is required";

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                return "Error: Invalid URL format";

            // Validate HTTP method
            var validMethods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };
            if (!validMethods.Contains(method.ToUpper()))
                return $"Error: Invalid HTTP method '{method}'. Valid methods are: {string.Join(", ", validMethods)}";

            // Set up the request message
            var request = new HttpRequestMessage(new HttpMethod(method), url);

            // Add headers
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    // Skip empty keys or values
                    if (string.IsNullOrWhiteSpace(header.Key) || string.IsNullOrWhiteSpace(header.Value))
                        continue;

                    // Special handling for Content-Type header
                    if (header.Key.ToLower() == "content-type")
                    {
                        // Create content if it doesn't exist yet
                        if (request.Content == null)
                        {
                            request.Content = new StringContent(string.Empty);
                        }
                        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(header.Value);
                    }
                    else
                    {
                        request.Headers.Add(header.Key, header.Value);
                    }
                }
            }

            // Add body for POST, PUT, PATCH requests
            if (!string.IsNullOrEmpty(body) && (method.ToUpper() == "POST" || method.ToUpper() == "PUT" || method.ToUpper() == "PATCH"))
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            // Send the request
            var response = await _httpClient.SendAsync(request);

            // Format the response
            var sb = new StringBuilder();
            sb.AppendLine($"## API Response");
            sb.AppendLine($"**Status Code:** {(int)response.StatusCode} {response.StatusCode}");
            sb.AppendLine();

            // Add headers
            sb.AppendLine("**Response Headers:**");
            foreach (var header in response.Headers)
            {
                sb.AppendLine($"- {header.Key}: {string.Join(", ", header.Value)}");
            }
            sb.AppendLine();

            // Add body
            var responseBody = await response.Content.ReadAsStringAsync();
            sb.AppendLine("**Response Body:**");

            // Try to format as JSON if it looks like JSON
            if (IsJson(responseBody))
            {
                try
                {
                    using var jsonDoc = JsonDocument.Parse(responseBody);
                    var prettyJson = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
                    sb.AppendLine("```json");
                    sb.AppendLine(prettyJson);
                    sb.AppendLine("```");
                }
                catch
                {
                    sb.AppendLine("```");
                    sb.AppendLine(responseBody);
                    sb.AppendLine("```");
                }
            }
            else
            {
                sb.AppendLine("```");
                sb.AppendLine(responseBody);
                sb.AppendLine("```");
            }

            return sb.ToString();
        }
        catch (TaskCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("API request timed out");
            return "Error: API request timed out. The server took too long to respond.";
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("API request was cancelled");
            return "Error: API request was cancelled.";
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"HTTP request failed: {ex.Message}");
            return $"Error: HTTP request failed - {ex.Message}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"API data fetch failed: {ex.Message}");
            return $"Error fetching API data: {ex.Message}";
        }
    }

    private bool IsJson(string strInput)
    {
        strInput = strInput.Trim();
        return strInput.StartsWith("{") && strInput.EndsWith("}")
               || strInput.StartsWith("[") && strInput.EndsWith("]");
    }
}
