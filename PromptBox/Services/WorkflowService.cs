using LiteDB;
using PromptBox.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace PromptBox.Services;

/// <summary>
/// Service for managing and executing multi-step prompt workflows
/// </summary>
public class WorkflowService : IWorkflowService
{
    private readonly IAIService _aiService;
    private readonly string _connectionString;
    
    public event EventHandler<WorkflowStepEventArgs>? StepStarted;
    public event EventHandler<WorkflowStepEventArgs>? StepCompleted;

    public WorkflowService(IAIService aiService)
    {
        _aiService = aiService;
        var dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        Directory.CreateDirectory(dataFolder);
        _connectionString = $"Filename={Path.Combine(dataFolder, "promptbox.db")};Connection=shared";
    }

    public List<Workflow> GetBuiltInWorkflows()
    {
        return new List<Workflow>
        {
            CreateCodeDevelopmentWorkflow(),
            CreateCodeReviewWorkflow(),
            CreateDocumentationWorkflow(),
            CreateBugFixWorkflow(),
            CreateFeatureDesignWorkflow(),
            CreateCodeMigrationWorkflow(),
            CreateContentCreationWorkflow(),
            CreateDataAnalysisWorkflow(),
            CreateLearningPathWorkflow(),
            CreateProjectPlanningWorkflow()
        };
    }

    public async Task<List<Workflow>> GetAllWorkflowsAsync()
    {
        var builtIn = GetBuiltInWorkflows();
        var custom = await GetCustomWorkflowsAsync();
        return builtIn.Concat(custom).ToList();
    }

    public async Task<Workflow?> GetWorkflowByIdAsync(int id)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<Workflow>("workflows");
            return collection.FindById(id);
        });
    }

    public async Task<int> SaveWorkflowAsync(Workflow workflow)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<Workflow>("workflows");
            
            if (workflow.Id == 0)
            {
                workflow.CreatedDate = DateTime.Now;
                workflow.UpdatedDate = DateTime.Now;
                workflow.IsBuiltIn = false;
                var result = collection.Insert(workflow);
                return result.AsInt32;
            }
            else
            {
                workflow.UpdatedDate = DateTime.Now;
                collection.Update(workflow);
                return workflow.Id;
            }
        });
    }

    public async Task<bool> DeleteWorkflowAsync(int id)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<Workflow>("workflows");
            return collection.Delete(id);
        });
    }

    public async IAsyncEnumerable<WorkflowStepResult> ExecuteWorkflowAsync(
        Workflow workflow,
        string initialInput,
        AIGenerationSettings settings,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Migrate workflow if needed
        MigrateWorkflow(workflow);
        
        // Initialize execution context
        var context = new WorkflowExecutionContext
        {
            InitialInput = initialInput,
            PreviousOutput = initialInput,
            CancellationToken = cancellationToken,
            Variables = new Dictionary<string, string>
            {
                { "input", initialInput },
                { "initial_input", initialInput }
            }
        };
        
        // Find start step
        var startStep = workflow.GetStartStep();
        if (startStep == null)
        {
            yield return new WorkflowStepResult
            {
                Success = false,
                Error = "No start step found in workflow"
            };
            yield break;
        }
        
        // Execute using graph traversal
        await foreach (var result in ExecuteStepAsync(workflow, startStep.StepId, context, settings))
        {
            yield return result;
            
            if (!result.Success && !(workflow.GetStepById(result.StepId)?.ErrorHandling?.ContinueOnError ?? false))
            {
                yield break;
            }
        }
    }
    
    private async IAsyncEnumerable<WorkflowStepResult> ExecuteStepAsync(
        Workflow workflow,
        string stepId,
        WorkflowExecutionContext context,
        AIGenerationSettings settings)
    {
        if (context.CancellationToken.IsCancellationRequested)
            yield break;
        
        var step = workflow.GetStepById(stepId);
        if (step == null) yield break;
        
        // Check for infinite loop (except for loop steps which handle their own iteration)
        if (step.StepType != WorkflowStepType.Loop && context.VisitedSteps.Contains(stepId))
        {
            Debug.WriteLine($"Skipping already visited step: {step.Name}");
            yield break;
        }
        
        context.VisitedSteps.Add(stepId);
        context.CurrentStepId = stepId;
        
        var stepIndex = workflow.Steps.IndexOf(step);
        
        StepStarted?.Invoke(this, new WorkflowStepEventArgs
        {
            StepIndex = stepIndex,
            StepName = step.Name,
            TotalSteps = workflow.Steps.Count
        });
        
        // Execute based on step type
        WorkflowStepResult result;
        
        switch (step.StepType)
        {
            case WorkflowStepType.Loop:
                await foreach (var loopResult in ExecuteLoopStepAsync(workflow, step, context, settings))
                {
                    yield return loopResult;
                }
                yield break;
                
            case WorkflowStepType.Parallel:
                await foreach (var parallelResult in ExecuteParallelStepAsync(workflow, step, context, settings))
                {
                    yield return parallelResult;
                }
                yield break;
                
            case WorkflowStepType.Conditional:
                result = await ExecuteStepWithRetryAsync(workflow, step, context, settings);
                yield return result;
                
                if (result.Success)
                {
                    // Evaluate conditions and follow matching branch
                    var nextStepId = EvaluateConditionalBranches(step, result.Output, result);
                    if (!string.IsNullOrEmpty(nextStepId))
                    {
                        await foreach (var branchResult in ExecuteStepAsync(workflow, nextStepId, context, settings))
                        {
                            yield return branchResult;
                        }
                    }
                }
                else
                {
                    // Failure - check for fallback step first
                    var fallbackStepId = step.ErrorHandling?.FallbackStepId;
                    var continueOnError = step.ErrorHandling?.ContinueOnError ?? false;
                    
                    if (!string.IsNullOrEmpty(fallbackStepId))
                    {
                        Debug.WriteLine($"Conditional step '{step.Name}' failed, executing fallback step: {fallbackStepId}");
                        
                        await foreach (var fallbackResult in ExecuteStepAsync(workflow, fallbackStepId, context, settings))
                        {
                            yield return fallbackResult;
                            
                            if (!fallbackResult.Success && !continueOnError)
                            {
                                yield break;
                            }
                        }
                    }
                    // No fallback - if ContinueOnError is true, default to NextStepId
                    else if (continueOnError && !string.IsNullOrEmpty(step.NextStepId))
                    {
                        Debug.WriteLine($"Conditional step '{step.Name}' failed but ContinueOnError is true, proceeding to NextStepId");
                        await foreach (var nextResult in ExecuteStepAsync(workflow, step.NextStepId, context, settings))
                        {
                            yield return nextResult;
                        }
                    }
                }
                yield break;
                
            default:
                result = await ExecuteStepWithRetryAsync(workflow, step, context, settings);
                yield return result;
                
                if (result.Success)
                {
                    // Success - continue to next step
                    if (!string.IsNullOrEmpty(step.NextStepId))
                    {
                        await foreach (var nextResult in ExecuteStepAsync(workflow, step.NextStepId, context, settings))
                        {
                            yield return nextResult;
                        }
                    }
                }
                else
                {
                    // Failure - check for fallback step
                    var fallbackStepId = step.ErrorHandling?.FallbackStepId;
                    var continueOnError = step.ErrorHandling?.ContinueOnError ?? false;
                    
                    if (!string.IsNullOrEmpty(fallbackStepId))
                    {
                        Debug.WriteLine($"Step '{step.Name}' failed, executing fallback step: {fallbackStepId}");
                        
                        // Execute fallback step - it handles its own continuation via its NextStepId
                        await foreach (var fallbackResult in ExecuteStepAsync(workflow, fallbackStepId, context, settings))
                        {
                            yield return fallbackResult;
                            
                            // If fallback fails and ContinueOnError is false, stop
                            if (!fallbackResult.Success && !continueOnError)
                            {
                                yield break;
                            }
                        }
                    }
                    // No fallback - if ContinueOnError is true, continue to next step
                    else if (continueOnError && !string.IsNullOrEmpty(step.NextStepId))
                    {
                        Debug.WriteLine($"Step '{step.Name}' failed but ContinueOnError is true, proceeding to NextStepId");
                        await foreach (var nextResult in ExecuteStepAsync(workflow, step.NextStepId, context, settings))
                        {
                            yield return nextResult;
                        }
                    }
                }
                break;
        }
    }
    
    private async Task<WorkflowStepResult> ExecuteStepWithRetryAsync(
        Workflow workflow,
        WorkflowStep step,
        WorkflowExecutionContext context,
        AIGenerationSettings settings)
    {
        var stepIndex = workflow.Steps.IndexOf(step);
        var totalSteps = workflow.Steps.Count;
        var maxRetries = step.ErrorHandling?.MaxRetries ?? 0;
        var retryDelay = step.ErrorHandling?.RetryDelayMs ?? 1000;
        var useExponentialBackoff = step.ErrorHandling?.UseExponentialBackoff ?? true;
        
        WorkflowStepResult? result = null;
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            if (context.CancellationToken.IsCancellationRequested)
                break;
            
            var stopwatch = Stopwatch.StartNew();
            var prompt = BuildStepPrompt(step, context.Variables, context.PreviousOutput, context.InitialInput);
            
            result = new WorkflowStepResult
            {
                StepId = step.StepId,
                StepOrder = step.Order,
                StepName = step.Name,
                Input = prompt,
                RetryCount = attempt
            };
            
            try
            {
                var response = await _aiService.GenerateAsync(prompt, settings);
                stopwatch.Stop();
                
                result.Success = response.Success;
                result.Output = response.Content;
                result.Error = response.Error;
                result.Duration = stopwatch.Elapsed;
                result.TokensUsed = response.TokensUsed;
                result.ExecutionStatus = response.Success ? NodeExecutionStatus.Success : NodeExecutionStatus.Failed;
                
                if (response.Success)
                {
                    // Update context
                    context.PreviousOutput = response.Content;
                    context.StepResults[step.StepId] = result;
                    
                    if (!string.IsNullOrEmpty(step.OutputVariable))
                        context.Variables[step.OutputVariable] = response.Content;
                    
                    context.Variables[$"step{step.Order + 1}"] = response.Content;
                    
                    StepCompleted?.Invoke(this, new WorkflowStepEventArgs
                    {
                        StepIndex = stepIndex,
                        StepName = step.Name,
                        TotalSteps = totalSteps,
                        Result = result
                    });
                    
                    return result;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Error = ex.Message;
                result.Duration = stopwatch.Elapsed;
                result.ExecutionStatus = NodeExecutionStatus.Failed;
            }
            
            // Retry delay
            if (attempt < maxRetries)
            {
                var delay = useExponentialBackoff 
                    ? retryDelay * (int)Math.Pow(2, attempt) 
                    : retryDelay;
                await Task.Delay(delay, context.CancellationToken);
            }
        }
        
        // All retries failed - check for fallback
        if (result != null && !result.Success && !string.IsNullOrEmpty(step.ErrorHandling?.FallbackStepId))
        {
            result.Error += $" (Fallback to step: {step.ErrorHandling.FallbackStepId})";
        }
        
        StepCompleted?.Invoke(this, new WorkflowStepEventArgs
        {
            StepIndex = stepIndex,
            StepName = step.Name,
            TotalSteps = totalSteps,
            Result = result
        });
        
        return result ?? new WorkflowStepResult { Success = false, Error = "Unknown error" };
    }
    
    private async IAsyncEnumerable<WorkflowStepResult> ExecuteLoopStepAsync(
        Workflow workflow,
        WorkflowStep step,
        WorkflowExecutionContext context,
        AIGenerationSettings settings)
    {
        var loopConfig = step.LoopConfig ?? new LoopConfig();
        var maxIterations = loopConfig.MaxIterations;
        var loopVariable = loopConfig.LoopVariable;
        
        context.LoopCounters[step.StepId] = 0;
        
        // Track cumulative token usage across all iterations
        // TokensUsed in each iteration result reflects that iteration only
        // The last iteration's result will have cumulative tokens in a separate variable
        var cumulativeTokens = 0;
        
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            if (context.CancellationToken.IsCancellationRequested)
                yield break;
            
            context.LoopCounters[step.StepId] = iteration;
            context.Variables[loopVariable] = iteration.ToString();
            context.Variables[$"{step.StepId}_iteration"] = iteration.ToString();
            
            // Remove from visited to allow re-execution
            context.VisitedSteps.Remove(step.StepId);
            
            var result = await ExecuteStepWithRetryAsync(workflow, step, context, settings);
            result.StepName = $"{step.Name} (Iteration {iteration + 1})";
            
            // Track cumulative tokens - each result shows its own tokens
            // For TokenCount exit conditions, we use the current iteration's tokens
            cumulativeTokens += result.TokensUsed;
            
            // Store cumulative tokens in context for reference
            context.Variables[$"{step.StepId}_total_tokens"] = cumulativeTokens.ToString();
            
            yield return result;
            
            if (!result.Success)
                yield break;
            
            // Check exit condition (uses current iteration's result for TokenCount evaluation)
            if (loopConfig.ExitCondition.Evaluate(result.Output, result))
            {
                Debug.WriteLine($"Loop exit condition met at iteration {iteration + 1}");
                break;
            }
        }
        
        // Continue to next step after loop
        if (!string.IsNullOrEmpty(step.NextStepId))
        {
            await foreach (var nextResult in ExecuteStepAsync(workflow, step.NextStepId, context, settings))
            {
                yield return nextResult;
            }
        }
    }
    
    private async IAsyncEnumerable<WorkflowStepResult> ExecuteParallelStepAsync(
        Workflow workflow,
        WorkflowStep step,
        WorkflowExecutionContext context,
        AIGenerationSettings settings)
    {
        var parallelConfig = step.ParallelConfig ?? new ParallelConfig();
        var branchStepIds = parallelConfig.BranchStepIds;
        
        if (branchStepIds.Count == 0)
        {
            Debug.WriteLine($"Parallel step '{step.Name}' has no branch steps configured");
            yield return new WorkflowStepResult
            {
                StepId = step.StepId,
                StepName = step.Name,
                Success = false,
                Error = "No parallel branches configured",
                ExecutionStatus = NodeExecutionStatus.Failed
            };
            yield break;
        }
        
        var stepIndex = workflow.Steps.IndexOf(step);
        var totalSteps = workflow.Steps.Count;
        
        // Notify step started
        StepStarted?.Invoke(this, new WorkflowStepEventArgs
        {
            StepIndex = stepIndex,
            StepName = step.Name,
            TotalSteps = totalSteps
        });
        
        // Create tasks for each parallel branch
        var branchTasks = new List<Task<List<WorkflowStepResult>>>();
        var branchContexts = new List<WorkflowExecutionContext>();
        
        foreach (var branchStepId in branchStepIds)
        {
            // Create a copy of the context for each branch to avoid conflicts
            var branchContext = new WorkflowExecutionContext
            {
                InitialInput = context.InitialInput,
                PreviousOutput = context.PreviousOutput,
                CancellationToken = context.CancellationToken,
                Variables = new Dictionary<string, string>(context.Variables),
                StepResults = new Dictionary<string, WorkflowStepResult>(context.StepResults),
                VisitedSteps = new HashSet<string>(), // Fresh visited set for each branch
                LoopCounters = new Dictionary<string, int>(context.LoopCounters)
            };
            branchContexts.Add(branchContext);
            
            // Create task that collects all results from the branch
            var task = CollectBranchResultsAsync(workflow, branchStepId, branchContext, settings);
            branchTasks.Add(task);
        }
        
        // Wait for all branches to complete
        var allBranchResults = await Task.WhenAll(branchTasks);
        
        // Yield results from all branches and track success
        // Token aggregation: sum of all branch tokens (total cost of parallel execution)
        var allSucceeded = true;
        var combinedOutputs = new List<string>();
        var totalTokensUsed = 0;
        
        for (int i = 0; i < allBranchResults.Length; i++)
        {
            var branchResults = allBranchResults[i];
            var branchContext = branchContexts[i];
            var branchTokens = 0;
            
            foreach (var branchResult in branchResults)
            {
                branchResult.StepName = $"{branchResult.StepName} (Branch {i + 1})";
                yield return branchResult;
                
                if (!branchResult.Success)
                    allSucceeded = false;
                
                // Sum tokens from all steps in this branch
                branchTokens += branchResult.TokensUsed;
            }
            
            totalTokensUsed += branchTokens;
            
            // Store branch output and token count in context
            var lastOutput = branchResults.LastOrDefault()?.Output ?? string.Empty;
            combinedOutputs.Add(lastOutput);
            context.Variables[$"{parallelConfig.OutputVariablePrefix}_{i}"] = lastOutput;
            context.Variables[$"{parallelConfig.OutputVariablePrefix}_{i}_tokens"] = branchTokens.ToString();
            
            // Merge branch context variables back (last write wins for conflicts)
            foreach (var kvp in branchContext.Variables)
            {
                if (!context.Variables.ContainsKey(kvp.Key))
                    context.Variables[kvp.Key] = kvp.Value;
            }
        }
        
        // Create summary result for the parallel step itself
        // TokensUsed is the sum of all branch tokens (total cost of parallel execution)
        var parallelResult = new WorkflowStepResult
        {
            StepId = step.StepId,
            StepName = $"{step.Name} (Parallel Complete)",
            Success = allSucceeded || parallelConfig.ContinueOnBranchFailure,
            Output = string.Join("\n---\n", combinedOutputs),
            TokensUsed = totalTokensUsed,
            ExecutionStatus = allSucceeded ? NodeExecutionStatus.Success : NodeExecutionStatus.Failed
        };
        
        context.PreviousOutput = parallelResult.Output;
        context.StepResults[step.StepId] = parallelResult;
        
        if (!string.IsNullOrEmpty(step.OutputVariable))
            context.Variables[step.OutputVariable] = parallelResult.Output;
        
        yield return parallelResult;
        
        // Notify step completed
        StepCompleted?.Invoke(this, new WorkflowStepEventArgs
        {
            StepIndex = stepIndex,
            StepName = step.Name,
            TotalSteps = totalSteps,
            Result = parallelResult
        });
        
        // Continue to next step if successful (or ContinueOnBranchFailure is true)
        if (parallelResult.Success && !string.IsNullOrEmpty(step.NextStepId))
        {
            await foreach (var nextResult in ExecuteStepAsync(workflow, step.NextStepId, context, settings))
            {
                yield return nextResult;
            }
        }
    }
    
    private async Task<List<WorkflowStepResult>> CollectBranchResultsAsync(
        Workflow workflow,
        string startStepId,
        WorkflowExecutionContext context,
        AIGenerationSettings settings)
    {
        var results = new List<WorkflowStepResult>();
        
        // Execute only the single branch step, not the entire chain
        // The parallel step handles continuation after all branches complete
        var branchStep = workflow.GetStepById(startStepId);
        if (branchStep == null)
        {
            results.Add(new WorkflowStepResult
            {
                StepId = startStepId,
                StepName = "Unknown",
                Success = false,
                Error = $"Branch step '{startStepId}' not found"
            });
            return results;
        }
        
        // Execute just this branch step (not following NextStepId)
        var result = await ExecuteStepWithRetryAsync(workflow, branchStep, context, settings);
        results.Add(result);
        
        return results;
    }
    
    private string? EvaluateConditionalBranches(WorkflowStep step, string output, WorkflowStepResult result)
    {
        foreach (var branch in step.ConditionalBranches)
        {
            if (branch.Condition.Evaluate(output, result))
            {
                Debug.WriteLine($"Conditional branch '{branch.Label}' matched");
                return branch.NextStepId;
            }
        }
        
        // Default to NextStepId if no branch matches
        return step.NextStepId;
    }

    private string BuildStepPrompt(WorkflowStep step, Dictionary<string, string> variables, string previousOutput, string? initialInput = null)
    {
        var prompt = step.PromptTemplate;
        
        // Ensure input and initial_input are always available for substitution
        // This maintains backward compatibility for {{input}} and {{initial_input}} placeholders
        if (!variables.ContainsKey("input"))
        {
            var inputValue = initialInput ?? previousOutput;
            variables["input"] = inputValue;
        }
        if (!variables.ContainsKey("initial_input"))
        {
            var inputValue = initialInput ?? variables.GetValueOrDefault("input", previousOutput);
            variables["initial_input"] = inputValue;
        }
        
        // Replace variables
        foreach (var kvp in variables)
        {
            prompt = prompt.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
        }
        
        // Replace special placeholders
        prompt = prompt.Replace("{{previous_output}}", previousOutput);
        prompt = prompt.Replace("{{previous}}", previousOutput);
        
        return prompt;
    }
    
    #region Workflow Templates
    
    public List<WorkflowTemplate> GetWorkflowTemplates()
    {
        return new List<WorkflowTemplate>
        {
            CreateCodeReviewWithConditionalTemplate(),
            CreateIterativeRefinementTemplate(),
            CreateErrorResilientProcessingTemplate()
        };
    }
    
    public WorkflowTemplate? GetWorkflowTemplateById(int id)
    {
        return GetWorkflowTemplates().FirstOrDefault(t => t.Id == id);
    }
    
    public Workflow CreateWorkflowFromTemplate(int templateId)
    {
        var template = GetWorkflowTemplateById(templateId);
        if (template == null)
            return new Workflow { Name = "New Workflow" };
        
        // Create mapping from old step IDs to new step IDs
        var idMapping = new Dictionary<string, string>();
        foreach (var step in template.TemplateWorkflow.Steps)
        {
            idMapping[step.StepId] = Guid.NewGuid().ToString();
        }
        
        // Deep copy the template workflow
        var workflow = new Workflow
        {
            Name = $"{template.Name} (Copy)",
            Description = template.Description,
            Category = template.Category,
            IsBuiltIn = false,
            Steps = template.TemplateWorkflow.Steps.Select(s => new WorkflowStep
            {
                StepId = idMapping[s.StepId],
                Order = s.Order,
                Name = s.Name,
                Description = s.Description,
                PromptTemplate = s.PromptTemplate,
                UsesPreviousOutput = s.UsesPreviousOutput,
                OutputVariable = s.OutputVariable,
                StepType = s.StepType,
                Position = s.Position,
                IsStartStep = s.IsStartStep,
                IsEndStep = s.IsEndStep,
                // Map NextStepId to new ID
                NextStepId = !string.IsNullOrEmpty(s.NextStepId) && idMapping.ContainsKey(s.NextStepId) 
                    ? idMapping[s.NextStepId] 
                    : null,
                // Map conditional branches
                ConditionalBranches = s.ConditionalBranches.Select(b => new ConditionalBranch
                {
                    Label = b.Label,
                    NextStepId = !string.IsNullOrEmpty(b.NextStepId) && idMapping.ContainsKey(b.NextStepId)
                        ? idMapping[b.NextStepId]
                        : string.Empty,
                    Condition = new ConditionEvaluator
                    {
                        Type = b.Condition.Type,
                        ComparisonValue = b.Condition.ComparisonValue,
                        Operator = b.Condition.Operator
                    }
                }).ToList(),
                ErrorHandling = s.ErrorHandling != null ? new ErrorHandlingConfig
                {
                    MaxRetries = s.ErrorHandling.MaxRetries,
                    RetryDelayMs = s.ErrorHandling.RetryDelayMs,
                    UseExponentialBackoff = s.ErrorHandling.UseExponentialBackoff,
                    ContinueOnError = s.ErrorHandling.ContinueOnError,
                    // Map fallback step ID
                    FallbackStepId = !string.IsNullOrEmpty(s.ErrorHandling.FallbackStepId) && idMapping.ContainsKey(s.ErrorHandling.FallbackStepId)
                        ? idMapping[s.ErrorHandling.FallbackStepId]
                        : null
                } : null,
                LoopConfig = s.LoopConfig != null ? new LoopConfig
                {
                    MaxIterations = s.LoopConfig.MaxIterations,
                    LoopVariable = s.LoopConfig.LoopVariable,
                    ExitCondition = new ConditionEvaluator
                    {
                        Type = s.LoopConfig.ExitCondition.Type,
                        ComparisonValue = s.LoopConfig.ExitCondition.ComparisonValue,
                        Operator = s.LoopConfig.ExitCondition.Operator
                    }
                } : null
            }).ToList()
        };
        
        // If no NextStepId connections exist, create sequential connections based on Order
        var hasConnections = workflow.Steps.Any(s => !string.IsNullOrEmpty(s.NextStepId) || s.ConditionalBranches.Any());
        if (!hasConnections && workflow.Steps.Count > 1)
        {
            var orderedSteps = workflow.Steps.OrderBy(s => s.Order).ToList();
            for (int i = 0; i < orderedSteps.Count - 1; i++)
            {
                orderedSteps[i].NextStepId = orderedSteps[i + 1].StepId;
            }
        }
        
        // Run migration to ensure start step and other defaults are set
        MigrateWorkflow(workflow);
        
        return workflow;
    }
    
    public (bool IsValid, List<string> Errors, List<string> Warnings) ValidateWorkflow(Workflow workflow)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        
        if (workflow.Steps.Count == 0)
        {
            errors.Add("Workflow must have at least one step.");
            return (false, errors, warnings);
        }
        
        // Check for start step
        var startSteps = workflow.Steps.Where(s => s.IsStartStep).ToList();
        if (startSteps.Count == 0)
        {
            errors.Add("Workflow must have a start step. Mark one step as the start step.");
        }
        else if (startSteps.Count > 1)
        {
            errors.Add($"Workflow has multiple start steps: {string.Join(", ", startSteps.Select(s => s.Name))}. Only one step can be the start step.");
        }
        
        // Check for unreachable steps
        var reachable = new HashSet<string>();
        var startStep = workflow.GetStartStep();
        if (startStep != null)
        {
            var queue = new Queue<string>();
            queue.Enqueue(startStep.StepId);
            
            while (queue.Count > 0)
            {
                var stepId = queue.Dequeue();
                if (reachable.Contains(stepId)) continue;
                reachable.Add(stepId);
                
                var step = workflow.GetStepById(stepId);
                if (step == null) continue;
                
                if (!string.IsNullOrEmpty(step.NextStepId))
                    queue.Enqueue(step.NextStepId);
                
                foreach (var branch in step.ConditionalBranches)
                {
                    if (!string.IsNullOrEmpty(branch.NextStepId))
                        queue.Enqueue(branch.NextStepId);
                }
                
                // Include parallel branch step IDs in reachability
                if (step.StepType == WorkflowStepType.Parallel && step.ParallelConfig != null)
                {
                    foreach (var branchStepId in step.ParallelConfig.BranchStepIds)
                    {
                        if (!string.IsNullOrEmpty(branchStepId) && !reachable.Contains(branchStepId))
                            queue.Enqueue(branchStepId);
                    }
                }
                
                // Include fallback step edges in reachability
                if (!string.IsNullOrEmpty(step.ErrorHandling?.FallbackStepId))
                    queue.Enqueue(step.ErrorHandling.FallbackStepId);
            }
        }
        
        var unreachable = workflow.Steps.Where(s => !reachable.Contains(s.StepId)).ToList();
        if (unreachable.Any())
        {
            errors.Add($"Unreachable steps: {string.Join(", ", unreachable.Select(s => s.Name))}");
        }
        
        // Check for end steps that are marked but not reachable
        var unreachableEndSteps = workflow.Steps.Where(s => s.IsEndStep && !reachable.Contains(s.StepId)).ToList();
        if (unreachableEndSteps.Any())
        {
            errors.Add($"End steps are not reachable from start: {string.Join(", ", unreachableEndSteps.Select(s => s.Name))}");
        }
        
        // Check standard steps have outgoing paths (unless they are end steps)
        foreach (var step in workflow.Steps.Where(s => s.StepType == WorkflowStepType.Standard && !s.IsEndStep))
        {
            if (string.IsNullOrEmpty(step.NextStepId) && step.ConditionalBranches.Count == 0)
            {
                warnings.Add($"Standard step '{step.Name}' has no next step configured. The workflow will end after this step.");
            }
        }
        
        // Check conditional steps have branches and outgoing paths
        foreach (var step in workflow.Steps.Where(s => s.StepType == WorkflowStepType.Conditional))
        {
            var hasBranches = step.ConditionalBranches.Count > 0;
            var hasDefaultNext = !string.IsNullOrEmpty(step.NextStepId);
            var hasAnyBranchTarget = step.ConditionalBranches.Any(b => !string.IsNullOrEmpty(b.NextStepId));
            
            if (!hasBranches && !hasDefaultNext)
            {
                errors.Add($"Conditional step '{step.Name}' has no branches or default next step. Add at least one branch or set a default next step.");
            }
            else if (hasBranches && !hasAnyBranchTarget && !hasDefaultNext)
            {
                errors.Add($"Conditional step '{step.Name}' has branches but none have target steps configured, and no default next step is set. The workflow will have no path forward.");
            }
            else if (hasBranches && !hasDefaultNext)
            {
                warnings.Add($"Conditional step '{step.Name}' has no default next step. If no branch conditions match, the workflow will end at this step.");
            }
        }
        
        // Check loop steps have valid exit conditions
        foreach (var step in workflow.Steps.Where(s => s.StepType == WorkflowStepType.Loop))
        {
            if (step.LoopConfig == null)
            {
                errors.Add($"Loop step '{step.Name}' has no loop configuration.");
                continue;
            }
            
            // Validate MaxIterations
            if (step.LoopConfig.MaxIterations < 1)
            {
                errors.Add($"Loop step '{step.Name}' must have at least 1 iteration (current: {step.LoopConfig.MaxIterations}).");
            }
            
            // Validate ExitCondition exists
            if (step.LoopConfig.ExitCondition == null)
            {
                errors.Add($"Loop step '{step.Name}' has no exit condition configured.");
                continue;
            }
            
            var exitCondition = step.LoopConfig.ExitCondition;
            
            // Validate exit condition has meaningful configuration based on type
            switch (exitCondition.Type)
            {
                case ConditionType.OutputContains:
                case ConditionType.OutputMatches:
                    if (string.IsNullOrWhiteSpace(exitCondition.ComparisonValue))
                    {
                        errors.Add($"Loop step '{step.Name}' exit condition requires a comparison value for {exitCondition.Type}.");
                    }
                    break;
                    
                case ConditionType.Regex:
                    if (string.IsNullOrWhiteSpace(exitCondition.ComparisonValue))
                    {
                        errors.Add($"Loop step '{step.Name}' exit condition requires a regex pattern.");
                    }
                    else
                    {
                        // Validate regex pattern is valid
                        try
                        {
                            _ = new System.Text.RegularExpressions.Regex(exitCondition.ComparisonValue);
                        }
                        catch (System.ArgumentException)
                        {
                            errors.Add($"Loop step '{step.Name}' has an invalid regex pattern: '{exitCondition.ComparisonValue}'.");
                        }
                    }
                    break;
                    
                case ConditionType.OutputLength:
                case ConditionType.TokenCount:
                    if (string.IsNullOrWhiteSpace(exitCondition.ComparisonValue) || 
                        !int.TryParse(exitCondition.ComparisonValue, out _))
                    {
                        errors.Add($"Loop step '{step.Name}' exit condition requires a numeric comparison value for {exitCondition.Type}.");
                    }
                    break;
                    
                case ConditionType.Success:
                    // Success type doesn't require ComparisonValue, it checks result.Success
                    break;
            }
            
            // Warn about potentially long-running loops with weak exit conditions
            if (step.LoopConfig.MaxIterations > 20 && 
                exitCondition.Type == ConditionType.Success &&
                string.IsNullOrWhiteSpace(exitCondition.ComparisonValue))
            {
                warnings.Add($"Loop step '{step.Name}' has high max iterations ({step.LoopConfig.MaxIterations}) with a success-based exit condition. The loop will exit on the first successful iteration. Consider using OutputContains or Regex for more control.");
            }
        }
        
        // Check parallel steps have valid configuration
        foreach (var step in workflow.Steps.Where(s => s.StepType == WorkflowStepType.Parallel))
        {
            if (step.ParallelConfig == null)
            {
                errors.Add($"Parallel step '{step.Name}' has no parallel configuration.");
                continue;
            }
            
            if (step.ParallelConfig.BranchStepIds.Count == 0)
            {
                errors.Add($"Parallel step '{step.Name}' has no branch steps configured.");
            }
            else if (step.ParallelConfig.BranchStepIds.Count == 1)
            {
                warnings.Add($"Parallel step '{step.Name}' has only one branch. Consider using a standard step instead.");
            }
        }
        
        // Create set of valid step IDs for reference validation
        var stepIds = new HashSet<string>(workflow.Steps.Select(s => s.StepId));
        
        // Validate parallel branch step IDs exist and check for invalid configurations
        foreach (var step in workflow.Steps.Where(s => s.StepType == WorkflowStepType.Parallel && s.ParallelConfig != null))
        {
            var branchIds = step.ParallelConfig!.BranchStepIds;
            
            // Check for duplicate branch step IDs
            var duplicates = branchIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicates.Any())
            {
                errors.Add($"Parallel step '{step.Name}' has duplicate branch step IDs: {string.Join(", ", duplicates)}.");
            }
            
            foreach (var branchStepId in branchIds)
            {
                if (string.IsNullOrEmpty(branchStepId))
                {
                    errors.Add($"Parallel step '{step.Name}' has an empty branch step ID.");
                    continue;
                }
                
                if (!stepIds.Contains(branchStepId))
                {
                    errors.Add($"Parallel step '{step.Name}' has a branch step ID that does not exist: '{branchStepId}'.");
                }
                else if (branchStepId == step.StepId)
                {
                    errors.Add($"Parallel step '{step.Name}' cannot have itself as a branch step (self-reference).");
                }
                else
                {
                    // Check for circular reference: branch step pointing back to parallel step
                    var branchStep = workflow.GetStepById(branchStepId);
                    if (branchStep != null)
                    {
                        if (branchStep.NextStepId == step.StepId)
                        {
                            errors.Add($"Parallel step '{step.Name}' has a circular reference: branch step '{branchStep.Name}' points back to the parallel step.");
                        }
                        
                        // Check conditional branches for circular references
                        foreach (var branch in branchStep.ConditionalBranches)
                        {
                            if (branch.NextStepId == step.StepId)
                            {
                                errors.Add($"Parallel step '{step.Name}' has a circular reference: branch step '{branchStep.Name}' conditional branch '{branch.Label}' points back to the parallel step.");
                            }
                        }
                    }
                }
            }
            
            // Warn about ContinueOnBranchFailure semantics
            if (!step.ParallelConfig.ContinueOnBranchFailure && step.ParallelConfig.WaitForAll)
            {
                // This is the default strict mode - no warning needed
            }
            else if (step.ParallelConfig.ContinueOnBranchFailure)
            {
                // Inform about behavior when branches fail
                warnings.Add($"Parallel step '{step.Name}' has ContinueOnBranchFailure enabled. If any branch fails, execution will continue to NextStepId. Branch failures will be reported but won't stop the workflow.");
            }
        }
        
        // Validate NextStepId and ConditionalBranch.NextStepId references exist
        foreach (var step in workflow.Steps)
        {
            // Validate NextStepId
            if (!string.IsNullOrEmpty(step.NextStepId) && !stepIds.Contains(step.NextStepId))
            {
                errors.Add($"Step '{step.Name}' has a NextStepId that does not exist: '{step.NextStepId}'.");
            }
            
            // Validate ConditionalBranch.NextStepId references
            foreach (var branch in step.ConditionalBranches)
            {
                if (!string.IsNullOrEmpty(branch.NextStepId) && !stepIds.Contains(branch.NextStepId))
                {
                    errors.Add($"Step '{step.Name}' has a conditional branch '{branch.Label}' with a target step ID that does not exist: '{branch.NextStepId}'.");
                }
            }
        }
        
        // Validate FallbackStepId references exist
        foreach (var step in workflow.Steps)
        {
            if (step.ErrorHandling != null && !string.IsNullOrEmpty(step.ErrorHandling.FallbackStepId))
            {
                if (!stepIds.Contains(step.ErrorHandling.FallbackStepId))
                {
                    errors.Add($"Step '{step.Name}' has a fallback step ID that does not exist: '{step.ErrorHandling.FallbackStepId}'.");
                }
                else if (step.ErrorHandling.FallbackStepId == step.StepId)
                {
                    errors.Add($"Step '{step.Name}' cannot have itself as a fallback step (would cause infinite loop).");
                }
            }
        }
        
        return (errors.Count == 0, errors, warnings);
    }
    
    public void MigrateWorkflow(Workflow workflow)
    {
        // Ensure all steps have unique, non-empty StepIds
        var usedIds = new HashSet<string>();
        foreach (var step in workflow.Steps)
        {
            // Assign new ID if empty or duplicate
            if (string.IsNullOrEmpty(step.StepId) || usedIds.Contains(step.StepId))
            {
                step.StepId = Guid.NewGuid().ToString();
            }
            usedIds.Add(step.StepId);
        }
        
        // Set start step if not set
        if (!workflow.Steps.Any(s => s.IsStartStep) && workflow.Steps.Count > 0)
        {
            workflow.Steps.OrderBy(s => s.Order).First().IsStartStep = true;
        }
        
        // Link steps by Order if NextStepId not set (linear migration)
        var orderedSteps = workflow.Steps.OrderBy(s => s.Order).ToList();
        for (int i = 0; i < orderedSteps.Count - 1; i++)
        {
            if (string.IsNullOrEmpty(orderedSteps[i].NextStepId))
            {
                orderedSteps[i].NextStepId = orderedSteps[i + 1].StepId;
            }
        }
        
        // Set end step
        var lastStep = orderedSteps.LastOrDefault();
        if (lastStep != null && string.IsNullOrEmpty(lastStep.NextStepId))
        {
            lastStep.IsEndStep = true;
        }
        
        // Assign default positions if not set and migrate HasValidPosition
        const int startX = 50;
        const int startY = 50;
        const int verticalSpacing = 150;
        
        for (int i = 0; i < orderedSteps.Count; i++)
        {
            var step = orderedSteps[i];
            
            if (step.Position == default)
            {
                step.Position = new System.Windows.Point(startX, startY + i * verticalSpacing);
                step.HasValidPosition = true;
            }
            else if (!step.HasValidPosition)
            {
                // Bug 5 Fix: Migrate existing workflows - set HasValidPosition = true
                // for all steps with non-default positions
                step.HasValidPosition = true;
            }
        }
    }
    
    private WorkflowTemplate CreateCodeReviewWithConditionalTemplate()
    {
        var step1Id = Guid.NewGuid().ToString();
        var step2Id = Guid.NewGuid().ToString();
        var step3Id = Guid.NewGuid().ToString();
        var step4Id = Guid.NewGuid().ToString();
        
        return new WorkflowTemplate
        {
            Id = 1,
            Name = "Code Review with Conditional Severity",
            Description = "Analyzes code and branches based on issue severity",
            Category = "Review",
            IconKind = "CodeBraces",
            IsBuiltIn = true,
            TemplateWorkflow = new Workflow
            {
                Name = "Code Review with Conditional Severity",
                Steps = new List<WorkflowStep>
                {
                    new()
                    {
                        StepId = step1Id,
                        Order = 0,
                        Name = "Analyze Code",
                        StepType = WorkflowStepType.Conditional,
                        IsStartStep = true,
                        Position = new System.Windows.Point(50, 50),
                        HasValidPosition = true,
                        PromptTemplate = "Analyze this code for issues. Rate severity as CRITICAL, MINOR, or NONE:\n\n{{input}}",
                        OutputVariable = "analysis",
                        ConditionalBranches = new List<ConditionalBranch>
                        {
                            new() { Label = "Critical", NextStepId = step2Id, Condition = new ConditionEvaluator { Type = ConditionType.OutputContains, ComparisonValue = "CRITICAL", Operator = ComparisonOperator.Contains } },
                            new() { Label = "Minor", NextStepId = step3Id, Condition = new ConditionEvaluator { Type = ConditionType.OutputContains, ComparisonValue = "MINOR", Operator = ComparisonOperator.Contains } }
                        },
                        NextStepId = step4Id
                    },
                    new()
                    {
                        StepId = step2Id,
                        Order = 1,
                        Name = "Generate Critical Fixes",
                        Position = new System.Windows.Point(50, 200),
                        HasValidPosition = true,
                        PromptTemplate = "Generate fixes for these critical issues:\n\n{{analysis}}",
                        IsEndStep = true
                    },
                    new()
                    {
                        StepId = step3Id,
                        Order = 2,
                        Name = "Generate Suggestions",
                        Position = new System.Windows.Point(300, 200),
                        HasValidPosition = true,
                        PromptTemplate = "Generate improvement suggestions for these minor issues:\n\n{{analysis}}",
                        IsEndStep = true
                    },
                    new()
                    {
                        StepId = step4Id,
                        Order = 3,
                        Name = "No Issues Found",
                        Position = new System.Windows.Point(550, 200),
                        HasValidPosition = true,
                        PromptTemplate = "Confirm no issues found and provide a brief summary:\n\n{{analysis}}",
                        IsEndStep = true
                    }
                }
            }
        };
    }
    
    private WorkflowTemplate CreateIterativeRefinementTemplate()
    {
        var step1Id = Guid.NewGuid().ToString();
        var step2Id = Guid.NewGuid().ToString();
        
        return new WorkflowTemplate
        {
            Id = 2,
            Name = "Iterative Content Refinement",
            Description = "Refines content through multiple iterations until quality threshold is met",
            Category = "Writing",
            IconKind = "Repeat",
            IsBuiltIn = true,
            TemplateWorkflow = new Workflow
            {
                Name = "Iterative Content Refinement",
                Steps = new List<WorkflowStep>
                {
                    new()
                    {
                        StepId = step1Id,
                        Order = 0,
                        Name = "Generate and Refine",
                        StepType = WorkflowStepType.Loop,
                        IsStartStep = true,
                        Position = new System.Windows.Point(50, 50),
                        HasValidPosition = true,
                        PromptTemplate = "Iteration {{iteration_count}}: Generate or refine content based on:\n\n{{input}}\n\nPrevious version (if any): {{previous_output}}\n\nEnd your response with 'QUALITY: X/10' where X is your self-assessment.",
                        OutputVariable = "draft",
                        LoopConfig = new LoopConfig
                        {
                            MaxIterations = 5,
                            LoopVariable = "iteration_count",
                            ExitCondition = new ConditionEvaluator
                            {
                                Type = ConditionType.Regex,
                                ComparisonValue = "QUALITY: ([8-9]|10)/10"
                            }
                        },
                        NextStepId = step2Id
                    },
                    new()
                    {
                        StepId = step2Id,
                        Order = 1,
                        Name = "Final Polish",
                        Position = new System.Windows.Point(50, 200),
                        HasValidPosition = true,
                        PromptTemplate = "Polish this final draft:\n\n{{draft}}",
                        IsEndStep = true
                    }
                }
            }
        };
    }
    
    private WorkflowTemplate CreateErrorResilientProcessingTemplate()
    {
        var step1Id = Guid.NewGuid().ToString();
        var step2Id = Guid.NewGuid().ToString();
        
        return new WorkflowTemplate
        {
            Id = 3,
            Name = "Error-Resilient Processing",
            Description = "Processes data with automatic retry and error handling",
            Category = "Data",
            IconKind = "ShieldCheck",
            IsBuiltIn = true,
            TemplateWorkflow = new Workflow
            {
                Name = "Error-Resilient Processing",
                Steps = new List<WorkflowStep>
                {
                    new()
                    {
                        StepId = step1Id,
                        Order = 0,
                        Name = "Process Data",
                        IsStartStep = true,
                        Position = new System.Windows.Point(50, 50),
                        HasValidPosition = true,
                        PromptTemplate = "Process this data:\n\n{{input}}",
                        OutputVariable = "processed",
                        ErrorHandling = new ErrorHandlingConfig
                        {
                            MaxRetries = 3,
                            RetryDelayMs = 1000,
                            UseExponentialBackoff = true,
                            ContinueOnError = false
                        },
                        NextStepId = step2Id
                    },
                    new()
                    {
                        StepId = step2Id,
                        Order = 1,
                        Name = "Validate Results",
                        Position = new System.Windows.Point(50, 200),
                        HasValidPosition = true,
                        PromptTemplate = "Validate these results:\n\n{{processed}}",
                        IsEndStep = true,
                        ErrorHandling = new ErrorHandlingConfig
                        {
                            MaxRetries = 2,
                            ContinueOnError = true
                        }
                    }
                }
            }
        };
    }
    
    #endregion

    public async Task<List<Workflow>> GetCustomWorkflowsAsync()
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_connectionString);
            var collection = db.GetCollection<Workflow>("workflows");
            return collection.FindAll().ToList();
        });
    }


    #region Built-in Workflows

    private Workflow CreateCodeDevelopmentWorkflow()
    {
        return new Workflow
        {
            Id = -1,
            Name = "Code Development Pipeline",
            Description = "Complete code development workflow: analyze requirements, generate code, create tests, and refactor",
            Category = "Development",
            IsBuiltIn = true,
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Order = 1,
                    Name = "Problem Analysis",
                    Description = "Analyze the problem and break it down into components",
                    OutputVariable = "analysis",
                    PromptTemplate = @"You are a senior software architect. Analyze the following problem/requirement and provide a detailed breakdown:

**Problem/Requirement:**
{{input}}

Please provide:
1. **Problem Summary**: A clear, concise summary of what needs to be built
2. **Key Components**: List the main components/modules needed
3. **Data Structures**: Suggest appropriate data structures
4. **Algorithm Approach**: Outline the algorithmic approach
5. **Edge Cases**: Identify potential edge cases to handle
6. **Dependencies**: List any external dependencies or libraries needed

Be thorough but concise."
                },
                new()
                {
                    Order = 2,
                    Name = "Code Generation",
                    Description = "Generate the implementation code based on analysis",
                    OutputVariable = "code",
                    UsesPreviousOutput = true,
                    PromptTemplate = @"You are an expert software developer. Based on the following analysis, generate clean, well-documented code:

**Analysis:**
{{previous_output}}

**Original Requirement:**
{{initial_input}}

Please generate:
1. Complete, working code implementation
2. Include comprehensive comments
3. Follow best practices and design patterns
4. Use meaningful variable and function names
5. Include error handling

Provide the code in a single, well-organized file or clearly separated modules."
                },
                new()
                {
                    Order = 3,
                    Name = "Test Case Generation",
                    Description = "Generate comprehensive test cases for the code",
                    OutputVariable = "tests",
                    UsesPreviousOutput = true,
                    PromptTemplate = @"You are a QA engineer specializing in test automation. Generate comprehensive test cases for the following code:

**Code to Test:**
{{previous_output}}

**Original Requirement:**
{{initial_input}}

Please provide:
1. **Unit Tests**: Test individual functions/methods
2. **Integration Tests**: Test component interactions
3. **Edge Case Tests**: Test boundary conditions
4. **Error Handling Tests**: Test error scenarios
5. **Test Data**: Include sample test data

Use a common testing framework appropriate for the language. Include both positive and negative test cases."
                },
                new()
                {
                    Order = 4,
                    Name = "Code Refactoring",
                    Description = "Refactor and optimize the generated code",
                    OutputVariable = "refactored",
                    UsesPreviousOutput = false,
                    PromptTemplate = @"You are a code quality expert. Review and refactor the following code for optimal quality:

**Original Code:**
{{code}}

**Test Cases (for reference):**
{{tests}}

Please provide:
1. **Refactored Code**: Improved version with better structure
2. **Performance Optimizations**: Any performance improvements
3. **Code Smells Fixed**: List any code smells that were addressed
4. **Design Pattern Applications**: Any patterns applied
5. **Final Recommendations**: Additional suggestions for improvement

Ensure the refactored code still passes all test cases."
                }
            }
        };
    }

    private Workflow CreateCodeReviewWorkflow()
    {
        return new Workflow
        {
            Id = -2,
            Name = "Code Review Pipeline",
            Description = "Comprehensive code review: security, performance, best practices, and suggestions",
            Category = "Review",
            IsBuiltIn = true,
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Order = 1,
                    Name = "Security Analysis",
                    Description = "Analyze code for security vulnerabilities",
                    OutputVariable = "security",
                    PromptTemplate = @"You are a security expert. Analyze the following code for security vulnerabilities:

**Code to Review:**
{{input}}

Please identify:
1. **Critical Vulnerabilities**: Any severe security issues
2. **Potential Risks**: Medium-risk security concerns
3. **Input Validation**: Issues with user input handling
4. **Authentication/Authorization**: Any auth-related issues
5. **Data Exposure**: Potential data leaks or exposure
6. **Recommendations**: Specific fixes for each issue

Rate overall security: Critical/High/Medium/Low risk."
                },
                new()
                {
                    Order = 2,
                    Name = "Performance Analysis",
                    Description = "Analyze code for performance issues",
                    OutputVariable = "performance",
                    PromptTemplate = @"You are a performance optimization expert. Analyze the following code for performance:

**Code to Review:**
{{input}}

Please identify:
1. **Time Complexity**: Big O analysis of key operations
2. **Space Complexity**: Memory usage analysis
3. **Bottlenecks**: Potential performance bottlenecks
4. **Optimization Opportunities**: Specific improvements
5. **Caching Opportunities**: Where caching could help
6. **Async/Parallel**: Opportunities for parallelization

Rate overall performance: Excellent/Good/Needs Improvement/Poor."
                },
                new()
                {
                    Order = 3,
                    Name = "Best Practices Review",
                    Description = "Check adherence to coding best practices",
                    OutputVariable = "practices",
                    PromptTemplate = @"You are a senior developer focused on code quality. Review the following code for best practices:

**Code to Review:**
{{input}}

Please evaluate:
1. **Code Organization**: Structure and modularity
2. **Naming Conventions**: Variable, function, class names
3. **Documentation**: Comments and documentation quality
4. **Error Handling**: Exception handling patterns
5. **SOLID Principles**: Adherence to SOLID
6. **DRY/KISS**: Code duplication and complexity
7. **Testing**: Testability of the code

Provide specific examples and suggestions for improvement."
                },
                new()
                {
                    Order = 4,
                    Name = "Summary & Recommendations",
                    Description = "Compile final review summary with prioritized recommendations",
                    OutputVariable = "summary",
                    PromptTemplate = @"You are a tech lead compiling a code review. Create a comprehensive summary:

**Security Analysis:**
{{security}}

**Performance Analysis:**
{{performance}}

**Best Practices Review:**
{{practices}}

Please provide:
1. **Executive Summary**: Brief overview of code quality
2. **Priority Fixes**: Top 5 issues to address immediately
3. **Improvement Roadmap**: Suggested order of improvements
4. **Estimated Effort**: Time estimates for fixes
5. **Overall Score**: Rate the code 1-10 with justification

Format as a professional code review document."
                }
            }
        };
    }

    private Workflow CreateDocumentationWorkflow()
    {
        return new Workflow
        {
            Id = -3,
            Name = "Documentation Generator",
            Description = "Generate comprehensive documentation: API docs, README, and usage examples",
            Category = "Documentation",
            IsBuiltIn = true,
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Order = 1,
                    Name = "Code Analysis",
                    Description = "Analyze code structure and functionality",
                    OutputVariable = "analysis",
                    PromptTemplate = @"You are a technical writer. Analyze the following code to understand its structure:

**Code:**
{{input}}

Please identify:
1. **Purpose**: What does this code do?
2. **Public API**: All public functions/methods/classes
3. **Parameters**: Input parameters for each function
4. **Return Values**: What each function returns
5. **Dependencies**: External dependencies
6. **Configuration**: Any configuration options

Be thorough in identifying all documentable elements."
                },
                new()
                {
                    Order = 2,
                    Name = "API Documentation",
                    Description = "Generate detailed API documentation",
                    OutputVariable = "api_docs",
                    PromptTemplate = @"You are a technical documentation expert. Generate API documentation:

**Code Analysis:**
{{previous_output}}

**Original Code:**
{{initial_input}}

Generate comprehensive API documentation including:
1. **Function Signatures**: Complete signatures with types
2. **Parameter Descriptions**: Detailed parameter explanations
3. **Return Value Descriptions**: What is returned and when
4. **Exceptions/Errors**: Possible errors and when they occur
5. **Code Examples**: Usage examples for each function
6. **Notes**: Important usage notes or warnings

Use standard documentation format (JSDoc/Docstring style)."
                },
                new()
                {
                    Order = 3,
                    Name = "README Generation",
                    Description = "Generate a comprehensive README file",
                    OutputVariable = "readme",
                    PromptTemplate = @"You are creating a README for a project. Generate a comprehensive README:

**API Documentation:**
{{previous_output}}

**Code Analysis:**
{{analysis}}

Create a README.md with:
1. **Project Title & Description**: Clear project overview
2. **Features**: Key features list
3. **Installation**: Step-by-step installation guide
4. **Quick Start**: Getting started in 5 minutes
5. **Usage Examples**: Common use cases with code
6. **API Reference**: Summary of main API
7. **Configuration**: Configuration options
8. **Contributing**: How to contribute
9. **License**: License information placeholder

Use proper Markdown formatting."
                },
                new()
                {
                    Order = 4,
                    Name = "Usage Examples",
                    Description = "Generate practical usage examples",
                    OutputVariable = "examples",
                    PromptTemplate = @"You are creating usage examples for developers. Generate practical examples:

**README:**
{{previous_output}}

**API Documentation:**
{{api_docs}}

Create comprehensive usage examples:
1. **Basic Usage**: Simple getting-started example
2. **Common Patterns**: Typical use cases
3. **Advanced Usage**: Complex scenarios
4. **Integration Examples**: How to integrate with other tools
5. **Error Handling**: How to handle errors properly
6. **Best Practices**: Recommended patterns

Each example should be complete and runnable."
                }
            }
        };
    }

    private Workflow CreateBugFixWorkflow()
    {
        return new Workflow
        {
            Id = -4,
            Name = "Bug Fix Pipeline",
            Description = "Systematic bug fixing: diagnose, fix, test, and document",
            Category = "Debugging",
            IsBuiltIn = true,
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Order = 1,
                    Name = "Bug Diagnosis",
                    Description = "Analyze and diagnose the bug",
                    OutputVariable = "diagnosis",
                    PromptTemplate = @"You are a debugging expert. Analyze the following bug report and code:

**Bug Report/Code:**
{{input}}

Please provide:
1. **Bug Summary**: Clear description of the issue
2. **Root Cause Analysis**: What's causing the bug
3. **Affected Components**: Which parts of code are affected
4. **Reproduction Steps**: How to reproduce the bug
5. **Impact Assessment**: Severity and scope of impact
6. **Related Issues**: Potential related problems

Be systematic and thorough in your analysis."
                },
                new()
                {
                    Order = 2,
                    Name = "Fix Implementation",
                    Description = "Generate the bug fix code",
                    OutputVariable = "fix",
                    PromptTemplate = @"You are a senior developer fixing a bug. Implement the fix:

**Bug Diagnosis:**
{{previous_output}}

**Original Code/Report:**
{{initial_input}}

Please provide:
1. **Fixed Code**: Complete corrected code
2. **Changes Explained**: What was changed and why
3. **Before/After**: Show the specific changes
4. **Side Effects**: Any potential side effects of the fix
5. **Alternative Solutions**: Other possible approaches

Ensure the fix is minimal and focused on the bug."
                },
                new()
                {
                    Order = 3,
                    Name = "Regression Tests",
                    Description = "Generate tests to prevent regression",
                    OutputVariable = "tests",
                    PromptTemplate = @"You are a QA engineer. Create regression tests for the bug fix:

**Bug Fix:**
{{previous_output}}

**Bug Diagnosis:**
{{diagnosis}}

Create tests that:
1. **Reproduce Original Bug**: Test that would have caught the bug
2. **Verify Fix**: Test that the fix works correctly
3. **Edge Cases**: Related edge cases to test
4. **Regression Prevention**: Tests to prevent reintroduction
5. **Integration Tests**: Tests for affected integrations

Include test data and expected results."
                },
                new()
                {
                    Order = 4,
                    Name = "Fix Documentation",
                    Description = "Document the bug fix for future reference",
                    OutputVariable = "documentation",
                    PromptTemplate = @"You are documenting a bug fix. Create comprehensive documentation:

**Bug Diagnosis:**
{{diagnosis}}

**Fix Implementation:**
{{fix}}

**Regression Tests:**
{{tests}}

Create documentation including:
1. **Bug Report**: Formal bug description
2. **Root Cause**: Technical explanation
3. **Solution**: How it was fixed
4. **Testing**: How to verify the fix
5. **Prevention**: How to prevent similar bugs
6. **Changelog Entry**: Entry for release notes

Format as a professional bug fix report."
                }
            }
        };
    }

    private Workflow CreateFeatureDesignWorkflow()
    {
        return new Workflow
        {
            Id = -5,
            Name = "Feature Design Pipeline",
            Description = "Design a new feature: requirements, architecture, implementation plan, and API design",
            Category = "Design",
            IsBuiltIn = true,
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Order = 1,
                    Name = "Requirements Analysis",
                    Description = "Analyze and formalize feature requirements",
                    OutputVariable = "requirements",
                    PromptTemplate = @"You are a product manager/business analyst. Analyze the feature request:

**Feature Request:**
{{input}}

Please provide:
1. **Feature Summary**: Clear description of the feature
2. **User Stories**: Who needs this and why
3. **Functional Requirements**: What it must do
4. **Non-Functional Requirements**: Performance, security, etc.
5. **Acceptance Criteria**: How to verify completion
6. **Out of Scope**: What this feature does NOT include
7. **Dependencies**: Prerequisites and dependencies

Be specific and measurable in requirements."
                },
                new()
                {
                    Order = 2,
                    Name = "Architecture Design",
                    Description = "Design the technical architecture",
                    OutputVariable = "architecture",
                    PromptTemplate = @"You are a software architect. Design the architecture for this feature:

**Requirements:**
{{previous_output}}

**Original Request:**
{{initial_input}}

Please provide:
1. **High-Level Design**: Overall architecture approach
2. **Component Diagram**: Key components and relationships
3. **Data Model**: Required data structures/schemas
4. **Integration Points**: How it connects to existing system
5. **Technology Choices**: Recommended technologies
6. **Scalability Considerations**: How it will scale
7. **Security Considerations**: Security measures needed

Include diagrams in text/ASCII format where helpful."
                },
                new()
                {
                    Order = 3,
                    Name = "API Design",
                    Description = "Design the API interface",
                    OutputVariable = "api",
                    PromptTemplate = @"You are an API designer. Design the API for this feature:

**Architecture:**
{{previous_output}}

**Requirements:**
{{requirements}}

Please provide:
1. **API Endpoints**: All endpoints with methods
2. **Request/Response Schemas**: JSON schemas
3. **Authentication**: Auth requirements
4. **Error Handling**: Error codes and messages
5. **Rate Limiting**: Throttling considerations
6. **Versioning**: API versioning strategy
7. **Examples**: Sample requests and responses

Follow RESTful best practices or appropriate paradigm."
                },
                new()
                {
                    Order = 4,
                    Name = "Implementation Plan",
                    Description = "Create detailed implementation plan",
                    OutputVariable = "plan",
                    PromptTemplate = @"You are a tech lead creating an implementation plan:

**Requirements:**
{{requirements}}

**Architecture:**
{{architecture}}

**API Design:**
{{api}}

Create an implementation plan:
1. **Task Breakdown**: Detailed task list
2. **Sprint Planning**: Suggested sprint allocation
3. **Dependencies**: Task dependencies
4. **Risk Assessment**: Technical risks and mitigations
5. **Testing Strategy**: How to test each component
6. **Rollout Plan**: Deployment strategy
7. **Success Metrics**: How to measure success

Include time estimates for each task."
                }
            }
        };
    }

    private Workflow CreateCodeMigrationWorkflow()
    {
        return new Workflow
        {
            Id = -6,
            Name = "Code Migration Pipeline",
            Description = "Migrate code between languages/frameworks: analyze, convert, validate, and optimize",
            Category = "Migration",
            IsBuiltIn = true,
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Order = 1,
                    Name = "Source Analysis",
                    Description = "Analyze the source code structure and dependencies",
                    OutputVariable = "analysis",
                    PromptTemplate = @"You are a code migration expert. Analyze the following source code:

**Source Code:**
{{input}}

Please identify:
1. **Language/Framework**: Current technology stack
2. **Code Structure**: Classes, functions, modules
3. **Dependencies**: External libraries and their purposes
4. **Design Patterns**: Patterns used in the code
5. **Business Logic**: Core functionality and algorithms
6. **Data Structures**: How data is organized
7. **API Contracts**: External interfaces

Be thorough in understanding the code before migration."
                },
                new()
                {
                    Order = 2,
                    Name = "Migration Strategy",
                    Description = "Plan the migration approach",
                    OutputVariable = "strategy",
                    PromptTemplate = @"You are planning a code migration. Create a migration strategy:

**Source Analysis:**
{{previous_output}}

**Original Code:**
{{initial_input}}

Please provide:
1. **Target Recommendations**: Best target language/framework options
2. **Equivalent Libraries**: Replacement libraries for dependencies
3. **Pattern Mapping**: How patterns translate to target
4. **Breaking Changes**: Incompatibilities to address
5. **Migration Order**: Suggested order of migration
6. **Risk Areas**: Parts that need special attention

Recommend the most suitable target technology."
                },
                new()
                {
                    Order = 3,
                    Name = "Code Conversion",
                    Description = "Convert the code to target language/framework",
                    OutputVariable = "converted",
                    PromptTemplate = @"You are converting code to a new language/framework:

**Migration Strategy:**
{{previous_output}}

**Source Analysis:**
{{analysis}}

**Original Code:**
{{initial_input}}

Please provide:
1. **Converted Code**: Complete working code in target language
2. **Idiomatic Patterns**: Use target language best practices
3. **Comments**: Explain non-obvious conversions
4. **Dependency Setup**: Package/dependency configuration
5. **Configuration Files**: Any needed config files

Ensure the converted code is idiomatic and follows best practices."
                },
                new()
                {
                    Order = 4,
                    Name = "Validation & Testing",
                    Description = "Create validation tests and migration checklist",
                    OutputVariable = "validation",
                    PromptTemplate = @"You are validating a code migration:

**Converted Code:**
{{previous_output}}

**Original Code:**
{{initial_input}}

**Migration Strategy:**
{{strategy}}

Please provide:
1. **Equivalence Tests**: Tests to verify same behavior
2. **Migration Checklist**: Verification checklist
3. **Performance Comparison**: Expected performance differences
4. **Known Limitations**: Any functionality gaps
5. **Rollback Plan**: How to revert if needed
6. **Documentation Updates**: What docs need updating

Ensure functional equivalence with the original."
                }
            }
        };
    }

    private Workflow CreateContentCreationWorkflow()
    {
        return new Workflow
        {
            Id = -7,
            Name = "Content Creation Pipeline",
            Description = "Create professional content: research, outline, draft, and polish",
            Category = "Writing",
            IsBuiltIn = true,
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Order = 1,
                    Name = "Research & Analysis",
                    Description = "Research the topic and gather key points",
                    OutputVariable = "research",
                    PromptTemplate = @"You are a content researcher. Analyze the following topic:

**Topic/Brief:**
{{input}}

Please provide:
1. **Topic Overview**: What this content should cover
2. **Target Audience**: Who will read this
3. **Key Points**: Main points to address
4. **Supporting Facts**: Important data/statistics to include
5. **Common Questions**: FAQs about this topic
6. **Unique Angles**: Fresh perspectives to consider
7. **Tone & Style**: Recommended writing approach

Be thorough in understanding the content needs."
                },
                new()
                {
                    Order = 2,
                    Name = "Content Outline",
                    Description = "Create a detailed content outline",
                    OutputVariable = "outline",
                    PromptTemplate = @"You are creating a content outline:

**Research:**
{{previous_output}}

**Original Brief:**
{{initial_input}}

Create a detailed outline:
1. **Title Options**: 3-5 compelling title options
2. **Hook/Introduction**: Opening that grabs attention
3. **Main Sections**: Detailed section breakdown
4. **Key Arguments**: Points to make in each section
5. **Examples/Stories**: Illustrations to include
6. **Call to Action**: Desired reader action
7. **SEO Keywords**: If applicable, target keywords

Structure for maximum engagement and clarity."
                },
                new()
                {
                    Order = 3,
                    Name = "First Draft",
                    Description = "Write the complete first draft",
                    OutputVariable = "draft",
                    PromptTemplate = @"You are writing the first draft:

**Content Outline:**
{{previous_output}}

**Research:**
{{research}}

Write a complete first draft:
1. **Engaging Opening**: Hook the reader immediately
2. **Clear Structure**: Follow the outline
3. **Compelling Content**: Informative and engaging
4. **Smooth Transitions**: Flow between sections
5. **Strong Conclusion**: Memorable ending
6. **Appropriate Length**: Match the content type

Write naturally and engagingly for the target audience."
                },
                new()
                {
                    Order = 4,
                    Name = "Polish & Optimize",
                    Description = "Edit, polish, and optimize the content",
                    OutputVariable = "final",
                    PromptTemplate = @"You are a professional editor. Polish this content:

**First Draft:**
{{previous_output}}

**Original Brief:**
{{initial_input}}

Please provide:
1. **Edited Content**: Polished final version
2. **Grammar/Style Fixes**: Corrections made
3. **Clarity Improvements**: Simplified complex parts
4. **Engagement Enhancements**: Made more compelling
5. **SEO Optimization**: If applicable
6. **Meta Description**: Summary for sharing
7. **Social Snippets**: Shareable quotes/excerpts

Deliver publication-ready content."
                }
            }
        };
    }

    private Workflow CreateDataAnalysisWorkflow()
    {
        return new Workflow
        {
            Id = -8,
            Name = "Data Analysis Pipeline",
            Description = "Analyze data: explore, analyze, visualize, and report",
            Category = "Analysis",
            IsBuiltIn = true,
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Order = 1,
                    Name = "Data Exploration",
                    Description = "Understand the data structure and quality",
                    OutputVariable = "exploration",
                    PromptTemplate = @"You are a data analyst. Explore the following data:

**Data/Description:**
{{input}}

Please analyze:
1. **Data Structure**: Columns, types, relationships
2. **Data Quality**: Missing values, outliers, inconsistencies
3. **Key Variables**: Most important fields
4. **Data Distribution**: Basic statistics
5. **Potential Issues**: Data quality concerns
6. **Initial Observations**: Interesting patterns noticed
7. **Questions to Answer**: What can this data tell us

Provide a thorough understanding of the data."
                },
                new()
                {
                    Order = 2,
                    Name = "Analysis Plan",
                    Description = "Create an analysis strategy",
                    OutputVariable = "plan",
                    PromptTemplate = @"You are planning a data analysis:

**Data Exploration:**
{{previous_output}}

**Original Data/Question:**
{{initial_input}}

Create an analysis plan:
1. **Analysis Objectives**: What we want to learn
2. **Hypotheses**: Assumptions to test
3. **Methods**: Statistical/analytical approaches
4. **Metrics**: Key metrics to calculate
5. **Segmentation**: How to slice the data
6. **Comparisons**: What to compare
7. **Tools/Code**: Suggested tools or code snippets

Design a comprehensive analysis approach."
                },
                new()
                {
                    Order = 3,
                    Name = "Analysis & Insights",
                    Description = "Perform analysis and extract insights",
                    OutputVariable = "insights",
                    PromptTemplate = @"You are performing data analysis:

**Analysis Plan:**
{{previous_output}}

**Data Exploration:**
{{exploration}}

Provide analysis results:
1. **Key Findings**: Main discoveries
2. **Statistical Results**: Numbers and calculations
3. **Patterns & Trends**: Identified patterns
4. **Correlations**: Relationships found
5. **Anomalies**: Unexpected findings
6. **Segment Analysis**: Differences across groups
7. **Visualization Suggestions**: Charts to create

Extract actionable insights from the data."
                },
                new()
                {
                    Order = 4,
                    Name = "Report & Recommendations",
                    Description = "Create final report with recommendations",
                    OutputVariable = "report",
                    PromptTemplate = @"You are creating an analysis report:

**Analysis & Insights:**
{{previous_output}}

**Analysis Plan:**
{{plan}}

**Original Question:**
{{initial_input}}

Create a comprehensive report:
1. **Executive Summary**: Key takeaways
2. **Methodology**: How analysis was done
3. **Key Findings**: Main results with evidence
4. **Visualizations**: Described charts/graphs
5. **Recommendations**: Data-driven suggestions
6. **Limitations**: Caveats and constraints
7. **Next Steps**: Further analysis needed

Format as a professional analysis report."
                }
            }
        };
    }

    private Workflow CreateLearningPathWorkflow()
    {
        return new Workflow
        {
            Id = -9,
            Name = "Learning Path Generator",
            Description = "Create personalized learning paths: assess, plan, resources, and milestones",
            Category = "Education",
            IsBuiltIn = true,
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Order = 1,
                    Name = "Goal Assessment",
                    Description = "Understand learning goals and current level",
                    OutputVariable = "assessment",
                    PromptTemplate = @"You are an education specialist. Assess the learning request:

**Learning Goal:**
{{input}}

Please analyze:
1. **Goal Clarity**: What exactly needs to be learned
2. **Skill Level**: Assumed current knowledge
3. **Prerequisites**: Required foundational knowledge
4. **Time Frame**: Realistic learning duration
5. **Learning Style**: Best approaches for this topic
6. **Success Criteria**: How to measure mastery
7. **Motivation**: Why this is being learned

Understand the complete learning context."
                },
                new()
                {
                    Order = 2,
                    Name = "Curriculum Design",
                    Description = "Design the learning curriculum",
                    OutputVariable = "curriculum",
                    PromptTemplate = @"You are designing a learning curriculum:

**Assessment:**
{{previous_output}}

**Learning Goal:**
{{initial_input}}

Create a structured curriculum:
1. **Learning Modules**: Organized topic breakdown
2. **Sequence**: Optimal learning order
3. **Time Allocation**: Hours per module
4. **Key Concepts**: Core ideas per module
5. **Practical Exercises**: Hands-on activities
6. **Projects**: Real-world applications
7. **Assessment Points**: Knowledge checks

Design for effective skill building."
                },
                new()
                {
                    Order = 3,
                    Name = "Resource Compilation",
                    Description = "Compile learning resources and materials",
                    OutputVariable = "resources",
                    PromptTemplate = @"You are compiling learning resources:

**Curriculum:**
{{previous_output}}

**Assessment:**
{{assessment}}

Provide resources for each module:
1. **Primary Resources**: Main learning materials
2. **Video Content**: Recommended videos/courses
3. **Reading Materials**: Books, articles, docs
4. **Practice Platforms**: Where to practice
5. **Community Resources**: Forums, groups
6. **Tools Needed**: Software/tools required
7. **Free vs Paid**: Cost considerations

Curate high-quality, accessible resources."
                },
                new()
                {
                    Order = 4,
                    Name = "Study Plan & Milestones",
                    Description = "Create actionable study plan with milestones",
                    OutputVariable = "plan",
                    PromptTemplate = @"You are creating a study plan:

**Resources:**
{{previous_output}}

**Curriculum:**
{{curriculum}}

**Learning Goal:**
{{initial_input}}

Create an actionable plan:
1. **Weekly Schedule**: Day-by-day breakdown
2. **Milestones**: Key achievement points
3. **Mini-Projects**: Practice projects per phase
4. **Review Sessions**: Spaced repetition schedule
5. **Progress Tracking**: How to measure progress
6. **Accountability**: Stay-on-track strategies
7. **Completion Criteria**: How to know you're done

Make it practical and achievable."
                }
            }
        };
    }

    private Workflow CreateProjectPlanningWorkflow()
    {
        return new Workflow
        {
            Id = -10,
            Name = "Project Planning Pipeline",
            Description = "Plan projects: scope, breakdown, timeline, and risk management",
            Category = "Management",
            IsBuiltIn = true,
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Order = 1,
                    Name = "Scope Definition",
                    Description = "Define project scope and objectives",
                    OutputVariable = "scope",
                    PromptTemplate = @"You are a project manager. Define the project scope:

**Project Description:**
{{input}}

Please define:
1. **Project Objectives**: Clear, measurable goals
2. **Deliverables**: What will be produced
3. **In Scope**: What's included
4. **Out of Scope**: What's explicitly excluded
5. **Stakeholders**: Who's involved
6. **Success Criteria**: How success is measured
7. **Constraints**: Budget, time, resource limits

Create a clear project charter."
                },
                new()
                {
                    Order = 2,
                    Name = "Work Breakdown",
                    Description = "Break down work into tasks",
                    OutputVariable = "wbs",
                    PromptTemplate = @"You are creating a work breakdown structure:

**Project Scope:**
{{previous_output}}

**Project Description:**
{{initial_input}}

Create a detailed WBS:
1. **Major Phases**: High-level project phases
2. **Work Packages**: Grouped tasks
3. **Individual Tasks**: Specific activities
4. **Dependencies**: Task relationships
5. **Effort Estimates**: Time per task
6. **Resource Needs**: Skills/people needed
7. **Deliverables Map**: What each phase produces

Break down to manageable, estimable tasks."
                },
                new()
                {
                    Order = 3,
                    Name = "Timeline & Schedule",
                    Description = "Create project timeline and schedule",
                    OutputVariable = "timeline",
                    PromptTemplate = @"You are creating a project schedule:

**Work Breakdown:**
{{previous_output}}

**Project Scope:**
{{scope}}

Create a project timeline:
1. **Gantt Chart**: Visual timeline (text format)
2. **Critical Path**: Must-complete-on-time tasks
3. **Milestones**: Key dates and checkpoints
4. **Sprint/Phase Planning**: If agile, sprint breakdown
5. **Buffer Time**: Contingency allowances
6. **Resource Calendar**: Who does what when
7. **Dependencies Timeline**: Sequenced activities

Create a realistic, achievable schedule."
                },
                new()
                {
                    Order = 4,
                    Name = "Risk & Communication Plan",
                    Description = "Identify risks and create communication plan",
                    OutputVariable = "risks",
                    PromptTemplate = @"You are completing project planning:

**Timeline:**
{{previous_output}}

**Work Breakdown:**
{{wbs}}

**Project Scope:**
{{scope}}

Create risk and communication plans:
1. **Risk Register**: Identified risks with probability/impact
2. **Mitigation Strategies**: How to reduce risks
3. **Contingency Plans**: If risks occur
4. **Communication Plan**: Who, what, when, how
5. **Status Reporting**: Report templates/frequency
6. **Escalation Path**: Issue escalation process
7. **Kickoff Agenda**: Project kickoff meeting plan

Prepare for successful project execution."
                }
            }
        };
    }

    #endregion
}
