# PromptBox Features Documentation

## Complete Feature List

### 1. Prompt Management

#### Create Prompts
- Click "New Prompt" button in the left sidebar
- Fill in title, category, and tags
- Write content in the Markdown editor
- Real-time preview on the right side
- Click "Save" to store

#### Edit Prompts
- Select a prompt from the middle list
- Modify any field (title, category, tags, content)
- Changes are reflected in real-time preview
- Click "Save" to update

#### Delete Prompts
- Select a prompt
- Click "Delete" button
- Confirm deletion in the dialog
- Prompt is permanently removed from database

#### View Prompts
- All prompts displayed in the middle panel
- Shows title, category, tags, and last updated date
- Click any prompt to view/edit details

### 2. Organization System

#### Categories
- Assign one category per prompt
- View all categories in the left sidebar
- Click a category to filter prompts
- Categories are automatically extracted from all prompts

#### Tags
- Add multiple tags per prompt (comma-separated)
- Visual tag display with colored badges
- Click a tag in the sidebar to filter
- Tags are automatically collected from all prompts

### 3. Search & Filter

#### Search Functionality
- Real-time search as you type
- Searches across:
  - Prompt titles
  - Categories
  - Tags
  - Content text
- Case-insensitive matching
- Instant results

#### Filtering
- Filter by category (click in sidebar)
- Filter by tag (click in sidebar)
- Combine search with filters
- Clear filters by clicking again

### 4. Markdown Support

#### Editor Features
- Full Markdown syntax support
- Monospace font (Consolas) for editing
- Multi-line text input
- Syntax highlighting in preview

#### Preview Features
- Real-time rendering
- Split-pane view (editor left, preview right)
- Supports:
  - Headers (# ## ###)
  - Bold (**text**)
  - Italic (*text*)
  - Lists (ordered and unordered)
  - Code blocks (```)
  - Links
  - Blockquotes

### 5. Import/Export

#### Export Single Prompt
**As Markdown (.md)**
- Includes title, category, tags, date
- Formatted with Markdown headers
- Content preserved with formatting

**As Text (.txt)**
- Plain text content only
- No metadata
- Direct copy of prompt content

#### Export All Prompts
**As JSON (.json)**
- Complete backup of all prompts
- Includes all metadata
- Can be imported later
- Human-readable format

#### Import Prompts
**From JSON (.json)**
- Import previously exported prompts
- Creates new entries (doesn't overwrite)
- Preserves all metadata
- Bulk import support

#### Export with Version History
**As JSON (.json)**
- Complete backup of all prompts with their version history
- Includes all prompt metadata and all saved versions
- Preserves version numbers, timestamps, and change history
- Ideal for full backup/restore scenarios

#### Import with Version History
**From JSON (.json)**
- Import prompts along with their complete version history
- Automatically remaps version references to new prompt IDs
- Preserves all historical versions for each prompt
- Full restore capability

#### Export Workflows
**As JSON (.json)**
- Export all custom (user-created) workflows
- Includes workflow name, description, category, and all steps
- Step templates and variable configurations preserved
- Share workflows between installations

#### Import Workflows
**From JSON (.json)**
- Import previously exported custom workflows
- Creates new workflow entries (doesn't overwrite)
- Preserves all step configurations and templates
- Bulk import support for multiple workflows

### 6. Clipboard Integration

#### Copy to Clipboard
- One-click copy button
- Copies current prompt content
- Confirmation message
- Ready to paste into AI tools

### 7. Theme System

#### Light Mode
- Clean, bright interface
- Easy on the eyes in daylight
- Professional appearance
- Material Design colors

#### Dark Mode
- Reduced eye strain
- Better for low-light environments
- Modern aesthetic
- Consistent color scheme

#### Theme Toggle
- Click theme icon in top bar
- Instant theme switching
- Preference saved automatically
- Persists across sessions

### 8. User Interface

#### Layout
- **Left Sidebar**: Navigation, categories, tags, settings
- **Middle Panel**: Search bar and prompt list
- **Right Panel**: Editor and preview

#### Settings Dialog
- Centralized settings management
- **Import/Export Tab**: All import/export operations in one place
  - Export/Import prompts (JSON)
  - Export/Import prompts with version history
  - Export/Import custom workflows
- **About Tab**: Application information

#### Material Design
- Modern, clean aesthetic
- Consistent component styling
- Smooth animations
- Professional appearance

#### Responsive Elements
- Resizable panels
- Scrollable lists
- Adaptive layouts
- Grid splitter for editor/preview

### 9. Data Storage

#### LiteDB Database
- Lightweight NoSQL database
- File-based storage
- No server required
- Fast read/write operations

#### Data Location
- Stored in `Data/promptbox.db`
- Created automatically on first run
- Portable (can be copied/backed up)
- Single file for all data

#### Collections
- **prompts**: All prompt entries
- Automatic indexing
- Efficient queries

### 10. Performance

#### Fast Search
- Instant results
- Efficient filtering
- No lag on large datasets

#### Optimized Loading
- Async database operations
- Non-blocking UI
- Smooth scrolling

#### Memory Management
- Observable collections
- Efficient data binding
- Minimal memory footprint

### 11. Prompt Library

#### Built-in Templates
- 30+ pre-built prompt templates
- Organized by category
- Ready to use or customize

#### Categories Available
- **Coding**: Code review, debugging, refactoring, unit tests
- **Writing**: Blog posts, emails, documentation
- **Analysis**: Data analysis, SWOT, competitor analysis
- **Creative**: Story generation, brainstorming, social media
- **Productivity**: Meeting summaries, task breakdown, decisions
- **Learning**: Concept explanations, study guides, ELI5
- **AI Assistant**: System prompts, prompt improvement, chain of thought
- **Business**: Business plans, pitch decks, user personas
- **Communication**: Feedback, difficult conversations, presentations
- **Research**: Literature reviews, research questions
- **Career**: Resume improvement, interview prep, cover letters

#### Library Browser
- Click "Browse Library" in the left sidebar
- Filter by category
- Search across all templates
- Preview template content
- Import to editor with one click
- Customize and save as your own prompt

### 12. Version History (Git-like)

#### Automatic Versioning
- Previous versions saved automatically on each edit
- Up to 50 versions stored per prompt
- Versions include all metadata (title, category, tags, content)

#### Version Browser
- Click "History" button to view all versions
- See timestamp for each version
- Visual diff showing changes between versions
- Color-coded: green for additions, red for deletions

#### Restore Previous Versions
- One-click restore to any previous version
- Confirmation dialog before restore
- Restored content loaded into editor for review
- Save to apply the restored version

#### Version Cleanup
- Versions automatically deleted when prompt is deleted
- Oldest versions pruned when limit reached
- Efficient storage using LiteDB

## Keyboard Shortcuts

Currently, the application uses standard Windows shortcuts:
- `Ctrl+C`: Copy (in text fields)
- `Ctrl+V`: Paste (in text fields)
- `Ctrl+A`: Select all (in text fields)
- `Tab`: Navigate between fields

### 13. AI Prompt Builder

#### Multi-Model AI Support
- **OpenAI**: GPT-4o, GPT-4o Mini, GPT-4 Turbo, GPT-3.5 Turbo
- **Anthropic**: Claude Sonnet 4, Claude 3.5 Sonnet, Claude 3.5 Haiku
- **Google**: Gemini 2.0 Flash, Gemini 1.5 Pro, Gemini 1.5 Flash
- **Mistral AI**: Mistral Large, Mistral Small
- **Groq**: Llama 3.3 70B, Mixtral 8x7B (ultra-fast inference)

#### Secure API Key Management
- Windows DPAPI encryption for secure storage
- Per-provider API key configuration
- Easy add/delete API keys
- Keys never stored in plain text
- Visual status indicators for configured providers

#### AI Enhancement Tools
- **Improve Clarity**: Make prompts more specific and unambiguous
- **Add Detail**: Enhance prompts with context and constraints
- **Make Concise**: Remove redundancy while keeping essentials
- **Professional Tone**: Rewrite in formal, professional language
- **Add Structure**: Organize with sections and numbered steps
- **Generate Variations**: Create multiple alternative versions

#### Prompt Quality Analyzer
- AI-powered quality scoring (0-100)
- Download rating
- Strength identification
- Improvement suggestions
- Actionable feedback

#### Variable Templates
- Insert reusable variables: `{{topic}}`, `{{context}}`, `{{format}}`
- Quick-insert buttons for common variables
- Support for: topic, context, format, tone, audience, length

#### Context Injection
Click the "Context" button to open the Context Injection dialog. Automatically inject files, folders, clipboard content, and notes into your prompts:

**Add Files**
- Select one or multiple files to include
- File content is automatically read and formatted
- Supports all text-based files (code, config, docs)
- Shows file size for reference

**Add Folder**
- Select a folder to list all files
- Automatically excludes bin, obj, .git, node_modules
- Shows file structure with relative paths
- Limited to 500 files for performance

**From Clipboard**
- One-click to add clipboard text content
- Perfect for pasting code snippets or error messages
- Shows character count

**Add Notes**
- Add custom notes or additional context
- Multi-line text support
- Great for explaining requirements or constraints

**How It Works**
- Click "Context" button to open the context management dialog
- Add/remove context items in the dialog
- Badge on button shows count of context items
- When you click "Test Prompt", all context is automatically appended
- Context is formatted with clear sections (File, Folder, Clipboard, Note)

**Example Use Case**
"Analyze this C# project" prompt + Add Folder → The file list is automatically included in the prompt sent to the AI.

#### Real-time AI Testing
- Test prompts directly with selected AI model
- Streaming responses for immediate feedback
- Adjustable temperature control
- Token usage tracking
- Stop generation capability

#### Innovative Features
- **Response-to-Prompt**: Use AI response as new prompt input
- **Prompt Variations**: Generate 3 alternative versions instantly
- **Model Switching**: Compare responses across different AI models
- **Copy Response**: One-click copy AI responses
- **AI Smart Suggestions**: AI-generated improved prompt versions
- **Quick Start Templates**: 10 pre-built templates for common tasks

### 14. AI Settings

#### Provider Configuration
- Visual provider cards with status
- Easy API key entry with password masking
- One-click save and delete
- Provider descriptions and model info

### 15. Quick Start Templates

#### Built-in Templates
- **Code Review**: Comprehensive code analysis template
- **Explain Concept**: Learning-focused explanation template
- **Debug Issue**: Structured debugging assistance
- **Write Documentation**: Technical docs template
- **Brainstorm Ideas**: Creative ideation template
- **Write Email**: Professional communication template
- **Create Test Cases**: QA testing template
- **Refactor Code**: Code improvement template
- **API Design**: REST API design template
- **Data Analysis**: Analytics and insights template

### 16. AI Smart Suggestions

#### AI-Powered Prompt Improvements
- Click "Get Suggestions" to generate AI-powered improvements
- Generates 3-5 complete improved prompt versions
- Each suggestion improves clarity, specificity, or structure
- One-click to apply any suggestion
- Uses selected AI model for intelligent analysis

### 17. Prompt Workflows (Multi-Step Pipelines)

#### Overview
Run complete multi-step prompt workflows with a single click. Similar to ChatGPT Agents, workflows chain multiple prompts together where each step's output feeds into the next step.

#### Built-in Workflows

**Code Development Pipeline** (4 steps)
1. Problem Analysis - Break down requirements into components
2. Code Generation - Generate implementation based on analysis
3. Test Case Generation - Create comprehensive test cases
4. Code Refactoring - Optimize and improve the generated code

**Code Review Pipeline** (4 steps)
1. Security Analysis - Identify vulnerabilities and risks
2. Performance Analysis - Analyze time/space complexity
3. Best Practices Review - Check SOLID, DRY, naming conventions
4. Summary & Recommendations - Prioritized action items

**Documentation Generator** (4 steps)
1. Code Analysis - Understand structure and functionality
2. API Documentation - Generate detailed API docs
3. README Generation - Create comprehensive README
4. Usage Examples - Practical code examples

**Bug Fix Pipeline** (4 steps)
1. Bug Diagnosis - Root cause analysis
2. Fix Implementation - Generate the fix
3. Regression Tests - Prevent future regressions
4. Fix Documentation - Document the fix

**Feature Design Pipeline** (4 steps)
1. Requirements Analysis - Formalize user stories and acceptance criteria
2. Architecture Design - High-level design and data models
3. API Design - Endpoints, schemas, authentication
4. Implementation Plan - Task breakdown and sprint planning

**Code Migration Pipeline** (4 steps)
1. Source Analysis - Analyze code structure and dependencies
2. Migration Strategy - Plan approach and identify equivalents
3. Code Conversion - Convert to target language/framework
4. Validation & Testing - Create equivalence tests and checklist

**Content Creation Pipeline** (4 steps)
1. Research & Analysis - Research topic and gather key points
2. Content Outline - Create detailed structure
3. First Draft - Write complete draft
4. Polish & Optimize - Edit and optimize for publication

**Data Analysis Pipeline** (4 steps)
1. Data Exploration - Understand structure and quality
2. Analysis Plan - Design analytical approach
3. Analysis & Insights - Extract findings and patterns
4. Report & Recommendations - Create actionable report

**Learning Path Generator** (4 steps)
1. Goal Assessment - Understand goals and current level
2. Curriculum Design - Structure the learning modules
3. Resource Compilation - Gather learning materials
4. Study Plan & Milestones - Create actionable schedule

**Project Planning Pipeline** (4 steps)
1. Scope Definition - Define objectives and deliverables
2. Work Breakdown - Break into tasks with estimates
3. Timeline & Schedule - Create project timeline
4. Risk & Communication Plan - Identify risks and plan communication

#### Workflow Features
- **Visual Progress Tracking**: See each step's status (pending, running, completed, failed)
- **Progress Bar**: Overall workflow completion percentage
- **Tabbed Results**: View output from each step in separate tabs
- **Variable Substitution**: Steps can reference previous outputs using `{{previous_output}}`, `{{step1}}`, etc.
- **Cancellation Support**: Stop workflow execution at any time
- **Error Handling**: Workflow stops gracefully on step failure
- **Copy All Results**: Export complete workflow output
- **Use as Prompt**: Load final result into the main editor

#### Custom Workflows
Users can create, edit, save, and delete their own custom workflows:
- **Create**: Click the "+" button to open the workflow editor
- **Edit**: Select a custom workflow and click the pencil icon to modify it
- **Delete**: Select a custom workflow and click the trash icon to remove it
- **Reorder Steps**: Use up/down arrows to change step order
- **Variable Support**: Use `{{input}}`, `{{previous_output}}`, `{{step1}}`, etc. in templates
- Custom workflows are stored in the local database and persist across sessions

#### Visual Workflow Designer
A powerful drag-and-drop visual designer for creating complex workflows with conditional logic:

**Node Types**
- **Standard**: Regular prompt execution step
- **Conditional**: Branch based on output evaluation (if/else logic)
- **Loop**: Repeat execution until exit condition is met
- **Parallel**:

**Visual Canvas Features**
- **Drag & Drop**: Drag nodes to position them on the canvas
- **Zoom**: 50%-200% zoom with slider or Ctrl+Mouse Wheel
- **Pan**: Middle mouse button or Ctrl+Drag to pan the canvas
- **Snap to Grid**: Optional 16px grid snapping for alignment
- **Auto Layout**: Automatically arrange nodes in hierarchical layout
- **Minimap**: Overview of entire workflow with viewport indicator
- **Connection Drawing**: Click output connector, then click target node to connect

**Keyboard Shortcuts**
- `Ctrl+N`: Add new standard step
- `Ctrl+D`: Duplicate selected node
- `Ctrl+C`: Copy selected node
- `Ctrl+V`: Paste copied node
- `Ctrl+Z`: Undo last action
- `Ctrl+Y`: Redo last undone action
- `Ctrl+L`: Auto layout nodes
- `Delete`: Delete selected node
- `Escape`: Cancel connection mode

**Undo/Redo System**
- Up to 50 undo steps
- Full state restoration
- Visual indicator showing available undo/redo actions

**Export Options**
- **Export as PNG**: Save workflow diagram as image
- **Export as JSON**: Save workflow with all settings for sharing
- **Import from JSON**: Load workflow from JSON file

**Conditional Logic**
- **Condition Types**: Output Contains, Output Matches, Regex Match, Success
- **Comparison Operators**: Equals, Not Equals, Contains, Greater Than, Less Than
- **Multiple Branches**: Create multiple conditional paths from a single node
- **Branch Labels**: Name each branch for clarity

**Loop Configuration**
- **Max Iterations**: Set maximum loop count (default 10)
- **Loop Variable**: Access iteration count in prompts
- **Exit Condition**: Define when to exit the loop
- **Exit Condition Types**: Output Contains, Output Matches, Regex, Success

**Error Handling**
- **Max Retries**: Retry failed steps (0-5 attempts)
- **Retry Delay**: Configurable delay between retries (ms)
- **Exponential Backoff**: Increase delay with each retry
- **Continue on Error**: Option to continue workflow despite failures
- **Fallback Steps**: Redirect to alternative step on failure

**Properties Panel**
- Edit step name, description, and prompt template
- Configure step type (Standard, Conditional, Loop)
- Set output variable name for referencing in later steps
- Configure loop settings and exit conditions
- Configure error handling and retry logic
- Mark steps as Start or End steps

**Workflow Templates**
- **Code Review with Conditional Severity**: Branches based on issue severity
- **Iterative Content Refinement**: Loops until quality threshold met
- **Error-Resilient Processing**: Automatic retry with exponential backoff

**Visual Execution View**
When running workflows, see real-time visual execution:
- Mini workflow diagram showing all nodes
- Color-coded status (gray=pending, orange=running, green=success, red=failed)
- Animated transitions between steps
- Loop iteration counters displayed on loop nodes

**Validation**
- Validates workflow structure before saving
- Checks for start step presence
- Detects unreachable steps
- Validates conditional branches have targets
- Ensures loop steps have exit conditions

#### How to Use
1. Click "Workflows" button in the left sidebar
2. Select a workflow from the list (or create a custom one)
3. Choose your AI model
4. Enter your input (code, requirements, bug report, etc.)
5. Click "Run Workflow"
6. Watch as each step executes and view results in real-time

### 18. Batch Processing

#### Overview
Execute multiple prompts against multiple AI models in a single batch operation. Perfect for comparing model responses, bulk processing, or testing prompts across different providers.

#### Features
- **Multi-Prompt Selection**: Select any number of prompts from your library
- **Multi-Model Selection**: Choose multiple AI models to test against
- **Real-time Progress**: Visual progress bar and status updates
- **Live Statistics**: Track success/failure counts and token usage
- **Pause/Resume**: Pause batch execution and resume later
- **Cancel Support**: Stop batch processing at any time
- **Result Export**: Export results to CSV or JSON format

#### How to Use
1. Click "Batch Processing" button in the left sidebar
2. Select prompts from the left panel (use checkboxes, Select All, or search)
3. Select AI models from the middle panel
4. Configure settings (temperature, max tokens)
5. Optionally name your batch job
6. Click "Start Batch"
7. Monitor progress in real-time
8. View results in the data grid
9. Double-click any result to view full response
10. Export results when complete

#### Statistics Tracked
- Total executions
- Successful completions
- Failed executions
- Total tokens used

#### Export Options
- **CSV**: Spreadsheet-compatible format with truncated responses
- **JSON**: Full data export with complete responses

### 19. Prompt Testing & A/B Comparison

#### Overview
Comprehensive testing framework for evaluating prompt quality and comparing prompt variations. Create test suites with evaluation criteria, run tests against multiple AI models, and perform A/B comparisons to find the best performing prompts.

#### Test Suite Features
- **Create Test Suites**: Define tests for specific prompts with multiple test cases
- **Test Cases**: Each test case includes:
  - Name and input text
  - Expected keywords (must be present in output)
  - Forbidden content (must not appear in output)
  - Expected output pattern (regex matching)
  - Minimum quality score threshold
- **Evaluation Criteria**: Configure what to check:
  - Keyword presence validation
  - Pattern matching (regex)
  - Quality score threshold
  - Token usage limits
  - Response time limits

#### Quality Metrics
- **Quality Score** (0-100): Overall prompt output quality
- **Clarity Score**: How clear and unambiguous the output is
- **Specificity Score**: How specific and detailed the output is
- **Effectiveness Score**: Based on keyword matching and criteria fulfillment
- **Pass/Fail Status**: Determined by evaluation criteria

#### A/B Comparison Testing
- **Create Variations**: Define multiple versions of a prompt
- **Side-by-Side Comparison**: Test all variations against the same input
- **Automatic Ranking**: Results ranked by quality score
- **Winner Analysis**: Identifies best performing variation with reasoning
- **Export Reports**: Generate Markdown comparison reports

#### How to Use
1. Click "Prompt Testing" button in the left sidebar
2. Create a new test by clicking "New" and selecting a prompt
3. Add test cases with expected outcomes
4. Configure evaluation criteria (keywords, quality thresholds, etc.)
5. Select AI models to test against
6. Click "Run Tests" to execute
7. View results with pass/fail status and detailed metrics
8. For A/B testing, add prompt variations and click "Run Comparison"
9. Export results as CSV, JSON, or Markdown report

#### Statistics Dashboard
- Total tests executed
- Pass/fail counts and rate
- Average quality scores
- Token usage tracking
- Duration metrics

#### Export Options
- **CSV**: Test results with all metrics
- **JSON**: Full test data export
- **Markdown Report**: A/B comparison reports with winner analysis

### 20. Prompt Comparison & Benchmarking Tool

#### Overview
A standalone tool for comparing 2-4 prompt variations side-by-side across multiple AI models. Perfect for quick A/B testing, prompt optimization, and finding the best performing prompt for your use case without the overhead of setting up full test suites.

#### Key Features
- **Multi-Variation Comparison**: Compare 2-4 prompt variations simultaneously
- **Multi-Model Testing**: Test against multiple AI models in one session
- **Shared Input**: All variations tested against the same input for fair comparison
- **Quality Metrics**: Automatic scoring for quality, clarity, and specificity
- **Automatic Ranking**: Results ranked by quality score with medal icons (🥇🥈🥉)
- **Side-by-Side View**: Visual comparison of outputs across variations and models
- **Diff View**: Line-by-line diff highlighting between any two variations
- **Winner Selection**: Save the best performing variation as a new prompt
- **Session History**: View and manage past comparison sessions
- **Export Reports**: Generate detailed Markdown comparison reports

#### How to Use
1. Click "Compare Prompts" button in the left sidebar
2. Add 2-4 prompt variations:
   - **From Prompt**: Select from your existing prompts
   - **Add Manual**: Enter custom prompt text
3. Enter shared input text that all variations will be tested against
4. Select one or more AI models to test with
5. Configure settings (name, description, temperature, max tokens)
6. Click "Start Comparison"
7. View results in three tabs:
   - **Results**: DataGrid with rankings, scores, and metrics
   - **Side-by-Side**: Visual grid comparing outputs
   - **Diff View**: Line-by-line comparison between variations
8. Export report or save winner as new prompt

#### Metrics Tracked
- **Quality Score**: Overall output quality (0-100)
- **Clarity Score**: How clear and unambiguous the output is
- **Specificity Score**: How specific and detailed the output is
- **Token Usage**: Tokens consumed per variation/model
- **Duration**: Response time for each execution
- **Success/Failure**: Track which executions succeeded

#### Export Report Contents
- Comparison metadata (name, date, description)
- Shared input used for testing
- All variations tested with their prompts
- Models tested
- Results summary table with rankings
- Winner analysis with scores
- Detailed outputs for each variation/model combination
- Recommendations based on metrics

#### Comparison History
- View all past comparison sessions
- See session name, date, and winner
- Delete old sessions
- Quick reference for previous findings

## Future Enhancement Ideas

Potential features for future versions:
- Keyboard shortcuts for common actions
- Favorites/starred prompts
- Community prompt library (online)
- Version comparison between any two versions
- Prompt sharing (export as link)
- Cloud sync option
- Prompt statistics and analytics
- Custom themes
- Plugin system

## Technical Features

### Architecture
- MVVM pattern
- Dependency injection
- Service-based design
- Clean separation of concerns

### Code Quality
- XML documentation
- Meaningful naming
- Async/await best practices
- Error handling

### Extensibility
- Interface-based services
- Easy to add new features
- Modular design
- Testable code

---

For usage instructions, see README.md
For build instructions, see BUILD_INSTRUCTIONS.md
