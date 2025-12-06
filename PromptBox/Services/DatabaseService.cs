using LiteDB;
using PromptBox.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PromptBox.Services;

/// <summary>
/// Service for managing local database operations using LiteDB
/// </summary>
public class DatabaseService : IDatabaseService
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public DatabaseService()
    {
        var dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        Directory.CreateDirectory(dataFolder);
        _dbPath = Path.Combine(dataFolder, "promptbox.db");
        _connectionString = $"Filename={_dbPath};Connection=shared";
    }

    public async Task<List<Prompt>> GetAllPromptsAsync()
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<Prompt>("prompts");
            return collection.FindAll().ToList();
        });
    }

    public async Task<Prompt?> GetPromptByIdAsync(int id)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<Prompt>("prompts");
            return collection.FindById(id);
        });
    }

    public async Task<int> SavePromptAsync(Prompt prompt)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<Prompt>("prompts");
            
            if (prompt.Id == 0)
            {
                prompt.CreatedDate = DateTime.Now;
                prompt.UpdatedDate = DateTime.Now;
                var result = collection.Insert(prompt);
                return result.AsInt32;
            }
            else
            {
                prompt.UpdatedDate = DateTime.Now;
                collection.Update(prompt);
                return prompt.Id;
            }
        });
    }

    public async Task<bool> DeletePromptAsync(int id)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<Prompt>("prompts");
            return collection.Delete(id);
        });
    }

    public async Task<List<string>> GetAllCategoriesAsync()
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<Prompt>("prompts");
            return collection.FindAll()
                .Select(p => p.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        });
    }

    public async Task<List<string>> GetAllTagsAsync()
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<Prompt>("prompts");
            return collection.FindAll()
                .SelectMany(p => p.Tags)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .OrderBy(t => t)
                .ToList();
        });
    }
}
