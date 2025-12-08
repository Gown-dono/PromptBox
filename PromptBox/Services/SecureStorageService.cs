using LiteDB;
using PromptBox.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PromptBox.Services;

/// <summary>
/// Secure storage service using Windows DPAPI for encryption
/// </summary>
public class SecureStorageService : ISecureStorageService
{
    private readonly string _connectionString;
    private readonly byte[] _additionalEntropy;

    public SecureStorageService()
    {
        var dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        Directory.CreateDirectory(dataFolder);
        var dbPath = Path.Combine(dataFolder, "promptbox.db");
        _connectionString = $"Filename={dbPath};Connection=shared";
        
        // Additional entropy for DPAPI - machine-specific
        _additionalEntropy = Encoding.UTF8.GetBytes("PromptBox_SecureKey_v1");
    }

    public async Task SaveApiKeyAsync(string provider, string apiKey)
    {
        await Task.Run(() =>
        {
            var encryptedKey = Encrypt(apiKey);
            
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<APIKeyConfig>("apikeys");
            
            // Remove existing key for this provider using Query.EQ for reliable matching
            collection.DeleteMany(Query.EQ("Provider", provider));
            
            // Insert new key
            var config = new APIKeyConfig
            {
                Provider = provider,
                EncryptedKey = encryptedKey,
                CreatedDate = DateTime.Now,
                IsValid = true
            };
            
            collection.Insert(config);
        });
    }

    public async Task<string?> GetApiKeyAsync(string provider)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var db = new LiteDatabase(_connectionString);
                var collection = db.GetCollection<APIKeyConfig>("apikeys");
                
                // Use Query.EQ for more reliable querying
                var config = collection.FindOne(Query.EQ("Provider", provider));
                
                if (config == null || string.IsNullOrEmpty(config.EncryptedKey))
                    return null;
                
                return Decrypt(config.EncryptedKey);
            }
            catch
            {
                return null;
            }
        });
    }

    public async Task<bool> DeleteApiKeyAsync(string provider)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<APIKeyConfig>("apikeys");
            return collection.DeleteMany(Query.EQ("Provider", provider)) > 0;
        });
    }

    public async Task<bool> HasApiKeyAsync(string provider)
    {
        // Verify the key can be retrieved and decrypted
        return await Task.Run(() =>
        {
            try
            {
                using var db = new LiteDatabase(_connectionString);
                var collection = db.GetCollection<APIKeyConfig>("apikeys");
                var config = collection.FindOne(Query.EQ("Provider", provider));
                
                if (config == null || string.IsNullOrEmpty(config.EncryptedKey))
                    return false;
                
                // Verify the key can be decrypted
                var decryptedKey = Decrypt(config.EncryptedKey);
                return !string.IsNullOrEmpty(decryptedKey);
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<List<string>> GetStoredProvidersAsync()
    {
        // Get all providers from the database and verify each key can be decrypted
        return await Task.Run(() =>
        {
            var validProviders = new List<string>();
            
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<APIKeyConfig>("apikeys");
            var allConfigs = collection.FindAll().ToList();
            
            foreach (var config in allConfigs)
            {
                if (string.IsNullOrEmpty(config.Provider) || string.IsNullOrEmpty(config.EncryptedKey))
                    continue;
                    
                try
                {
                    // Verify the key can be decrypted
                    var decryptedKey = Decrypt(config.EncryptedKey);
                    if (!string.IsNullOrEmpty(decryptedKey))
                    {
                        validProviders.Add(config.Provider);
                    }
                }
                catch
                {
                    // Key can't be decrypted, skip it
                }
            }
            
            return validProviders.Distinct().ToList();
        });
    }

    public async Task UpdateLastUsedAsync(string provider)
    {
        await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<APIKeyConfig>("apikeys");
            var config = collection.FindOne(Query.EQ("Provider", provider));
            
            if (config != null)
            {
                config.LastUsedDate = DateTime.Now;
                collection.Update(config);
            }
        });
    }

    /// <summary>
    /// Removes API key records that cannot be decrypted due to corrupted or cross-user-context DPAPI blobs.
    /// This cleanup is specifically for decryption failures, not for missing or empty keys.
    /// </summary>
    public async Task<int> CleanupInvalidKeysAsync()
    {
        return await Task.Run(() =>
        {
            int removedCount = 0;
            
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<APIKeyConfig>("apikeys");
            var allConfigs = collection.FindAll().ToList();
            
            foreach (var config in allConfigs)
            {
                // Skip records with no encrypted key - these are just absent, not corrupted
                if (string.IsNullOrEmpty(config.EncryptedKey))
                    continue;
                
                try
                {
                    // Attempt to decrypt the key directly
                    Decrypt(config.EncryptedKey);
                    // Decryption succeeded, key is valid - do nothing
                }
                catch (CryptographicException)
                {
                    // Decryption failed due to DPAPI issue (corrupted blob or different user context)
                    // This is the only case where we should delete the key
                    collection.Delete(config.Id);
                    removedCount++;
                }
                catch (FormatException)
                {
                    // Base64 decoding failed - corrupted data
                    collection.Delete(config.Id);
                    removedCount++;
                }
                // Other exceptions (e.g., temporary failures) are not treated as invalid keys
            }
            
            return removedCount;
        });
    }

    private string Encrypt(string plainText)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = ProtectedData.Protect(plainBytes, _additionalEntropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encryptedBytes);
    }

    private string Decrypt(string encryptedText)
    {
        var encryptedBytes = Convert.FromBase64String(encryptedText);
        var plainBytes = ProtectedData.Unprotect(encryptedBytes, _additionalEntropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
