# Build Instructions for PromptBox

## Prerequisites

Before building PromptBox, ensure you have the following installed:

1. **Visual Studio 2022** (Community, Professional, or Enterprise)
   - Workload: .NET Desktop Development
   - OR **Visual Studio Code** with C# extension

2. **.NET 8.0 SDK**
   - Download from: https://dotnet.microsoft.com/download/dotnet/8.0

3. **Windows 10/11** (WPF is Windows-only)

## Building with Visual Studio

### Method 1: Using Visual Studio IDE

1. Open `PromptBox.sln` in Visual Studio 2022

2. Restore NuGet packages:
   - Right-click on the solution in Solution Explorer
   - Select "Restore NuGet Packages"
   - Wait for packages to download

3. Build the solution:
   - Press `Ctrl+Shift+B` or
   - Go to Build → Build Solution

4. Run the application:
   - Press `F5` (Debug mode) or `Ctrl+F5` (Release mode)

### Method 2: Using Command Line

1. Open Command Prompt or PowerShell

2. Navigate to the project directory:
```bash
cd path\to\PromptBox
```

3. Restore dependencies:
```bash
dotnet restore
```

4. Build the project:
```bash
dotnet build
```

5. Run the application:
```bash
dotnet run --project PromptBox\PromptBox.csproj
```

## Creating a Release Build

### Self-Contained Executable (Recommended)

This creates a standalone executable that doesn't require .NET to be installed:

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Output location: `PromptBox\bin\Release\net8.0-windows\win-x64\publish\`

### Framework-Dependent Build

Smaller file size but requires .NET 8.0 Runtime:

```bash
dotnet publish -c Release -r win-x64 --no-self-contained
```

## Troubleshooting

### Issue: NuGet packages not restoring

**Solution:**
```bash
dotnet nuget locals all --clear
dotnet restore --force
```

### Issue: MaterialDesign themes not loading

**Solution:**
- Ensure MaterialDesignThemes and MaterialDesignColors packages are installed
- Clean and rebuild the solution:
```bash
dotnet clean
dotnet build
```

### Issue: LiteDB database errors

**Solution:**
- The database is created automatically in the `Data` folder
- If issues persist, delete the `Data` folder and restart the app

### Issue: Markdown preview not rendering

**Solution:**
- Verify Markdig.Wpf package is installed
- Check that the XAML namespace is correctly defined in MainWindow.xaml

## Project Structure

```
PromptBox/
├── PromptBox.sln              # Solution file
├── PromptBox/
│   ├── PromptBox.csproj       # Project file
│   ├── App.xaml               # Application entry point
│   ├── App.xaml.cs            # DI configuration
│   ├── Models/                # Data models
│   ├── ViewModels/            # MVVM ViewModels
│   ├── Views/                 # XAML views
│   ├── Services/              # Business services
│   ├── Utilities/             # Helper classes
│   └── Properties/            # Settings
├── README.md                  # Project documentation
└── LICENSE                    # MIT License
```

## Development Tips

### Hot Reload
- Visual Studio 2022 supports XAML Hot Reload
- Make UI changes without restarting the app

### Debugging
- Set breakpoints in ViewModels to debug business logic
- Use the Output window to see LiteDB operations

### Testing Database
- Database file: `PromptBox\bin\Debug\net8.0-windows\Data\promptbox.db`
- Use LiteDB Studio to inspect: https://github.com/mbdavid/LiteDB.Studio

## Next Steps

After building:
1. Test all features (create, edit, delete prompts)
2. Try importing/exporting
3. Toggle between light and dark themes
4. Test search and filtering

## Support

For issues or questions:
- Check the README.md for feature documentation
- Review the code comments for implementation details
- Open an issue on GitHub

---

Happy coding! 🚀
