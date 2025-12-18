using PromptBox.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PromptBox.Services;

/// <summary>
/// Interface for community prompt template operations
/// </summary>
public interface IPromptCommunityService
{
    /// <summary>
    /// Fetch community templates from GitHub/API with caching
    /// </summary>
    Task<List<PromptTemplate>> FetchCommunityTemplatesAsync(bool forceRefresh = false);
    
    /// <summary>
    /// Get a single community template by ID
    /// </summary>
    Task<PromptTemplate?> GetCommunityTemplateByIdAsync(string id);
    
    /// <summary>
    /// Submit a template to the community via GitHub PR or API
    /// </summary>
    Task<(bool Success, string Message)> SubmitTemplateAsync(PromptTemplate template, string submitterInfo);
    
    /// <summary>
    /// Report inappropriate content
    /// </summary>
    Task<bool> ReportTemplateAsync(string templateId, string reason);
    
    /// <summary>
    /// Check if new templates are available
    /// </summary>
    Task<bool> CheckForUpdatesAsync();
    
    /// <summary>
    /// Get total count of community templates
    /// </summary>
    Task<int> GetCommunityTemplateCountAsync();
    
    /// <summary>
    /// Record a download for a community template
    /// </summary>
    Task RecordDownloadAsync(string templateId);
    
    /// <summary>
    /// Get pending submissions for moderation (admin only)
    /// </summary>
    Task<List<PromptTemplate>> GetPendingSubmissionsAsync();
    
    /// <summary>
    /// Approve a pending submission (admin only)
    /// </summary>
    Task<bool> ApproveSubmissionAsync(string templateId);
    
    /// <summary>
    /// Reject a pending submission (admin only)
    /// </summary>
    Task<bool> RejectSubmissionAsync(string templateId);
}
