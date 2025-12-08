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
        var stepOutputs = new Dictionary<string, string>
        {
            { "input", initialInput },
            { "initial_input", initialInput }
        };
        
        string previousOutput = initialInput;

        for (int i = 0; i < workflow.Steps.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            var step = workflow.Steps[i];
            var stopwatch = Stopwatch.StartNew();
            
            StepStarted?.Invoke(this, new WorkflowStepEventArgs
            {
                StepIndex = i,
                StepName = step.Name,
                TotalSteps = workflow.Steps.Count
            });

            // Build the prompt with variable substitution
            var prompt = BuildStepPrompt(step, stepOutputs, previousOutput);
            
            var result = new WorkflowStepResult
            {
                StepOrder = step.Order,
                StepName = step.Name,
                Input = prompt
            };

            try
            {
                var response = await _aiService.GenerateAsync(prompt, settings);
                stopwatch.Stop();
                
                result.Success = response.Success;
                result.Output = response.Content;
                result.Error = response.Error;
                result.Duration = stopwatch.Elapsed;

                if (response.Success)
                {
                    previousOutput = response.Content;
                    
                    // Store output for variable substitution
                    if (!string.IsNullOrEmpty(step.OutputVariable))
                        stepOutputs[step.OutputVariable] = response.Content;
                    
                    stepOutputs[$"step{i + 1}"] = response.Content;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Error = ex.Message;
                result.Duration = stopwatch.Elapsed;
            }

            StepCompleted?.Invoke(this, new WorkflowStepEventArgs
            {
                StepIndex = i,
                StepName = step.Name,
                TotalSteps = workflow.Steps.Count,
                Result = result
            });

            yield return result;

            // Stop if step failed
            if (!result.Success)
                yield break;
        }
    }

    private string BuildStepPrompt(WorkflowStep step, Dictionary<string, string> variables, string previousOutput)
    {
        var prompt = step.PromptTemplate;
        
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
