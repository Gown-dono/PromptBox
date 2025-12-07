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

## Keyboard Shortcuts

Currently, the application uses standard Windows shortcuts:
- `Ctrl+C`: Copy (in text fields)
- `Ctrl+V`: Paste (in text fields)
- `Ctrl+A`: Select all (in text fields)
- `Tab`: Navigate between fields

## Future Enhancement Ideas

Potential features for future versions:
- Keyboard shortcuts for common actions
- Version history for prompts
- Favorites/starred prompts
- Community prompt library (online)
- Prompt sharing (export as link)
- Cloud sync option
- AI-powered prompt suggestions
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
