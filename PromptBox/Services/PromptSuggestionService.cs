using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PromptBox.Services;

/// <summary>
/// Service for intelligent prompt suggestions
/// </summary>
public class PromptSuggestionService : IPromptSuggestionService
{
    public List<PromptSuggestion> GetQuickSuggestions(string prompt)
    {
        var suggestions = new List<PromptSuggestion>();
        
        if (string.IsNullOrWhiteSpace(prompt))
            return suggestions;

        var lowerPrompt = prompt.ToLower();

        // Check for missing role/persona
        if (!ContainsRoleIndicator(lowerPrompt))
        {
            suggestions.Add(new PromptSuggestion
            {
                Title = "Add a Role",
                Description = "Define who the AI should act as for better responses",
                SuggestedText = "Act as an expert {{role}} and ",
                Type = SuggestionType.AddRole
            });
        }

        // Check for missing format specification
        if (!ContainsFormatIndicator(lowerPrompt))
        {
            suggestions.Add(new PromptSuggestion
            {
                Title = "Specify Output Format",
                Description = "Tell the AI how to structure the response",
                SuggestedText = "\n\nFormat the response as: {{format}}",
                Type = SuggestionType.AddFormat
            });
        }

        // Check for missing context
        if (prompt.Length < 100 && !ContainsContextIndicator(lowerPrompt))
        {
            suggestions.Add(new PromptSuggestion
            {
                Title = "Add Context",
                Description = "Provide background information for better results",
                SuggestedText = "\n\nContext: {{context}}",
                Type = SuggestionType.AddContext
            });
        }

        // Check for missing constraints
        if (!ContainsConstraintIndicator(lowerPrompt))
        {
            suggestions.Add(new PromptSuggestion
            {
                Title = "Add Constraints",
                Description = "Set boundaries for more focused responses",
                SuggestedText = "\n\nConstraints:\n- Keep it under {{length}}\n- Focus on {{focus}}",
                Type = SuggestionType.AddConstraints
            });
        }

        // Check for missing examples
        if (!ContainsExampleIndicator(lowerPrompt))
        {
            suggestions.Add(new PromptSuggestion
            {
                Title = "Include Examples",
                Description = "Show the AI what you're looking for",
                SuggestedText = "\n\nExample:\nInput: {{example_input}}\nOutput: {{example_output}}",
                Type = SuggestionType.AddExamples
            });
        }

        // Check for missing tone
        if (!ContainsToneIndicator(lowerPrompt))
        {
            suggestions.Add(new PromptSuggestion
            {
                Title = "Specify Tone",
                Description = "Define the communication style",
                SuggestedText = "\n\nTone: {{tone}} (e.g., professional, casual, friendly)",
                Type = SuggestionType.AddTone
            });
        }

        return suggestions.Take(4).ToList(); // Return top 4 suggestions
    }

    public List<PromptStarter> GetPromptStarters()
    {
        return new List<PromptStarter>
        {
            new() { Name = "Code Review", Category = "Development", Icon = "CodeBraces",
                Template = "Act as a senior software engineer. Review the following code for:\n- Bugs and potential issues\n- Performance improvements\n- Best practices\n- Security concerns\n\nCode:\n```\n{{code}}\n```\n\nProvide specific, actionable feedback." },
            
            new() { Name = "Explain Concept", Category = "Learning", Icon = "School",
                Template = "Explain {{concept}} in simple terms.\n\nTarget audience: {{audience}}\nDepth: {{depth}} (beginner/intermediate/advanced)\n\nInclude:\n- Key points\n- Real-world examples\n- Common misconceptions" },
            
            new() { Name = "Write Documentation", Category = "Technical", Icon = "FileDocument",
                Template = "Write technical documentation for {{subject}}.\n\nInclude:\n- Overview\n- Prerequisites\n- Step-by-step instructions\n- Code examples\n- Troubleshooting tips\n\nFormat: Markdown\nTone: Professional but approachable" },
            
            new() { Name = "Debug Issue", Category = "Development", Icon = "Bug",
                Template = "Help me debug this issue:\n\nProblem: {{problem}}\n\nExpected behavior: {{expected}}\nActual behavior: {{actual}}\n\nRelevant code:\n```\n{{code}}\n```\n\nError message (if any): {{error}}\n\nProvide step-by-step debugging approach." },
            
            new() { Name = "Brainstorm Ideas", Category = "Creative", Icon = "Lightbulb",
                Template = "Brainstorm {{count}} creative ideas for {{topic}}.\n\nContext: {{context}}\nConstraints: {{constraints}}\n\nFor each idea, provide:\n- Brief description\n- Key benefits\n- Potential challenges\n- Implementation difficulty (1-5)" },
            
            new() { Name = "Write Email", Category = "Communication", Icon = "Email",
                Template = "Write a {{tone}} email about {{subject}}.\n\nRecipient: {{recipient}}\nPurpose: {{purpose}}\nKey points to include:\n- {{point1}}\n- {{point2}}\n\nLength: {{length}}\nCall to action: {{cta}}" },
            
            new() { Name = "Create Test Cases", Category = "QA", Icon = "TestTube",
                Template = "Create comprehensive test cases for {{feature}}.\n\nInclude:\n- Happy path scenarios\n- Edge cases\n- Error handling\n- Boundary conditions\n\nFormat each test case with:\n- Test ID\n- Description\n- Preconditions\n- Steps\n- Expected result" },
            
            new() { Name = "Refactor Code", Category = "Development", Icon = "Wrench",
                Template = "Refactor the following code to improve:\n- Readability\n- Maintainability\n- Performance\n- {{specific_goal}}\n\nCode:\n```\n{{code}}\n```\n\nExplain each change and why it improves the code." },
            
            new() { Name = "API Design", Category = "Architecture", Icon = "Api",
                Template = "Design a REST API for {{feature}}.\n\nRequirements:\n{{requirements}}\n\nInclude:\n- Endpoints (method, path, description)\n- Request/response schemas\n- Error handling\n- Authentication approach\n- Rate limiting considerations" },
            
            new() { Name = "Data Analysis", Category = "Analytics", Icon = "ChartBar",
                Template = "Analyze the following data and provide insights:\n\n{{data}}\n\nFocus on:\n- Key trends\n- Anomalies\n- Correlations\n- Actionable recommendations\n\nPresent findings in a clear, structured format." }
        };
    }

    private bool ContainsRoleIndicator(string prompt)
    {
        var rolePatterns = new[] { "act as", "you are", "pretend", "role", "persona", "expert", "specialist" };
        return rolePatterns.Any(p => prompt.Contains(p));
    }

    private bool ContainsFormatIndicator(string prompt)
    {
        var formatPatterns = new[] { "format", "structure", "output as", "respond with", "list", "bullet", "markdown", "json", "table" };
        return formatPatterns.Any(p => prompt.Contains(p));
    }

    private bool ContainsContextIndicator(string prompt)
    {
        var contextPatterns = new[] { "context", "background", "situation", "scenario", "given that", "considering" };
        return contextPatterns.Any(p => prompt.Contains(p));
    }

    private bool ContainsConstraintIndicator(string prompt)
    {
        var constraintPatterns = new[] { "constraint", "limit", "maximum", "minimum", "must", "should not", "avoid", "only", "within" };
        return constraintPatterns.Any(p => prompt.Contains(p));
    }

    private bool ContainsExampleIndicator(string prompt)
    {
        var examplePatterns = new[] { "example", "for instance", "such as", "like this", "sample", "e.g." };
        return examplePatterns.Any(p => prompt.Contains(p));
    }

    private bool ContainsToneIndicator(string prompt)
    {
        var tonePatterns = new[] { "tone", "style", "voice", "formal", "casual", "friendly", "professional", "humorous" };
        return tonePatterns.Any(p => prompt.Contains(p));
    }
}
