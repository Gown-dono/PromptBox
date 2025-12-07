# Quick Start Guide - PromptBox

## Running the Application

### Option 1: Using .NET CLI
```bash
dotnet run --project PromptBox/PromptBox.csproj
```

### Option 2: Using Visual Studio
1. Open `PromptBox.sln` in Visual Studio 2022
2. Press `F5` to run in Debug mode
3. Or press `Ctrl+F5` to run without debugging

### Option 3: Run the Compiled Executable
```bash
cd PromptBox/bin/Debug/net8.0-windows
./PromptBox.exe
```

## First Steps

### 1. Create Your First Prompt
1. Click **"New Prompt"** button in the left sidebar
2. Enter a title (e.g., "ChatGPT Code Review Prompt")
3. Add a category (e.g., "Development")
4. Add tags separated by commas (e.g., "code, review, quality")
5. Write your prompt in the left editor:
   ```markdown
   # Code Review Prompt
   
   Please review the following code for:
   - **Best practices**
   - **Performance issues**
   - **Security vulnerabilities**
   - **Code readability**
   
   Provide specific suggestions for improvement.
   ```
6. See the preview render in real-time on the right
7. Click **"Save"**

### 2. Search and Filter
- Type in the search bar to find prompts instantly
- Click a category in the sidebar to filter by category
- Click a tag to filter by that specific tag

### 3. Use a Prompt
1. Select a prompt from the list
2. Click **"Copy to Clipboard"**
3. Paste into ChatGPT, Claude, or any AI tool

### 4. Export Your Prompts
- **Single Prompt**: Select it and click "Export as MD" or "Export as TXT"
- **All Prompts**: Click "Export All (JSON)" for backup
- **Import**: Click "Import (JSON)" to restore from backup

## Example Prompts to Create

### 1. Code Explanation Prompt
**Category**: Development  
**Tags**: code, explanation, learning  
**Content**:
```markdown
Explain the following code in simple terms:
- What does it do?
- How does it work?
- Are there any potential issues?
```

### 2. Bug Fix Prompt
**Category**: Debugging  
**Tags**: bug, fix, troubleshooting  
**Content**:
```markdown
I'm encountering the following error:
[ERROR MESSAGE]

Code context:
[CODE]

Please help me:
1. Identify the root cause
2. Suggest a fix
3. Explain why it happened
```

### 3. Documentation Prompt
**Category**: Documentation  
**Tags**: docs, readme, markdown  
**Content**:
```markdown
Create comprehensive documentation for this code including:
- Overview and purpose
- Installation instructions
- Usage examples
- API reference
- Contributing guidelines
```

## Tips

1. **Use Markdown**: Format your prompts with headers, lists, code blocks, and emphasis
2. **Organize with Categories**: Group similar prompts together
3. **Tag Everything**: Tags make searching much faster
4. **Export Regularly**: Back up your prompts as JSON
5. **Copy Quickly**: Use the clipboard button for instant copying

## Troubleshooting

### Application won't start
- Ensure .NET 8.0 Runtime is installed
- Check that you're on Windows 10/11

### Database errors
- The database is created automatically in `PromptBox/Data/`
- If issues persist, delete the `Data` folder and restart

### Theme not saving
- Check that the application has write permissions
- Settings are stored in user profile

## Data Location

Your prompts are stored locally at:
```
PromptBox/Data/promptbox.db
```

Back up this file to preserve your prompts!

## Keyboard Shortcuts

Standard Windows shortcuts work:
- `Ctrl+C`: Copy
- `Ctrl+V`: Paste
- `Ctrl+A`: Select all
- `Tab`: Navigate between fields

## 📚 Next Steps

- Read [README.md](README.md) for full feature documentation
- Check [FEATURES.md](FEATURES.md) for detailed feature descriptions
- See [BUILD_INSTRUCTIONS.md](BUILD_INSTRUCTIONS.md) for development setup
