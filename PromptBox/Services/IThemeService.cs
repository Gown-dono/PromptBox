namespace PromptBox.Services;

public interface IThemeService
{
    bool IsDarkMode { get; }
    void ToggleTheme();
    void SetTheme(bool isDark);
}
