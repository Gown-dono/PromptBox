namespace PromptBox.Services;

/// <summary>
/// Interface for secure storage of sensitive data like API keys
/// </summary>
public interface ISecureStorageService
{
    /// <summary>
    /// Encrypts and stores an API key for a provider
    /// </summary>
    System.Threading.Tasks.Task SaveApiKeyAsync(string provider, string apiKey);
    
    /// <summary>
    /// Retrieves and decrypts an API key for a provider
    /// </summary>
    System.Threading.Tasks.Task<string?> GetApiKeyAsync(string provider);
    
    /// <summary>
    /// Deletes an API key for a provider
    /// </summary>
    System.Threading.Tasks.Task<bool> DeleteApiKeyAsync(string provider);
    
    /// <summary>
    /// Checks if an API key exists for a provider
    /// </summary>
    System.Threading.Tasks.Task<bool> HasApiKeyAsync(string provider);
    
    /// <summary>
    /// Gets all providers that have stored API keys
    /// </summary>
    System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GetStoredProvidersAsync();
    
    /// <summary>
    /// Updates the last used date for a provider's API key
    /// </summary>
    System.Threading.Tasks.Task UpdateLastUsedAsync(string provider);
    
    /// <summary>
    /// Removes API key records that cannot be decrypted (corrupted or from different user context)
    /// </summary>
    System.Threading.Tasks.Task<int> CleanupInvalidKeysAsync();
}
