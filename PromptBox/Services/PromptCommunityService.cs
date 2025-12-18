using PromptBox.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PromptBox.Services;

/// <summary>
/// Service for fetching, caching, and managing community prompt templates
/// </summary>
public class PromptCommunityService : IPromptCommunityService
{
    private readonly IDatabaseService _databaseService;
    private readonly HttpClient _httpClient;
    
    // Configuration constants
    private const string GITHUB_REPO_OWNER = "Gown-dono";
    private const string GITHUB_REPO_NAME = "prompt-templates";
    private const string GITHUB_BRANCH = "main";
    private const string TEMPLATES_PATH = "templates";
    private const int CACHE_TTL_HOURS = 24;
    private const string GITHUB_API_BASE_URL = "https://api.github.com";
    private const string COMMUNITY_API_BASE_URL = "https://promptbox-ratings-api.gown-dono.workers.dev";
    
    private List<PromptTemplate>? _cachedTemplates;
    private DateTime _lastFetchTime = DateTime.MinValue;

    public PromptCommunityService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PromptBox", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<List<PromptTemplate>> FetchCommunityTemplatesAsync(bool forceRefresh = false)
    {
        try
        {
            // Check in-memory cache first
            if (!forceRefresh && _cachedTemplates != null && 
                DateTime.Now - _lastFetchTime < TimeSpan.FromMinutes(5))
            {
                return _cachedTemplates;
            }

            // Check database cache
            if (!forceRefresh)
            {
                var cachedTemplates = await _databaseService.GetCachedCommunityTemplatesAsync();
                var validCached = cachedTemplates.Where(c => c.ExpiresAt > DateTime.Now).ToList();
                
                if (validCached.Any())
                {
                    var templates = validCached
                        .Select(c => DeserializeTemplate(c.JsonData))
                        .Where(t => t != null)
                        .Cast<PromptTemplate>()
                        .ToList();
                    
                    _cachedTemplates = templates;
                    _lastFetchTime = DateTime.Now;
                    return templates;
                }
            }

            // Fetch from GitHub
            System.Diagnostics.Debug.WriteLine("Fetching community templates from GitHub...");
            var communityTemplates = await FetchFromGitHubAsync();
            System.Diagnostics.Debug.WriteLine($"Fetched {communityTemplates.Count} community templates");
            
            // Cache the templates
            await CacheTemplatesAsync(communityTemplates);
            
            _cachedTemplates = communityTemplates;
            _lastFetchTime = DateTime.Now;
            
            // Clean up expired cache
            await _databaseService.DeleteExpiredCacheAsync();
            
            return communityTemplates;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching community templates: {ex.Message}");
            
            // Return cached data if available
            if (_cachedTemplates != null)
                return _cachedTemplates;
            
            // Try to return any cached data from database
            var fallbackCached = await _databaseService.GetCachedCommunityTemplatesAsync();
            return fallbackCached
                .Select(c => DeserializeTemplate(c.JsonData))
                .Where(t => t != null)
                .Cast<PromptTemplate>()
                .ToList();
        }
    }

    public async Task<PromptTemplate?> GetCommunityTemplateByIdAsync(string id)
    {
        var templates = await FetchCommunityTemplatesAsync();
        return templates.FirstOrDefault(t => t.Id == id);
    }


    public Task<(bool Success, string Message)> SubmitTemplateAsync(PromptTemplate template, string submitterInfo)
    {
        try
        {
            // Validate template
            if (string.IsNullOrWhiteSpace(template.Title))
                return Task.FromResult((false, "Template title is required."));
            if (string.IsNullOrWhiteSpace(template.Content))
                return Task.FromResult((false, "Template content is required."));
            if (template.Content.Length > 10000)
                return Task.FromResult((false, "Template content exceeds maximum length (10,000 characters)."));

            // Prepare template for submission
            template.Id = string.IsNullOrEmpty(template.Id) 
                ? Guid.NewGuid().ToString("N")[..12] 
                : template.Id;
            template.SubmittedBy = submitterInfo;
            template.SubmittedDate = DateTime.UtcNow;
            template.LastUpdated = DateTime.UtcNow;
            template.IsCommunity = true;
            template.IsOfficial = false;

            // For now, return instructions for manual submission
            // In a full implementation, this would create a GitHub PR via API
            var message = $"Template prepared for submission!\n\n" +
                         $"To submit your template to the community:\n" +
                         $"1. Fork the repository: github.com/{GITHUB_REPO_OWNER}/{GITHUB_REPO_NAME}\n" +
                         $"2. Add your template JSON to: {TEMPLATES_PATH}/{template.Category.ToLowerInvariant()}/{template.Id}.json\n" +
                         $"3. Create a Pull Request\n\n" +
                         $"Template ID: {template.Id}";

            return Task.FromResult((true, message));
        }
        catch (Exception ex)
        {
            return Task.FromResult((false, $"Error preparing submission: {ex.Message}"));
        }
    }
    public Task<bool> ReportTemplateAsync(string templateId, string reason)
    {
        // In a full implementation, this would send a report to the API
        // For now, just log it locally
        System.Diagnostics.Debug.WriteLine($"Template {templateId} reported: {reason}");
        return Task.FromResult(true);
    }

    public async Task<bool> CheckForUpdatesAsync()
    {
        try
        {
            var cachedTemplates = await _databaseService.GetCachedCommunityTemplatesAsync();
            if (!cachedTemplates.Any())
                return true; // No cache, updates available

            var oldestCache = cachedTemplates.Min(c => c.CachedDate);
            return DateTime.Now - oldestCache > TimeSpan.FromHours(CACHE_TTL_HOURS);
        }
        catch
        {
            return false;
        }
    }

    public async Task<int> GetCommunityTemplateCountAsync()
    {
        var templates = await FetchCommunityTemplatesAsync();
        return templates.Count;
    }

    public async Task RecordDownloadAsync(string templateId)
    {
        // Increment local cached count immediately for UI responsiveness
        if (_cachedTemplates != null)
        {
            var template = _cachedTemplates.FirstOrDefault(t => t.Id == templateId);
            if (template != null)
            {
                template.DownloadCount++;
            }
        }
        
        // Record download to the community API (fire and forget, don't block UI)
        try
        {
            var url = $"{COMMUNITY_API_BASE_URL}/api/downloads/{Uri.EscapeDataString(templateId)}";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<DownloadResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                // Update local cache with server count
                if (result != null && _cachedTemplates != null)
                {
                    var template = _cachedTemplates.FirstOrDefault(t => t.Id == templateId);
                    if (template != null)
                    {
                        template.DownloadCount = result.DownloadCount;
                    }
                }
                System.Diagnostics.Debug.WriteLine($"Download recorded for {templateId}: {result?.DownloadCount}");
            }
        }
        catch (Exception ex)
        {
            // Don't fail if API is unavailable - local count is already incremented
            System.Diagnostics.Debug.WriteLine($"Failed to record download to API: {ex.Message}");
        }
    }


    #region Private Methods

    private async Task<List<PromptTemplate>> FetchFromGitHubAsync()
    {
        var templates = new List<PromptTemplate>();
        
        try
        {
            // Fetch directory listing
            var url = $"{GITHUB_API_BASE_URL}/repos/{GITHUB_REPO_OWNER}/{GITHUB_REPO_NAME}/contents/{TEMPLATES_PATH}?ref={GITHUB_BRANCH}";
            System.Diagnostics.Debug.WriteLine($"Fetching templates from: {url}");
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                // Repository not accessible, return empty list
                System.Diagnostics.Debug.WriteLine($"GitHub API returned status: {response.StatusCode}");
                return new List<PromptTemplate>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var items = JsonSerializer.Deserialize<List<GitHubContentItem>>(content, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (items == null)
                return new List<PromptTemplate>();

            // Process directories (categories)
            foreach (var item in items.Where(i => i.Type == "dir"))
            {
                System.Diagnostics.Debug.WriteLine($"Processing category: {item.Name}");
                var categoryTemplates = await FetchCategoryTemplatesAsync(item.Path);
                System.Diagnostics.Debug.WriteLine($"Found {categoryTemplates.Count} templates in {item.Name}");
                templates.AddRange(categoryTemplates);
            }

            // Process files in root templates folder
            foreach (var item in items.Where(i => i.Type == "file" && i.Name.EndsWith(".json")))
            {
                var template = await FetchTemplateFileAsync(item.DownloadUrl);
                if (template != null)
                {
                    templates.Add(template);
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"Total templates fetched from GitHub: {templates.Count}");
            
            // Fetch download counts from community API and merge
            await MergeDownloadCountsAsync(templates);
        }
        catch (HttpRequestException ex)
        {
            // Network error, return empty list
            System.Diagnostics.Debug.WriteLine($"Network error fetching templates: {ex.Message}");
            return new List<PromptTemplate>();
        }
        catch (TaskCanceledException ex)
        {
            // Timeout, return empty list
            System.Diagnostics.Debug.WriteLine($"Timeout fetching templates: {ex.Message}");
            return new List<PromptTemplate>();
        }
        catch (Exception ex)
        {
            // Unexpected error
            System.Diagnostics.Debug.WriteLine($"Unexpected error fetching templates: {ex.Message}");
            return new List<PromptTemplate>();
        }

        return templates;
    }

    private async Task MergeDownloadCountsAsync(List<PromptTemplate> templates)
    {
        try
        {
            var url = $"{COMMUNITY_API_BASE_URL}/api/downloads";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to fetch download counts: {response.StatusCode}");
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var downloadCounts = JsonSerializer.Deserialize<List<DownloadCountItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (downloadCounts == null) return;

            // Create lookup dictionary for fast merging
            var countLookup = downloadCounts.ToDictionary(d => d.TemplateId, d => d.DownloadCount);
            
            foreach (var template in templates)
            {
                if (countLookup.TryGetValue(template.Id, out var count))
                {
                    template.DownloadCount = count;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"Merged download counts for {countLookup.Count} templates");
        }
        catch (Exception ex)
        {
            // Don't fail template loading if download counts unavailable
            System.Diagnostics.Debug.WriteLine($"Failed to merge download counts: {ex.Message}");
        }
    }

    private async Task<List<PromptTemplate>> FetchCategoryTemplatesAsync(string path)
    {
        var templates = new List<PromptTemplate>();
        
        try
        {
            var url = $"{GITHUB_API_BASE_URL}/repos/{GITHUB_REPO_OWNER}/{GITHUB_REPO_NAME}/contents/{path}?ref={GITHUB_BRANCH}";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
                return templates;

            var content = await response.Content.ReadAsStringAsync();
            var items = JsonSerializer.Deserialize<List<GitHubContentItem>>(content, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (items == null)
                return templates;

            foreach (var item in items.Where(i => i.Type == "file" && i.Name.EndsWith(".json")))
            {
                var template = await FetchTemplateFileAsync(item.DownloadUrl);
                if (template != null)
                {
                    templates.Add(template);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching category templates: {ex.Message}");
        }

        return templates;
    }

    private async Task<PromptTemplate?> FetchTemplateFileAsync(string downloadUrl)
    {
        try
        {
            var response = await _httpClient.GetAsync(downloadUrl);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            var template = JsonSerializer.Deserialize<PromptTemplate>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            
            // Ensure community flags are set
            if (template != null)
            {
                template.IsCommunity = true;
                template.IsOfficial = false;
            }
            
            return template;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching template file: {ex.Message}");
            return null;
        }
    }

    private async Task CacheTemplatesAsync(List<PromptTemplate> templates)
    {
        var expiresAt = DateTime.Now.AddHours(CACHE_TTL_HOURS);
        
        foreach (var template in templates)
        {
            var cached = new CachedCommunityTemplate
            {
                TemplateId = template.Id,
                JsonData = JsonSerializer.Serialize(template),
                CachedDate = DateTime.Now,
                ExpiresAt = expiresAt
            };
            
            await _databaseService.SaveCachedCommunityTemplateAsync(cached);
        }
    }

    private PromptTemplate? DeserializeTemplate(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<PromptTemplate>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
        }
        catch
        {
            return null;
        }
    }

    private List<PromptTemplate> GetSampleCommunityTemplates()
    {
        // Return sample community templates when GitHub is unavailable
        return new List<PromptTemplate>
        {
            new()
            {
                Id = "community-api-docs",
                Title = "API Documentation Generator",
                Category = "Coding",
                Tags = new List<string> { "api", "documentation", "openapi" },
                Description = "Generate comprehensive API documentation from code",
                Content = @"Generate API documentation for the following endpoint:

**Endpoint:** [METHOD] [PATH]
**Description:** [BRIEF DESCRIPTION]

**Request:**
```
[REQUEST BODY/PARAMS]
```

**Response:**
```
[RESPONSE BODY]
```

Please generate:
1. OpenAPI/Swagger specification
2. Human-readable documentation
3. Example requests (cURL, JavaScript, Python)
4. Error response documentation",
                Author = "Community",
                Version = "1.0",
                IsCommunity = true,
                IsOfficial = false,
                DownloadCount = 0,
                SubmittedBy = "PromptBox",
                SubmittedDate = DateTime.Now.AddDays(-30),
                LicenseType = "MIT"
            },
            new()
            {
                Id = "community-sql-optimizer",
                Title = "SQL Query Optimizer",
                Category = "Data Science",
                Tags = new List<string> { "sql", "database", "optimization" },
                Description = "Optimize SQL queries for better performance",
                Content = @"Analyze and optimize this SQL query:

```sql
[PASTE YOUR SQL QUERY]
```

**Database:** [MySQL/PostgreSQL/SQL Server/etc.]
**Table sizes:** [APPROXIMATE ROW COUNTS]
**Current execution time:** [IF KNOWN]

Please provide:
1. Analysis of current query issues
2. Optimized query version
3. Recommended indexes
4. Explanation of improvements
5. Estimated performance gain",
                Author = "Community",
                Version = "1.0",
                IsCommunity = true,
                IsOfficial = false,
                DownloadCount = 0,
                SubmittedBy = "PromptBox",
                SubmittedDate = DateTime.Now.AddDays(-45),
                LicenseType = "MIT"
            },
            new()
            {
                Id = "community-ux-review",
                Title = "UX Design Review",
                Category = "Design",
                Tags = new List<string> { "ux", "design", "accessibility" },
                Description = "Get comprehensive UX feedback on your designs",
                Content = @"Review this UI/UX design and provide feedback:

**Design Description:** [DESCRIBE THE INTERFACE]
**Target Users:** [WHO WILL USE THIS]
**Platform:** [Web/Mobile/Desktop]
**Key User Goals:** [WHAT USERS WANT TO ACCOMPLISH]

Please evaluate:
1. **Usability**: Is it intuitive and easy to use?
2. **Accessibility**: WCAG compliance issues
3. **Visual Hierarchy**: Information organization
4. **User Flow**: Path to complete key tasks
5. **Consistency**: Design pattern adherence
6. **Recommendations**: Specific improvements with rationale",
                Author = "Community",
                Version = "1.0",
                IsCommunity = true,
                IsOfficial = false,
                DownloadCount = 0,
                SubmittedBy = "PromptBox",
                SubmittedDate = DateTime.Now.AddDays(-15),
                LicenseType = "MIT"
            },
            new()
            {
                Id = "community-cicd-pipeline",
                Title = "CI/CD Pipeline Generator",
                Category = "DevOps",
                Tags = new List<string> { "cicd", "devops", "automation" },
                Description = "Generate CI/CD pipeline configurations",
                Content = @"Create a CI/CD pipeline configuration for:

**Project Type:** [Web App/API/Library/etc.]
**Language/Framework:** [SPECIFY]
**Platform:** [GitHub Actions/GitLab CI/Jenkins/etc.]
**Deployment Target:** [AWS/Azure/GCP/Kubernetes/etc.]

**Requirements:**
- [ ] Build and test
- [ ] Code quality checks (linting, formatting)
- [ ] Security scanning
- [ ] Docker image build
- [ ] Deployment to staging
- [ ] Deployment to production
- [ ] Notifications

Please generate:
1. Complete pipeline configuration file
2. Required secrets/variables list
3. Setup instructions
4. Best practices notes",
                Author = "Community",
                Version = "1.0",
                IsCommunity = true,
                IsOfficial = false,
                DownloadCount = 0,
                SubmittedBy = "PromptBox",
                SubmittedDate = DateTime.Now.AddDays(-20),
                LicenseType = "MIT"
            },
            new()
            {
                Id = "community-meal-planner",
                Title = "Weekly Meal Planner",
                Category = "Health & Wellness",
                Tags = new List<string> { "meal", "nutrition", "planning" },
                Description = "Create personalized weekly meal plans",
                Content = @"Create a weekly meal plan based on:

**Dietary Preferences:** [Vegetarian/Vegan/Keto/etc.]
**Allergies/Restrictions:** [LIST ANY]
**Calorie Target:** [DAILY CALORIES]
**Cooking Skill Level:** [Beginner/Intermediate/Advanced]
**Time Available:** [MINUTES PER MEAL]
**Budget:** [LOW/MEDIUM/HIGH]
**Number of People:** [COUNT]

Please provide:
1. 7-day meal plan (breakfast, lunch, dinner, snacks)
2. Grocery shopping list
3. Meal prep suggestions
4. Nutritional summary per day
5. Quick recipe instructions

**Disclaimer:** This is for informational purposes only. Consult a healthcare professional for personalized dietary advice.",
                Author = "Community",
                Version = "1.0",
                IsCommunity = true,
                IsOfficial = false,
                DownloadCount = 0,
                SubmittedBy = "PromptBox",
                SubmittedDate = DateTime.Now.AddDays(-60),
                LicenseType = "MIT"
            }
        };
    }

    #endregion

    #region Helper Classes

    private class GitHubContentItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; } = string.Empty;
    }

    private class DownloadResponse
    {
        public bool Success { get; set; }
        public int DownloadCount { get; set; }
    }

    private class DownloadCountItem
    {
        public string TemplateId { get; set; } = string.Empty;
        public int DownloadCount { get; set; }
    }

    #endregion
}
