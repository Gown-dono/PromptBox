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
- **Left Sidebar**: Navigation, categories, tags, export options
- **Middle Panel**: Search bar and prompt list
- **Right Panel**: Editor and preview

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
- Clarity and specificity ratings
- Strength identification
- Improvement suggestions
- Actionable feedback

#### Variable Templates
- Insert reusable variables: `{{topic}}`, `{{context}}`, `{{format}}`
- Quick-insert buttons for common variables
- Support for: topic, context, format, tone, audience, length

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
