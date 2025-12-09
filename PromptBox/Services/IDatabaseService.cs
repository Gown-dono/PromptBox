using PromptBox.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PromptBox.Services;

public interface IDatabaseService
{
    Task<List<Prompt>> GetAllPromptsAsync();
    Task<Prompt?> GetPromptByIdAsync(int id);
    Task<int> SavePromptAsync(Prompt prompt);
    Task<bool> DeletePromptAsync(int id);
    Task<List<string>> GetAllCategoriesAsync();
    Task<List<string>> GetAllTagsAsync();
    
    // Batch processing methods
    Task<int> SaveBatchJobAsync(BatchJob job);
    Task<BatchJob?> GetBatchJobByIdAsync(int id);
    Task<List<BatchJob>> GetAllBatchJobsAsync();
    Task<bool> DeleteBatchJobAsync(int id);
    Task<int> SaveBatchResultAsync(BatchResult result);
    Task<List<BatchResult>> GetBatchResultsByJobIdAsync(int jobId);
    Task<List<BatchResult>> GetBatchResultsByPromptIdAsync(int promptId);
    Task<bool> DeleteBatchResultsByJobIdAsync(int jobId);
    Task SaveBatchResultAndUpdateJobAsync(BatchResult result, BatchJob job);

    // Prompt testing methods
    Task<int> SavePromptTestAsync(PromptTest test);
    Task<PromptTest?> GetPromptTestByIdAsync(int id);
    Task<List<PromptTest>> GetAllPromptTestsAsync();
    Task<List<PromptTest>> GetPromptTestsByPromptIdAsync(int promptId);
    Task<bool> DeletePromptTestAsync(int id);
    Task<int> SaveTestResultAsync(TestResult result);
    Task<List<TestResult>> GetTestResultsByTestIdAsync(int testId);
    Task<bool> DeleteTestResultsByTestIdAsync(int testId);
    Task<int> SaveTestComparisonAsync(TestComparison comparison);
    Task<List<TestComparison>> GetAllTestComparisonsAsync();
    Task<bool> DeleteTestComparisonAsync(int id);

    // Prompt comparison session methods
    Task<int> SavePromptComparisonSessionAsync(PromptComparisonSession session);
    Task<PromptComparisonSession?> GetPromptComparisonSessionByIdAsync(int id);
    Task<List<PromptComparisonSession>> GetAllPromptComparisonSessionsAsync();
    Task<bool> DeletePromptComparisonSessionAsync(int id);
}
