using PromptBox.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PromptBox.Services;

/// <summary>
/// Service providing a comprehensive library of pre-built prompt templates
/// </summary>
public class PromptLibraryService : IPromptLibraryService
{
    private readonly List<PromptTemplate> _templates;
    private readonly IPromptCommunityService? _communityService;

    public PromptLibraryService()
    {
        _templates = InitializeLibrary();
    }

    public PromptLibraryService(IPromptCommunityService communityService)
    {
        _communityService = communityService;
        _templates = InitializeLibrary();
    }

    // Synchronous methods (for backward compatibility - local templates only)
    
    /// <summary>
    /// Gets all local built-in templates only. Does not include community templates.
    /// </summary>
    /// <remarks>
    /// This synchronous method operates on local built-in templates only.
    /// To include community templates, use <see cref="GetAllTemplatesAsync"/> instead.
    /// </remarks>
    /// <returns>A list of local built-in prompt templates.</returns>
    public List<PromptTemplate> GetAllTemplates() => _templates.ToList();

    /// <summary>
    /// Gets local built-in templates filtered by category. Does not include community templates.
    /// </summary>
    /// <remarks>
    /// This synchronous method operates on local built-in templates only.
    /// To include community templates, use <see cref="SearchTemplatesAsync"/> with appropriate filters.
    /// </remarks>
    /// <param name="category">The category to filter by.</param>
    /// <returns>A list of local built-in prompt templates in the specified category.</returns>
    public List<PromptTemplate> GetTemplatesByCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return _templates.ToList();
        return _templates.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Gets all categories from local built-in templates only. Does not include community template categories.
    /// </summary>
    /// <remarks>
    /// This synchronous method operates on local built-in templates only.
    /// Community templates may have additional categories not returned by this method.
    /// </remarks>
    /// <returns>A list of category names from local built-in templates.</returns>
    public List<string> GetCategories() => _templates.Select(t => t.Category).Distinct().OrderBy(c => c).ToList();

    /// <summary>
    /// Gets a local built-in template by ID. Does not search community templates.
    /// </summary>
    /// <remarks>
    /// This synchronous method operates on local built-in templates only.
    /// To find community templates by ID, use <see cref="GetAllTemplatesAsync"/> and filter the results.
    /// </remarks>
    /// <param name="id">The template ID to search for.</param>
    /// <returns>The matching template, or null if not found in local templates.</returns>
    public PromptTemplate? GetTemplateById(string id) => _templates.FirstOrDefault(t => t.Id == id);

    /// <summary>
    /// Searches local built-in templates only. Does not include community templates in search.
    /// </summary>
    /// <remarks>
    /// This synchronous method operates on local built-in templates only.
    /// To search including community templates, use <see cref="SearchTemplatesAsync"/> instead.
    /// </remarks>
    /// <param name="query">The search query to match against title, description, category, and tags.</param>
    /// <returns>A list of matching local built-in prompt templates.</returns>
    public List<PromptTemplate> SearchTemplates(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return _templates.ToList();
        var q = query.ToLowerInvariant();
        return _templates.Where(t =>
            t.Title.ToLowerInvariant().Contains(q) ||
            t.Description.ToLowerInvariant().Contains(q) ||
            t.Category.ToLowerInvariant().Contains(q) ||
            t.Tags.Any(tag => tag.ToLowerInvariant().Contains(q))
        ).ToList();
    }

    // Async methods with community support
    public async Task<List<PromptTemplate>> GetAllTemplatesAsync(bool includeCommunity = true)
    {
        var allTemplates = _templates.ToList();
        
        if (includeCommunity && _communityService != null)
        {
            try
            {
                var communityTemplates = await _communityService.FetchCommunityTemplatesAsync();
                allTemplates.AddRange(communityTemplates);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching community templates: {ex.Message}");
            }
        }
        
        return allTemplates
            .OrderByDescending(t => t.DownloadCount)
            .ThenBy(t => t.Title)
            .ToList();
    }

    public async Task<List<PromptTemplate>> GetCommunityTemplatesAsync()
    {
        if (_communityService == null)
            return new List<PromptTemplate>();
        
        return await _communityService.FetchCommunityTemplatesAsync();
    }

    public async Task<List<PromptTemplate>> SearchTemplatesAsync(string query, string source = "All")
    {
        var templates = await GetAllTemplatesAsync(source != "Local");
        
        // Filter by source
        if (source == "Local")
            templates = templates.Where(t => t.IsOfficial && !t.IsCommunity).ToList();
        else if (source == "Community")
            templates = templates.Where(t => t.IsCommunity).ToList();
        
        // Apply search query
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.ToLowerInvariant();
            templates = templates.Where(t =>
                t.Title.ToLowerInvariant().Contains(q) ||
                t.Description.ToLowerInvariant().Contains(q) ||
                t.Category.ToLowerInvariant().Contains(q) ||
                t.Tags.Any(tag => tag.ToLowerInvariant().Contains(q))
            ).ToList();
        }
        
        return templates;
    }

    public async Task<bool> RefreshCommunityTemplatesAsync()
    {
        if (_communityService == null)
            return false;
        
        try
        {
            await _communityService.FetchCommunityTemplatesAsync(forceRefresh: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task RecordLocalDownloadAsync(string templateId)
    {
        var template = _templates.FirstOrDefault(t => t.Id == templateId);
        if (template != null)
        {
            template.DownloadCount++;
        }
        return Task.CompletedTask;
    }

    private static List<PromptTemplate> InitializeLibrary()
    {
        var templates = new List<PromptTemplate>
        {
            // === CODING ===
            new()
            {
                Id = "code-review",
                Title = "Code Review Assistant",
                Category = "Coding",
                Tags = new List<string> { "code", "review", "quality" },
                Description = "Get thorough code reviews with actionable feedback",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Please review the following code and provide feedback on:

1. **Code Quality**: Readability, naming conventions, and structure
2. **Potential Bugs**: Logic errors, edge cases, null checks
3. **Performance**: Inefficiencies, unnecessary operations
4. **Security**: Vulnerabilities, input validation
5. **Best Practices**: Design patterns, SOLID principles

Code to review:
```
[PASTE YOUR CODE HERE]
```

Please provide specific suggestions with code examples where applicable."
            },
            new()
            {
                Id = "debug-helper",
                Title = "Debug Helper",
                Category = "Coding",
                Tags = new List<string> { "debug", "error", "troubleshoot" },
                Description = "Get help debugging errors and issues",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"I'm encountering an issue and need help debugging.

**Error Message:**
```
[PASTE ERROR MESSAGE]
```

**Relevant Code:**
```
[PASTE CODE]
```

**What I've Tried:**
- [List attempts]

**Expected Behavior:**
[Describe what should happen]

**Actual Behavior:**
[Describe what's happening]

Please help me identify the root cause and suggest a fix."
            },

            new()
            {
                Id = "code-explain",
                Title = "Code Explainer",
                Category = "Coding",
                Tags = new List<string> { "explain", "learn", "understand" },
                Description = "Get clear explanations of complex code",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Please explain the following code in detail:

```
[PASTE CODE HERE]
```

Include:
1. **Overview**: What does this code do at a high level?
2. **Line-by-line breakdown**: Explain each significant section
3. **Key concepts**: What programming concepts are used?
4. **Flow**: How does data/control flow through the code?
5. **Use cases**: When would you use this pattern?"
            },
            new()
            {
                Id = "refactor",
                Title = "Code Refactoring",
                Category = "Coding",
                Tags = new List<string> { "refactor", "improve", "clean" },
                Description = "Get suggestions for improving code structure",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Please refactor the following code to improve:

- Readability and maintainability
- Performance where possible
- Adherence to best practices
- Removal of code smells

Original code:
```
[PASTE CODE HERE]
```

Language/Framework: [SPECIFY]

Please provide the refactored code with explanations for each change."
            },
            new()
            {
                Id = "unit-test",
                Title = "Unit Test Generator",
                Category = "Coding",
                Tags = new List<string> { "test", "unit-test", "testing" },
                Description = "Generate comprehensive unit tests",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Generate unit tests for the following code:

```
[PASTE CODE HERE]
```

Testing framework: [Jest/NUnit/PyTest/etc.]

Please include:
1. Tests for happy path scenarios
2. Edge case tests
3. Error handling tests
4. Mock setup where needed
5. Clear test names describing the scenario"
            },

            // === WRITING ===
            new()
            {
                Id = "blog-post",
                Title = "Blog Post Writer",
                Category = "Writing",
                Tags = new List<string> { "blog", "content", "article" },
                Description = "Create engaging blog posts on any topic",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Write a blog post about: [TOPIC]

**Target Audience:** [WHO]
**Tone:** [Professional/Casual/Technical/etc.]
**Length:** [Word count]

Structure:
1. Engaging hook/introduction
2. Main points with examples
3. Practical takeaways
4. Strong conclusion with CTA

Include relevant subheadings and make it SEO-friendly."
            },
            new()
            {
                Id = "email-professional",
                Title = "Professional Email",
                Category = "Writing",
                Tags = new List<string> { "email", "business", "communication" },
                Description = "Craft professional business emails",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Write a professional email for the following situation:

**Purpose:** [REQUEST/FOLLOW-UP/INTRODUCTION/etc.]
**Recipient:** [ROLE/RELATIONSHIP]
**Key Points:**
- [Point 1]
- [Point 2]

**Tone:** [Formal/Friendly Professional/etc.]
**Desired Action:** [What should the recipient do?]

Keep it concise, clear, and professional."
            },
            new()
            {
                Id = "documentation",
                Title = "Technical Documentation",
                Category = "Writing",
                Tags = new List<string> { "docs", "technical", "readme" },
                Description = "Create clear technical documentation",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Create technical documentation for:

**Project/Feature:** [NAME]
**Type:** [README/API Docs/User Guide/etc.]

Include:
1. Overview and purpose
2. Prerequisites/requirements
3. Installation/setup steps
4. Usage examples with code
5. Configuration options
6. Troubleshooting common issues
7. Contributing guidelines (if applicable)

Make it clear, well-organized, and beginner-friendly."
            },

            // === ANALYSIS ===
            new()
            {
                Id = "data-analysis",
                Title = "Data Analysis",
                Category = "Analysis",
                Tags = new List<string> { "data", "analysis", "insights" },
                Description = "Analyze data and extract insights",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Analyze the following data and provide insights:

**Data:**
```
[PASTE DATA HERE]
```

**Context:** [What does this data represent?]
**Questions to Answer:**
1. [Question 1]
2. [Question 2]

Please provide:
- Key findings and patterns
- Statistical observations
- Anomalies or outliers
- Actionable recommendations
- Visualizations suggestions"
            },
            new()
            {
                Id = "competitor-analysis",
                Title = "Competitor Analysis",
                Category = "Analysis",
                Tags = new List<string> { "business", "competitor", "market" },
                Description = "Analyze competitors and market position",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Conduct a competitor analysis for:

**My Product/Service:** [DESCRIPTION]
**Competitors:** [LIST COMPETITORS]
**Industry:** [INDUSTRY]

Analyze:
1. **Strengths & Weaknesses** of each competitor
2. **Pricing strategies**
3. **Unique value propositions**
4. **Market positioning**
5. **Opportunities** for differentiation
6. **Threats** to be aware of

Provide actionable recommendations for competitive advantage."
            },
            new()
            {
                Id = "swot-analysis",
                Title = "SWOT Analysis",
                Category = "Analysis",
                Tags = new List<string> { "swot", "strategy", "planning" },
                Description = "Generate comprehensive SWOT analysis",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Create a SWOT analysis for:

**Subject:** [COMPANY/PROJECT/IDEA]
**Context:** [BRIEF DESCRIPTION]
**Industry:** [INDUSTRY]

Provide detailed analysis of:
- **Strengths**: Internal advantages
- **Weaknesses**: Internal limitations
- **Opportunities**: External possibilities
- **Threats**: External challenges

Include specific examples and strategic recommendations."
            },

            // === CREATIVE ===
            new()
            {
                Id = "story-generator",
                Title = "Story Generator",
                Category = "Creative",
                Tags = new List<string> { "story", "creative", "fiction" },
                Description = "Generate creative stories and narratives",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Write a [SHORT STORY/CHAPTER/SCENE] with:

**Genre:** [Fantasy/Sci-Fi/Mystery/etc.]
**Setting:** [WHERE AND WHEN]
**Main Character:** [BRIEF DESCRIPTION]
**Conflict:** [CENTRAL PROBLEM]
**Tone:** [Dark/Humorous/Suspenseful/etc.]
**Length:** [WORD COUNT]

Include vivid descriptions, dialogue, and emotional depth."
            },
            new()
            {
                Id = "brainstorm",
                Title = "Brainstorming Session",
                Category = "Creative",
                Tags = new List<string> { "ideas", "brainstorm", "creative" },
                Description = "Generate creative ideas and solutions",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Help me brainstorm ideas for:

**Topic/Challenge:** [DESCRIBE]
**Context:** [BACKGROUND INFO]
**Constraints:** [ANY LIMITATIONS]
**Goal:** [WHAT SUCCESS LOOKS LIKE]

Please provide:
1. 10+ diverse ideas (conventional and unconventional)
2. Pros and cons for top 3 ideas
3. Unexpected combinations or approaches
4. Questions to explore further

Think outside the box!"
            },
            new()
            {
                Id = "social-media",
                Title = "Social Media Content",
                Category = "Creative",
                Tags = new List<string> { "social", "marketing", "content" },
                Description = "Create engaging social media posts",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Create social media content for:

**Platform:** [Twitter/LinkedIn/Instagram/etc.]
**Topic:** [SUBJECT]
**Goal:** [Engagement/Awareness/Sales/etc.]
**Brand Voice:** [Professional/Playful/Inspirational/etc.]
**Include:** [Hashtags/Emojis/CTA/etc.]

Generate [NUMBER] variations with different angles/hooks."
            },

            // === PRODUCTIVITY ===
            new()
            {
                Id = "meeting-summary",
                Title = "Meeting Summary",
                Category = "Productivity",
                Tags = new List<string> { "meeting", "summary", "notes" },
                Description = "Summarize meetings and extract action items",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Summarize the following meeting notes:

**Meeting Notes:**
```
[PASTE NOTES/TRANSCRIPT]
```

Provide:
1. **Executive Summary** (2-3 sentences)
2. **Key Discussion Points**
3. **Decisions Made**
4. **Action Items** (with owners and deadlines)
5. **Open Questions/Follow-ups**
6. **Next Steps**

Format for easy sharing with stakeholders."
            },
            new()
            {
                Id = "task-breakdown",
                Title = "Task Breakdown",
                Category = "Productivity",
                Tags = new List<string> { "tasks", "planning", "project" },
                Description = "Break down complex tasks into manageable steps",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Break down this task/project into actionable steps:

**Task:** [DESCRIBE THE TASK]
**Deadline:** [WHEN]
**Resources:** [AVAILABLE RESOURCES]
**Constraints:** [LIMITATIONS]

Provide:
1. Step-by-step breakdown
2. Time estimates for each step
3. Dependencies between steps
4. Potential blockers and mitigations
5. Milestones/checkpoints
6. Priority order"
            },
            new()
            {
                Id = "decision-matrix",
                Title = "Decision Matrix",
                Category = "Productivity",
                Tags = new List<string> { "decision", "analysis", "compare" },
                Description = "Create decision matrices for complex choices",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Help me make a decision between these options:

**Options:**
1. [Option 1]
2. [Option 2]
3. [Option 3]

**Important Criteria:**
- [Criterion 1]
- [Criterion 2]
- [Criterion 3]

**Context:** [SITUATION]

Create a weighted decision matrix and provide:
1. Scoring for each option
2. Pros and cons analysis
3. Risk assessment
4. Recommendation with reasoning"
            },

            // === LEARNING ===
            new()
            {
                Id = "concept-explain",
                Title = "Concept Explainer",
                Category = "Learning",
                Tags = new List<string> { "learn", "explain", "education" },
                Description = "Get clear explanations of complex concepts",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Explain [CONCEPT] to me.

**My Background:** [BEGINNER/INTERMEDIATE/ADVANCED]
**Context:** [WHY I'M LEARNING THIS]

Please include:
1. Simple definition
2. Real-world analogy
3. Key components/principles
4. Common misconceptions
5. Practical examples
6. How it connects to [RELATED TOPIC]
7. Resources for deeper learning"
            },
            new()
            {
                Id = "study-guide",
                Title = "Study Guide Creator",
                Category = "Learning",
                Tags = new List<string> { "study", "education", "exam" },
                Description = "Create comprehensive study guides",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Create a study guide for: [TOPIC/SUBJECT]

**Exam/Goal:** [WHAT I'M PREPARING FOR]
**Time Available:** [DURATION]
**Current Level:** [BEGINNER/INTERMEDIATE/ADVANCED]

Include:
1. Key concepts and definitions
2. Important formulas/rules
3. Common question types
4. Practice problems with solutions
5. Memory aids/mnemonics
6. Study schedule suggestion
7. Tips for exam day"
            },
            new()
            {
                Id = "eli5",
                Title = "Explain Like I'm 5",
                Category = "Learning",
                Tags = new List<string> { "simple", "explain", "beginner" },
                Description = "Get simple explanations of complex topics",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Explain [COMPLEX TOPIC] like I'm 5 years old.

Use:
- Simple words
- Fun analogies
- Relatable examples
- No jargon

Then provide a slightly more detailed explanation for someone with basic knowledge."
            },

            // === AI ASSISTANT ===
            new()
            {
                Id = "system-prompt",
                Title = "Custom AI Assistant",
                Category = "AI Assistant",
                Tags = new List<string> { "system", "persona", "assistant" },
                Description = "Create custom AI assistant personas",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"You are [ROLE/PERSONA], an expert in [DOMAIN].

**Your characteristics:**
- [Trait 1]
- [Trait 2]
- [Communication style]

**Your expertise includes:**
- [Area 1]
- [Area 2]

**When responding:**
1. [Guideline 1]
2. [Guideline 2]
3. [Guideline 3]

**You should NOT:**
- [Limitation 1]
- [Limitation 2]

Begin by introducing yourself briefly."
            },
            new()
            {
                Id = "prompt-improve",
                Title = "Prompt Improver",
                Category = "AI Assistant",
                Tags = new List<string> { "prompt", "improve", "optimize" },
                Description = "Improve and optimize your prompts",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Improve this prompt to get better AI responses:

**Original Prompt:**
```
[PASTE YOUR PROMPT]
```

**Goal:** [WHAT I WANT TO ACHIEVE]

Please:
1. Identify weaknesses in the original
2. Provide an improved version
3. Explain the improvements
4. Suggest variations for different use cases

Focus on clarity, specificity, and structure."
            },
            new()
            {
                Id = "chain-of-thought",
                Title = "Chain of Thought",
                Category = "AI Assistant",
                Tags = new List<string> { "reasoning", "logic", "step-by-step" },
                Description = "Get step-by-step reasoning for complex problems",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Solve this problem using step-by-step reasoning:

**Problem:**
[DESCRIBE THE PROBLEM]

Please:
1. Break down the problem into components
2. Think through each step explicitly
3. Show your reasoning at each stage
4. Consider alternative approaches
5. Verify your solution
6. Explain any assumptions made

Take your time and be thorough."
            },

            // === BUSINESS ===
            new()
            {
                Id = "business-plan",
                Title = "Business Plan Outline",
                Category = "Business",
                Tags = new List<string> { "business", "plan", "startup" },
                Description = "Create business plan outlines",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Create a business plan outline for:

**Business Idea:** [DESCRIPTION]
**Industry:** [INDUSTRY]
**Target Market:** [WHO]
**Stage:** [Idea/MVP/Growth/etc.]

Include sections for:
1. Executive Summary
2. Problem & Solution
3. Market Analysis
4. Business Model
5. Marketing Strategy
6. Operations Plan
7. Financial Projections
8. Team & Resources
9. Risk Analysis
10. Milestones & Timeline"
            },
            new()
            {
                Id = "pitch-deck",
                Title = "Pitch Deck Content",
                Category = "Business",
                Tags = new List<string> { "pitch", "investor", "presentation" },
                Description = "Create compelling pitch deck content",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Create pitch deck content for:

**Company:** [NAME]
**Product/Service:** [DESCRIPTION]
**Stage:** [Pre-seed/Seed/Series A/etc.]
**Ask:** [FUNDING AMOUNT]

Generate content for these slides:
1. Hook/Opening
2. Problem
3. Solution
4. Market Size (TAM/SAM/SOM)
5. Business Model
6. Traction
7. Competition
8. Team
9. Financials
10. The Ask

Keep each slide concise and impactful."
            },
            new()
            {
                Id = "user-persona",
                Title = "User Persona Creator",
                Category = "Business",
                Tags = new List<string> { "persona", "user", "marketing" },
                Description = "Create detailed user personas",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Create a detailed user persona for:

**Product/Service:** [DESCRIPTION]
**Target Segment:** [WHO]

Include:
1. **Demographics**: Name, age, location, job, income
2. **Background**: Education, career path, family
3. **Goals**: What they want to achieve
4. **Pain Points**: Frustrations and challenges
5. **Behaviors**: How they work, shop, consume media
6. **Motivations**: What drives their decisions
7. **Objections**: Why they might not buy
8. **Quote**: A statement that captures their mindset

Make it realistic and actionable for marketing."
            },

            // === COMMUNICATION ===
            new()
            {
                Id = "feedback-give",
                Title = "Constructive Feedback",
                Category = "Communication",
                Tags = new List<string> { "feedback", "review", "communication" },
                Description = "Give constructive feedback effectively",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Help me give constructive feedback on:

**Situation:** [WHAT HAPPENED]
**Recipient:** [ROLE/RELATIONSHIP]
**Goal:** [DESIRED OUTCOME]

Please draft feedback that:
1. Starts with something positive
2. Is specific and actionable
3. Focuses on behavior, not personality
4. Offers suggestions for improvement
5. Ends on an encouraging note

Tone: [Formal/Casual/Supportive/Direct]"
            },
            new()
            {
                Id = "difficult-conversation",
                Title = "Difficult Conversation Prep",
                Category = "Communication",
                Tags = new List<string> { "conversation", "conflict", "communication" },
                Description = "Prepare for difficult conversations",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Help me prepare for a difficult conversation:

**Situation:** [DESCRIBE]
**Other Person:** [ROLE/RELATIONSHIP]
**My Goal:** [DESIRED OUTCOME]
**Their Likely Perspective:** [WHAT THEY MIGHT THINK]
**Emotions Involved:** [FEELINGS]

Provide:
1. Opening statement
2. Key points to make
3. Anticipated objections and responses
4. Questions to ask
5. Phrases to avoid
6. De-escalation techniques
7. Ideal outcome and fallback positions"
            },
            new()
            {
                Id = "presentation-outline",
                Title = "Presentation Outline",
                Category = "Communication",
                Tags = new List<string> { "presentation", "speaking", "slides" },
                Description = "Create engaging presentation outlines",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Create a presentation outline for:

**Topic:** [SUBJECT]
**Audience:** [WHO]
**Duration:** [MINUTES]
**Goal:** [Inform/Persuade/Inspire/etc.]

Include:
1. Attention-grabbing opening
2. Clear structure with transitions
3. Key messages (max 3)
4. Supporting evidence/stories
5. Interactive elements
6. Memorable closing
7. Q&A preparation

Suggest visuals for each section."
            },

            // === RESEARCH ===
            new()
            {
                Id = "literature-review",
                Title = "Literature Review",
                Category = "Research",
                Tags = new List<string> { "research", "academic", "review" },
                Description = "Structure literature reviews",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Help me structure a literature review on:

**Topic:** [RESEARCH TOPIC]
**Field:** [DISCIPLINE]
**Scope:** [TIME PERIOD/GEOGRAPHIC/etc.]
**Purpose:** [THESIS/PAPER/GRANT/etc.]

Provide:
1. Key themes to organize around
2. Important authors/works to include
3. Gaps in current research
4. Methodological approaches used
5. Conflicting viewpoints
6. Synthesis framework
7. Transition suggestions between sections"
            },
            new()
            {
                Id = "research-questions",
                Title = "Research Question Generator",
                Category = "Research",
                Tags = new List<string> { "research", "questions", "academic" },
                Description = "Generate research questions",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Generate research questions for:

**Topic Area:** [BROAD TOPIC]
**Field:** [DISCIPLINE]
**Level:** [Undergraduate/Graduate/PhD/etc.]
**Type:** [Qualitative/Quantitative/Mixed]

Provide:
1. 5 broad research questions
2. 5 specific/narrow questions
3. Hypotheses for testable questions
4. Variables to consider
5. Potential methodologies
6. Feasibility considerations"
            },

            // === CAREER ===
            new()
            {
                Id = "resume-improve",
                Title = "Resume Improver",
                Category = "Career",
                Tags = new List<string> { "resume", "job", "career" },
                Description = "Improve resume bullet points",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Improve these resume bullet points:

**Current Role:** [JOB TITLE]
**Target Role:** [DESIRED POSITION]
**Industry:** [INDUSTRY]

**Current Bullets:**
- [Bullet 1]
- [Bullet 2]
- [Bullet 3]

Please:
1. Add quantifiable achievements
2. Use strong action verbs
3. Highlight relevant skills
4. Optimize for ATS keywords
5. Show impact, not just duties

Provide before/after comparisons."
            },
            new()
            {
                Id = "interview-prep",
                Title = "Interview Preparation",
                Category = "Career",
                Tags = new List<string> { "interview", "job", "career" },
                Description = "Prepare for job interviews",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Help me prepare for an interview:

**Position:** [JOB TITLE]
**Company:** [COMPANY NAME]
**Industry:** [INDUSTRY]
**Interview Type:** [Phone/Technical/Behavioral/etc.]

Provide:
1. 10 likely questions with sample answers
2. Questions I should ask them
3. Key points about my background to highlight
4. Potential weaknesses to address
5. Company research talking points
6. STAR method examples for behavioral questions"
            },
            new()
            {
                Id = "cover-letter",
                Title = "Cover Letter Writer",
                Category = "Career",
                Tags = new List<string> { "cover-letter", "job", "application" },
                Description = "Write compelling cover letters",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Write a cover letter for:

**Position:** [JOB TITLE]
**Company:** [COMPANY NAME]
**My Background:** [BRIEF SUMMARY]
**Key Qualifications:**
- [Qualification 1]
- [Qualification 2]

**Why This Company:** [REASON]
**Tone:** [Professional/Enthusiastic/etc.]

Make it:
- Concise (under 400 words)
- Specific to the role
- Showing personality
- With a strong opening and closing"
            },

            // === DATA SCIENCE ===
            new()
            {
                Id = "data-cleaning",
                Title = "Data Cleaning Guide",
                Category = "Data Science",
                Tags = new List<string> { "data", "cleaning", "preprocessing" },
                Description = "Get guidance on cleaning and preprocessing data",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Help me clean and preprocess this dataset:

**Data Description:** [DESCRIBE YOUR DATA]
**Data Format:** [CSV/JSON/SQL/etc.]
**Sample Data:**
```
[PASTE SAMPLE ROWS]
```

**Known Issues:**
- [Issue 1: e.g., missing values]
- [Issue 2: e.g., duplicates]

Please provide:
1. Data quality assessment
2. Cleaning steps with code examples
3. Handling missing values strategy
4. Outlier detection approach
5. Data type conversions needed
6. Validation checks to implement"
            },
            new()
            {
                Id = "data-visualization",
                Title = "Data Visualization Advisor",
                Category = "Data Science",
                Tags = new List<string> { "visualization", "charts", "graphs" },
                Description = "Get recommendations for visualizing your data",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Recommend visualizations for my data:

**Data Type:** [Time series/Categorical/Numerical/etc.]
**Variables:** [LIST KEY VARIABLES]
**Audience:** [Technical/Executive/General]
**Goal:** [Compare/Show trends/Distribution/Correlation]
**Tool:** [Python/R/Tableau/Excel/etc.]

Please suggest:
1. Best chart types for this data
2. Color scheme recommendations
3. Code examples for implementation
4. Accessibility considerations
5. Common pitfalls to avoid"
            },
            new()
            {
                Id = "statistical-analysis",
                Title = "Statistical Analysis Helper",
                Category = "Data Science",
                Tags = new List<string> { "statistics", "analysis", "hypothesis" },
                Description = "Get help with statistical analysis",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Help me with statistical analysis:

**Research Question:** [WHAT YOU WANT TO KNOW]
**Data Description:** [VARIABLES AND TYPES]
**Sample Size:** [N]
**Hypothesis:** [IF APPLICABLE]

Please provide:
1. Appropriate statistical test(s)
2. Assumptions to check
3. Code implementation (Python/R)
4. How to interpret results
5. Effect size considerations
6. Limitations of the analysis"
            },

            // === DEVOPS ===
            new()
            {
                Id = "dockerfile-generator",
                Title = "Dockerfile Generator",
                Category = "DevOps",
                Tags = new List<string> { "docker", "container", "deployment" },
                Description = "Generate optimized Dockerfiles",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Create a Dockerfile for:

**Application Type:** [Web app/API/Worker/etc.]
**Language/Framework:** [Node.js/Python/Go/etc.]
**Base Image Preference:** [Alpine/Debian/etc.]
**Requirements:**
- [Requirement 1]
- [Requirement 2]

Please provide:
1. Optimized multi-stage Dockerfile
2. .dockerignore file
3. Security best practices
4. Build and run commands
5. Environment variable handling
6. Health check configuration"
            },
            new()
            {
                Id = "kubernetes-config",
                Title = "Kubernetes Configuration",
                Category = "DevOps",
                Tags = new List<string> { "kubernetes", "k8s", "orchestration" },
                Description = "Generate Kubernetes manifests",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Create Kubernetes configuration for:

**Application:** [NAME AND DESCRIPTION]
**Replicas:** [NUMBER]
**Resources:** [CPU/Memory requirements]
**Exposure:** [Internal/External/LoadBalancer]
**Storage:** [Persistent volume needs]

Please generate:
1. Deployment manifest
2. Service manifest
3. ConfigMap/Secrets
4. Ingress (if needed)
5. Resource limits
6. Health probes
7. Scaling configuration"
            },
            new()
            {
                Id = "monitoring-setup",
                Title = "Monitoring Setup Guide",
                Category = "DevOps",
                Tags = new List<string> { "monitoring", "observability", "alerts" },
                Description = "Set up monitoring and alerting",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Help me set up monitoring for:

**System Type:** [Web app/Microservices/Database/etc.]
**Stack:** [Current tech stack]
**Metrics Needed:** [Performance/Errors/Business/etc.]
**Alert Requirements:** [What should trigger alerts]

Please provide:
1. Key metrics to track
2. Tool recommendations
3. Dashboard layout suggestions
4. Alert threshold guidelines
5. Log aggregation strategy
6. Incident response workflow"
            },

            // === DESIGN ===
            new()
            {
                Id = "ui-feedback",
                Title = "UI Design Feedback",
                Category = "Design",
                Tags = new List<string> { "ui", "design", "feedback" },
                Description = "Get feedback on UI designs",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Review this UI design:

**Screen/Component:** [DESCRIBE]
**Purpose:** [WHAT IT DOES]
**Target Users:** [WHO]
**Platform:** [Web/iOS/Android/Desktop]

Please evaluate:
1. Visual hierarchy
2. Color and contrast
3. Typography choices
4. Spacing and alignment
5. Interactive elements
6. Consistency with design systems
7. Specific improvement suggestions"
            },
            new()
            {
                Id = "accessibility-audit",
                Title = "Accessibility Audit",
                Category = "Design",
                Tags = new List<string> { "accessibility", "a11y", "wcag" },
                Description = "Audit designs for accessibility",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Audit this design/component for accessibility:

**Component:** [DESCRIBE]
**Current Implementation:** [HTML/DESCRIPTION]
**Target WCAG Level:** [A/AA/AAA]

Please check:
1. Color contrast ratios
2. Keyboard navigation
3. Screen reader compatibility
4. Focus indicators
5. Alternative text needs
6. Form accessibility
7. Motion/animation concerns
8. Specific WCAG violations and fixes"
            },
            new()
            {
                Id = "design-system",
                Title = "Design System Component",
                Category = "Design",
                Tags = new List<string> { "design-system", "components", "tokens" },
                Description = "Create design system components",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Create a design system component for:

**Component:** [Button/Card/Modal/etc.]
**Variants:** [Primary/Secondary/etc.]
**States:** [Default/Hover/Active/Disabled]
**Framework:** [React/Vue/CSS/etc.]

Please provide:
1. Component specifications
2. Design tokens (colors, spacing, typography)
3. Variant definitions
4. State behaviors
5. Usage guidelines
6. Code implementation
7. Documentation template"
            },

            // === EDUCATION ===
            new()
            {
                Id = "lesson-plan",
                Title = "Lesson Plan Creator",
                Category = "Education",
                Tags = new List<string> { "teaching", "lesson", "curriculum" },
                Description = "Create structured lesson plans",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Create a lesson plan for:

**Subject:** [TOPIC]
**Grade/Level:** [AGE/SKILL LEVEL]
**Duration:** [TIME]
**Learning Objectives:** [WHAT STUDENTS WILL LEARN]
**Prerequisites:** [PRIOR KNOWLEDGE NEEDED]

Please include:
1. Introduction/Hook (5 min)
2. Main instruction
3. Guided practice
4. Independent practice
5. Assessment methods
6. Differentiation strategies
7. Materials needed
8. Homework/Extension activities"
            },
            new()
            {
                Id = "quiz-generator",
                Title = "Quiz Generator",
                Category = "Education",
                Tags = new List<string> { "quiz", "assessment", "questions" },
                Description = "Generate quizzes and assessments",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Generate a quiz on:

**Topic:** [SUBJECT]
**Level:** [Beginner/Intermediate/Advanced]
**Question Types:** [Multiple choice/Short answer/Essay]
**Number of Questions:** [COUNT]
**Time Limit:** [MINUTES]

Please create:
1. Questions with varying difficulty
2. Answer key with explanations
3. Point values
4. Common misconceptions to test
5. Bloom's taxonomy alignment
6. Grading rubric (for open-ended)"
            },

            // === SALES & MARKETING ===
            new()
            {
                Id = "email-campaign",
                Title = "Email Campaign Writer",
                Category = "Sales & Marketing",
                Tags = new List<string> { "email", "marketing", "campaign" },
                Description = "Create email marketing campaigns",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Create an email campaign for:

**Product/Service:** [DESCRIPTION]
**Campaign Goal:** [Awareness/Conversion/Retention]
**Target Audience:** [WHO]
**Email Sequence:** [NUMBER OF EMAILS]
**Tone:** [Professional/Casual/Urgent]

Please provide:
1. Subject lines (A/B test options)
2. Preview text
3. Email body copy
4. Call-to-action buttons
5. Personalization tokens
6. Send timing recommendations
7. Segmentation suggestions"
            },
            new()
            {
                Id = "seo-content",
                Title = "SEO Content Optimizer",
                Category = "Sales & Marketing",
                Tags = new List<string> { "seo", "content", "keywords" },
                Description = "Optimize content for search engines",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Optimize this content for SEO:

**Target Keyword:** [PRIMARY KEYWORD]
**Secondary Keywords:** [LIST]
**Content Type:** [Blog/Landing page/Product page]
**Current Content:**
```
[PASTE CONTENT]
```

Please provide:
1. Title tag optimization
2. Meta description
3. Header structure (H1, H2, H3)
4. Keyword placement suggestions
5. Internal linking opportunities
6. Content gaps to fill
7. Readability improvements"
            },

            // === CUSTOMER SUPPORT ===
            new()
            {
                Id = "support-response",
                Title = "Support Response Templates",
                Category = "Customer Support",
                Tags = new List<string> { "support", "customer", "response" },
                Description = "Create customer support response templates",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Create a support response for:

**Issue Type:** [Complaint/Question/Bug report/Feature request]
**Customer Sentiment:** [Angry/Frustrated/Neutral/Happy]
**Issue Description:** [DESCRIBE THE PROBLEM]
**Resolution Available:** [Yes/No/Partial]

Please provide:
1. Empathetic opening
2. Issue acknowledgment
3. Solution/Next steps
4. Timeline expectations
5. Closing with care
6. Follow-up actions
7. Escalation criteria (if needed)"
            },
            new()
            {
                Id = "faq-generator",
                Title = "FAQ Generator",
                Category = "Customer Support",
                Tags = new List<string> { "faq", "documentation", "help" },
                Description = "Generate FAQ documentation",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Generate FAQs for:

**Product/Service:** [DESCRIPTION]
**Target Audience:** [New users/All users/Specific segment]
**Topics to Cover:** [LIST MAIN AREAS]
**Tone:** [Formal/Friendly/Technical]

Please create:
1. 10-15 common questions
2. Clear, concise answers
3. Related questions links
4. Troubleshooting steps where needed
5. Contact escalation paths
6. Category organization"
            },

            // === PERSONAL DEVELOPMENT ===
            new()
            {
                Id = "goal-setting",
                Title = "Goal Setting Framework",
                Category = "Personal Development",
                Tags = new List<string> { "goals", "planning", "productivity" },
                Description = "Create SMART goals and action plans",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Help me set and plan for this goal:

**Goal Area:** [Career/Health/Finance/Learning/etc.]
**Desired Outcome:** [WHAT YOU WANT TO ACHIEVE]
**Timeline:** [WHEN]
**Current Situation:** [WHERE YOU ARE NOW]
**Resources Available:** [TIME/MONEY/SUPPORT]

Please provide:
1. SMART goal formulation
2. Milestone breakdown
3. Weekly action items
4. Potential obstacles and solutions
5. Accountability measures
6. Progress tracking method
7. Reward system suggestions"
            },
            new()
            {
                Id = "habit-tracker",
                Title = "Habit Building Plan",
                Category = "Personal Development",
                Tags = new List<string> { "habits", "routine", "self-improvement" },
                Description = "Create habit building strategies",
                IsOfficial = true,
                IsCommunity = false,
                Content = @"Help me build this habit:

**Habit:** [WHAT YOU WANT TO DO]
**Frequency:** [Daily/Weekly/etc.]
**Current Routine:** [EXISTING HABITS]
**Motivation:** [WHY THIS MATTERS]
**Past Attempts:** [WHAT DIDN'T WORK]

Please provide:
1. Habit stacking strategy
2. Implementation intentions
3. Environment design tips
4. Cue-routine-reward loop
5. Tracking method
6. Recovery plan for missed days
7. 30-day progression plan"
            }
        };
        
        // Mark all built-in templates as official
        foreach (var template in templates)
        {
            template.IsOfficial = true;
            template.IsCommunity = false;
        }
        
        return templates;
    }
}
