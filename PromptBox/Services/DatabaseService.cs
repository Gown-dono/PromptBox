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

    #region Batch Processing Methods

    public async Task<int> SaveBatchJobAsync(BatchJob job)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<BatchJob>("batchJobs");
            
            if (job.Id == 0)
            {
                job.CreatedDate = DateTime.Now;
                var result = collection.Insert(job);
                return result.AsInt32;
            }
            else
            {
                collection.Update(job);
                return job.Id;
            }
        });
    }

    public async Task<BatchJob?> GetBatchJobByIdAsync(int id)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<BatchJob>("batchJobs");
            return collection.FindById(id);
        });
    }

    public async Task<List<BatchJob>> GetAllBatchJobsAsync()
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<BatchJob>("batchJobs");
            return collection.FindAll().OrderByDescending(j => j.CreatedDate).ToList();
        });
    }

    public async Task<bool> DeleteBatchJobAsync(int id)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            
            // Delete all results for this job first (cascading delete)
            var resultsCollection = db.GetCollection<BatchResult>("batchResults");
            resultsCollection.DeleteMany(r => r.BatchJobId == id);
            
            // Delete the job
            var jobsCollection = db.GetCollection<BatchJob>("batchJobs");
            return jobsCollection.Delete(id);
        });
    }

    public async Task<int> SaveBatchResultAsync(BatchResult result)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<BatchResult>("batchResults");
            
            // Ensure index on BatchJobId for efficient queries
            collection.EnsureIndex(r => r.BatchJobId);
            collection.EnsureIndex(r => r.PromptId);
            
            if (result.Id == 0)
            {
                result.ExecutedAt = DateTime.Now;
                var insertResult = collection.Insert(result);
                return insertResult.AsInt32;
            }
            else
            {
                collection.Update(result);
                return result.Id;
            }
        });
    }

    public async Task<List<BatchResult>> GetBatchResultsByJobIdAsync(int jobId)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<BatchResult>("batchResults");
            return collection.Find(r => r.BatchJobId == jobId).OrderBy(r => r.ExecutedAt).ToList();
        });
    }

    public async Task<List<BatchResult>> GetBatchResultsByPromptIdAsync(int promptId)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<BatchResult>("batchResults");
            return collection.Find(r => r.PromptId == promptId).OrderByDescending(r => r.ExecutedAt).ToList();
        });
    }

    public async Task<bool> DeleteBatchResultsByJobIdAsync(int jobId)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<BatchResult>("batchResults");
            return collection.DeleteMany(r => r.BatchJobId == jobId) > 0;
        });
    }

    public async Task SaveBatchResultAndUpdateJobAsync(BatchResult result, BatchJob job)
    {
        await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            db.BeginTrans();
            try
            {
                var resultsCollection = db.GetCollection<BatchResult>("batchResults");
                resultsCollection.EnsureIndex(r => r.BatchJobId);
                resultsCollection.EnsureIndex(r => r.PromptId);

                if (result.Id == 0)
                {
                    result.ExecutedAt = DateTime.Now;
                    resultsCollection.Insert(result);
                }
                else
                {
                    resultsCollection.Update(result);
                }

                var jobsCollection = db.GetCollection<BatchJob>("batchJobs");
                jobsCollection.Update(job);

                db.Commit();
            }
            catch
            {
                db.Rollback();
                throw;
            }
        });
    }

    #endregion

    #region Prompt Testing Methods

    public async Task<int> SavePromptTestAsync(PromptTest test)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<PromptTest>("promptTests");
            collection.EnsureIndex(t => t.PromptId);

            if (test.Id == 0)
            {
                test.CreatedDate = DateTime.Now;
                test.UpdatedDate = DateTime.Now;
                var result = collection.Insert(test);
                return result.AsInt32;
            }
            else
            {
                test.UpdatedDate = DateTime.Now;
                collection.Update(test);
                return test.Id;
            }
        });
    }

    public async Task<PromptTest?> GetPromptTestByIdAsync(int id)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<PromptTest>("promptTests");
            return collection.FindById(id);
        });
    }

    public async Task<List<PromptTest>> GetAllPromptTestsAsync()
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<PromptTest>("promptTests");
            return collection.FindAll().OrderByDescending(t => t.UpdatedDate).ToList();
        });
    }

    public async Task<List<PromptTest>> GetPromptTestsByPromptIdAsync(int promptId)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<PromptTest>("promptTests");
            return collection.Find(t => t.PromptId == promptId).OrderByDescending(t => t.UpdatedDate).ToList();
        });
    }

    public async Task<bool> DeletePromptTestAsync(int id)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);

            // Cascade delete test results
            var resultsCollection = db.GetCollection<TestResult>("testResults");
            resultsCollection.DeleteMany(r => r.TestId == id);

            var collection = db.GetCollection<PromptTest>("promptTests");
            return collection.Delete(id);
        });
    }

    public async Task<int> SaveTestResultAsync(TestResult result)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<TestResult>("testResults");
            collection.EnsureIndex(r => r.TestId);

            if (result.Id == 0)
            {
                result.ExecutedAt = DateTime.Now;
                var insertResult = collection.Insert(result);
                return insertResult.AsInt32;
            }
            else
            {
                collection.Update(result);
                return result.Id;
            }
        });
    }

    public async Task<List<TestResult>> GetTestResultsByTestIdAsync(int testId)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<TestResult>("testResults");
            return collection.Find(r => r.TestId == testId).OrderByDescending(r => r.ExecutedAt).ToList();
        });
    }

    public async Task<bool> DeleteTestResultsByTestIdAsync(int testId)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<TestResult>("testResults");
            return collection.DeleteMany(r => r.TestId == testId) > 0;
        });
    }

    public async Task<int> SaveTestComparisonAsync(TestComparison comparison)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<TestComparison>("testComparisons");

            if (comparison.Id == 0)
            {
                comparison.CreatedDate = DateTime.Now;
                var result = collection.Insert(comparison);
                return result.AsInt32;
            }
            else
            {
                collection.Update(comparison);
                return comparison.Id;
            }
        });
    }

    public async Task<List<TestComparison>> GetAllTestComparisonsAsync()
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<TestComparison>("testComparisons");
            return collection.FindAll().OrderByDescending(c => c.CreatedDate).ToList();
        });
    }

    public async Task<bool> DeleteTestComparisonAsync(int id)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<TestComparison>("testComparisons");
            return collection.Delete(id);
        });
    }

    #endregion

    #region Prompt Comparison Session Methods

    public async Task<int> SavePromptComparisonSessionAsync(PromptComparisonSession session)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<PromptComparisonSession>("promptComparisonSessions");
            collection.EnsureIndex(s => s.CreatedDate);

            if (session.Id == 0)
            {
                session.CreatedDate = DateTime.Now;
                var result = collection.Insert(session);
                return result.AsInt32;
            }
            else
            {
                collection.Update(session);
                return session.Id;
            }
        });
    }

    public async Task<PromptComparisonSession?> GetPromptComparisonSessionByIdAsync(int id)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<PromptComparisonSession>("promptComparisonSessions");
            return collection.FindById(id);
        });
    }

    public async Task<List<PromptComparisonSession>> GetAllPromptComparisonSessionsAsync()
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<PromptComparisonSession>("promptComparisonSessions");
            return collection.FindAll().OrderByDescending(s => s.CreatedDate).ToList();
        });
    }

    public async Task<bool> DeletePromptComparisonSessionAsync(int id)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<PromptComparisonSession>("promptComparisonSessions");
            return collection.Delete(id);
        });
    }

    #endregion
}
