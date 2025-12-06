using MaterialDesignThemes.Wpf;
using System.Windows;

namespace PromptBox.Services;

/// <summary>
/// Service for managing application theme (Light/Dark mode)
/// </summary>
public class ThemeService : IThemeService
{
    private readonly PaletteHelper _paletteHelper;
    private bool _isDarkMode;

    public bool IsDarkMode => _isDarkMode;

    public ThemeService()
    {
        _paletteHelper = new PaletteHelper();
        LoadThemePreference();
    }

    public void ToggleTheme()
    {
        SetTheme(!_isDarkMode);
    }

    public void SetTheme(bool isDark)
    {
        _isDarkMode = isDark;
        
        var theme = _paletteHelper.GetTheme();
        theme.SetBaseTheme(isDark ? Theme.Dark : Theme.Light);
        _paletteHelper.SetTheme(theme);
        
        SaveThemePreference();
    }

    private void LoadThemePreference()
    {
        var savedTheme = Properties.Settings.Default.IsDarkMode;
        SetTheme(savedTheme);
    }

    private void SaveThemePreference()
    {
        Properties.Settings.Default.IsDarkMode = _isDarkMode;
        Properties.Settings.Default.Save();
    }
}
