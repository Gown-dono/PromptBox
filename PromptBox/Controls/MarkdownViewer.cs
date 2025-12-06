using Markdig;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace PromptBox.Controls;

/// <summary>
/// A WPF control that renders Markdown content
/// </summary>
public class MarkdownViewer : Control
{
    private ScrollViewer? _scrollViewer;
    private RichTextBox? _richTextBox;

    static MarkdownViewer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MarkdownViewer),
            new FrameworkPropertyMetadata(typeof(MarkdownViewer)));
    }

    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.Register(
            nameof(Markdown),
            typeof(string),
            typeof(MarkdownViewer),
            new PropertyMetadata(string.Empty, OnMarkdownChanged));

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
        {
            viewer.UpdateMarkdown();
        }
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        
        _scrollViewer = GetTemplateChild("PART_ScrollViewer") as ScrollViewer;
        _richTextBox = GetTemplateChild("PART_RichTextBox") as RichTextBox;
        
        UpdateMarkdown();
    }

    private void UpdateMarkdown()
    {
        if (_richTextBox == null) return;

        try
        {
            var document = new FlowDocument();
            
            if (!string.IsNullOrWhiteSpace(Markdown))
            {
                var html = Markdig.Markdown.ToHtml(Markdown);
                var lines = Markdown.Split('\n');
                
                foreach (var line in lines)
                {
                    var paragraph = new Paragraph();
                    
                    // Headers
                    if (line.StartsWith("# "))
                    {
                        paragraph.Inlines.Add(new Run(line.Substring(2))
                        {
                            FontSize = 24,
                            FontWeight = FontWeights.Bold
                        });
                    }
                    else if (line.StartsWith("## "))
                    {
                        paragraph.Inlines.Add(new Run(line.Substring(3))
                        {
                            FontSize = 20,
                            FontWeight = FontWeights.Bold
                        });
                    }
                    else if (line.StartsWith("### "))
                    {
                        paragraph.Inlines.Add(new Run(line.Substring(4))
                        {
                            FontSize = 16,
                            FontWeight = FontWeights.Bold
                        });
                    }
                    // Bold
                    else if (line.Contains("**"))
                    {
                        ProcessInlineFormatting(paragraph, line);
                    }
                    // Lists
                    else if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
                    {
                        var indent = line.Length - line.TrimStart().Length;
                        paragraph.Margin = new Thickness(indent * 10 + 20, 0, 0, 0);
                        paragraph.Inlines.Add(new Run("• " + line.TrimStart().Substring(2)));
                    }
                    // Code blocks
                    else if (line.StartsWith("```"))
                    {
                        paragraph.Inlines.Add(new Run(line)
                        {
                            FontFamily = new FontFamily("Consolas"),
                            Background = new SolidColorBrush(Color.FromRgb(240, 240, 240))
                        });
                    }
                    // Normal text
                    else
                    {
                        paragraph.Inlines.Add(new Run(line));
                    }
                    
                    document.Blocks.Add(paragraph);
                }
            }
            else
            {
                document.Blocks.Add(new Paragraph(new Run("Preview will appear here...")));
            }

            _richTextBox.Document = document;
        }
        catch
        {
            // Fallback to plain text
            var document = new FlowDocument();
            document.Blocks.Add(new Paragraph(new Run(Markdown ?? string.Empty)));
            _richTextBox.Document = document;
        }
    }

    private void ProcessInlineFormatting(Paragraph paragraph, string line)
    {
        var parts = line.Split(new[] { "**" }, StringSplitOptions.None);
        for (int i = 0; i < parts.Length; i++)
        {
            if (i % 2 == 0)
            {
                paragraph.Inlines.Add(new Run(parts[i]));
            }
            else
            {
                paragraph.Inlines.Add(new Run(parts[i]) { FontWeight = FontWeights.Bold });
            }
        }
    }
}
