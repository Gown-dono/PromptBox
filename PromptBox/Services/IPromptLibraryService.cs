using PromptBox.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PromptBox.Services;

/// <summary>
/// Interface for the prompt library service
/// </summary>
public interface IPromptLibraryService
{
    // Synchronous methods (for backward compatibility - local templates only)
    
    /// <summary>
    /// Gets all local built-in templates only. Does not include community templates.
    /// </summary>
    /// <remarks>
    /// This synchronous method operates on local built-in templates only.
    /// To include community templates, use <see cref="GetAllTemplatesAsync"/> instead.
    /// </remarks>
    /// <returns>A list of local built-in prompt templates.</returns>
    List<PromptTemplate> GetAllTemplates();
    
    /// <summary>
    /// Gets local built-in templates filtered by category. Does not include community templates.
    /// </summary>
    /// <remarks>
    /// This synchronous method operates on local built-in templates only.
    /// To include community templates, use <see cref="SearchTemplatesAsync"/> with appropriate filters.
    /// </remarks>
    /// <param name="category">The category to filter by.</param>
    /// <returns>A list of local built-in prompt templates in the specified category.</returns>
    List<PromptTemplate> GetTemplatesByCategory(string category);
    
    /// <summary>
    /// Gets all categories from local built-in templates only. Does not include community template categories.
    /// </summary>
    /// <remarks>
    /// This synchronous method operates on local built-in templates only.
    /// Community templates may have additional categories not returned by this method.
    /// </remarks>
    /// <returns>A list of category names from local built-in templates.</returns>
    List<string> GetCategories();
    
    /// <summary>
    /// Gets a local built-in template by ID. Does not search community templates.
    /// </summary>
    /// <remarks>
    /// This synchronous method operates on local built-in templates only.
    /// To find community templates by ID, use <see cref="GetAllTemplatesAsync"/> and filter the results.
    /// </remarks>
    /// <param name="id">The template ID to search for.</param>
    /// <returns>The matching template, or null if not found in local templates.</returns>
    PromptTemplate? GetTemplateById(string id);
    
    /// <summary>
    /// Searches local built-in templates only. Does not include community templates in search.
    /// </summary>
    /// <remarks>
    /// This synchronous method operates on local built-in templates only.
    /// To search including community templates, use <see cref="SearchTemplatesAsync"/> instead.
    /// </remarks>
    /// <param name="query">The search query to match against title, description, category, and tags.</param>
    /// <returns>A list of matching local built-in prompt templates.</returns>
    List<PromptTemplate> SearchTemplates(string query);
    
    // Async methods with community support
    
    /// <summary>
    /// Gets all templates asynchronously, optionally including community templates.
    /// </summary>
    /// <param name="includeCommunity">Whether to include community templates. Defaults to true.</param>
    /// <returns>A list of prompt templates, sorted by rating and download count.</returns>
    Task<List<PromptTemplate>> GetAllTemplatesAsync(bool includeCommunity = true);
    
    /// <summary>
    /// Gets only community templates asynchronously.
    /// </summary>
    /// <returns>A list of community prompt templates.</returns>
    Task<List<PromptTemplate>> GetCommunityTemplatesAsync();
    
    /// <summary>
    /// Searches templates asynchronously with source filtering.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="source">Source filter: "All", "Local", or "Community".</param>
    /// <returns>A list of matching prompt templates.</returns>
    Task<List<PromptTemplate>> SearchTemplatesAsync(string query, string source = "All");
    
    /// <summary>
    /// Forces a refresh of community templates from the remote source.
    /// </summary>
    /// <returns>True if refresh was successful, false otherwise.</returns>
    Task<bool> RefreshCommunityTemplatesAsync();
    
    /// <summary>
    /// Record a download for a local/built-in template
    /// </summary>
    Task RecordLocalDownloadAsync(string templateId);
}
