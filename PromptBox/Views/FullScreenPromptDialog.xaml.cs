using Markdig;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PromptBox.Views;

/// <summary>
/// Full screen dialog for viewing prompt content with Markdown preview
/// </summary>
public partial class FullScreenPromptDialog : Window
{
    private readonly string _content;
    
    public FullScreenPromptDialog(string title, string content)
    {
        InitializeComponent();
        TitleText.Text = title;
        _content = content;
        ContentTextBox.Text = content;
        
        Loaded += (s, e) => RenderMarkdown();
    }

    private bool IsDarkMode()
    {
        var background = (SolidColorBrush)FindResource("MaterialDesignPaper");
        if (background != null)
        {
            var color = background.Color;
            var brightness = (color.R * 299 + color.G * 587 + color.B * 114) / 1000;
            return brightness < 128;
        }
        return true;
    }

    private void RenderMarkdown()
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
        
        var html = Markdown.ToHtml(_content, pipeline);
        var isDark = IsDarkMode();
        
        // Theme-aware styling with purple accent
        var styledHtml = isDark ? GetDarkThemeHtml(html) : GetLightThemeHtml(html);
        
        MarkdownPreview.NavigateToString(styledHtml);
    }

    private string GetDarkThemeHtml(string html)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            font-size: 14px;
            line-height: 1.6;
            padding: 20px;
            background-color: #1e1e1e;
            color: #d4d4d4;
        }}
        h1, h2, h3, h4, h5, h6 {{
            color: #b39ddb;
            margin-top: 24px;
            margin-bottom: 16px;
            font-weight: 600;
        }}
        h1 {{ font-size: 2em; border-bottom: 1px solid #7e57c2; padding-bottom: 0.3em; }}
        h2 {{ font-size: 1.5em; border-bottom: 1px solid #7e57c2; padding-bottom: 0.3em; }}
        h3 {{ font-size: 1.25em; }}
        code {{
            background-color: #2d2d2d;
            padding: 2px 6px;
            border-radius: 3px;
            font-family: 'Consolas', 'Courier New', monospace;
            font-size: 13px;
            color: #ce9178;
        }}
        pre {{
            background-color: #2d2d2d;
            padding: 16px;
            border-radius: 6px;
            overflow-x: auto;
            border: 1px solid #7e57c2;
        }}
        pre code {{
            background-color: transparent;
            padding: 0;
            color: #d4d4d4;
        }}
        blockquote {{
            border-left: 4px solid #7e57c2;
            margin: 16px 0;
            padding: 0 16px;
            color: #b39ddb;
            background-color: rgba(126, 87, 194, 0.1);
        }}
        ul, ol {{
            padding-left: 24px;
        }}
        li {{
            margin: 4px 0;
        }}
        a {{
            color: #b39ddb;
            text-decoration: none;
        }}
        a:hover {{
            text-decoration: underline;
            color: #d1c4e9;
        }}
        table {{
            border-collapse: collapse;
            width: 100%;
            margin: 16px 0;
        }}
        th, td {{
            border: 1px solid #444;
            padding: 8px 12px;
            text-align: left;
        }}
        th {{
            background-color: #7e57c2;
            color: white;
        }}
        tr:nth-child(even) {{
            background-color: #252525;
        }}
        hr {{
            border: none;
            border-top: 1px solid #7e57c2;
            margin: 24px 0;
        }}
        strong {{
            color: #d1c4e9;
        }}
        em {{
            color: #ce93d8;
        }}
    </style>
</head>
<body>
{html}
</body>
</html>";
    }

    private string GetLightThemeHtml(string html)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            font-size: 14px;
            line-height: 1.6;
            padding: 20px;
            background-color: #fafafa;
            color: #333333;
        }}
        h1, h2, h3, h4, h5, h6 {{
            color: #673ab7;
            margin-top: 24px;
            margin-bottom: 16px;
            font-weight: 600;
        }}
        h1 {{ font-size: 2em; border-bottom: 2px solid #673ab7; padding-bottom: 0.3em; }}
        h2 {{ font-size: 1.5em; border-bottom: 1px solid #9575cd; padding-bottom: 0.3em; }}
        h3 {{ font-size: 1.25em; }}
        code {{
            background-color: #f3e5f5;
            padding: 2px 6px;
            border-radius: 3px;
            font-family: 'Consolas', 'Courier New', monospace;
            font-size: 13px;
            color: #7b1fa2;
        }}
        pre {{
            background-color: #f5f5f5;
            padding: 16px;
            border-radius: 6px;
            overflow-x: auto;
            border: 1px solid #e1bee7;
        }}
        pre code {{
            background-color: transparent;
            padding: 0;
            color: #333333;
        }}
        blockquote {{
            border-left: 4px solid #673ab7;
            margin: 16px 0;
            padding: 0 16px;
            color: #512da8;
            background-color: rgba(103, 58, 183, 0.05);
        }}
        ul, ol {{
            padding-left: 24px;
        }}
        li {{
            margin: 4px 0;
        }}
        a {{
            color: #673ab7;
            text-decoration: none;
        }}
        a:hover {{
            text-decoration: underline;
            color: #512da8;
        }}
        table {{
            border-collapse: collapse;
            width: 100%;
            margin: 16px 0;
        }}
        th, td {{
            border: 1px solid #e0e0e0;
            padding: 8px 12px;
            text-align: left;
        }}
        th {{
            background-color: #673ab7;
            color: white;
        }}
        tr:nth-child(even) {{
            background-color: #f5f5f5;
        }}
        hr {{
            border: none;
            border-top: 1px solid #9575cd;
            margin: 24px 0;
        }}
        strong {{
            color: #512da8;
        }}
        em {{
            color: #7b1fa2;
        }}
    </style>
</head>
<body>
{html}
</body>
</html>";
    }

    private void ViewMode_Changed(object sender, RoutedEventArgs e)
    {
        if (RawPane == null || PreviewPane == null || Splitter == null) return;
        
        if (SplitViewRadio.IsChecked == true)
        {
            // Split view
            RawColumn.Width = new GridLength(1, GridUnitType.Star);
            SplitterColumn.Width = GridLength.Auto;
            PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
            RawPane.Visibility = Visibility.Visible;
            Splitter.Visibility = Visibility.Visible;
            PreviewPane.Visibility = Visibility.Visible;
        }
        else if (RawViewRadio.IsChecked == true)
        {
            // Raw text only
            RawColumn.Width = new GridLength(1, GridUnitType.Star);
            SplitterColumn.Width = new GridLength(0);
            PreviewColumn.Width = new GridLength(0);
            RawPane.Visibility = Visibility.Visible;
            Splitter.Visibility = Visibility.Collapsed;
            PreviewPane.Visibility = Visibility.Collapsed;
        }
        else if (PreviewViewRadio.IsChecked == true)
        {
            // Preview only
            RawColumn.Width = new GridLength(0);
            SplitterColumn.Width = new GridLength(0);
            PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
            RawPane.Visibility = Visibility.Collapsed;
            Splitter.Visibility = Visibility.Collapsed;
            PreviewPane.Visibility = Visibility.Visible;
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_content);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
