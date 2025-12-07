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
                var lines = Markdown.Split('\n');
                bool inCodeBlock = false;
                string codeBlockLanguage = string.Empty;
                var codeBlockLines = new System.Collections.Generic.List<string>();
                
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    
                    // Check for code block start/end
                    if (line.TrimStart().StartsWith("```"))
                    {
                        if (!inCodeBlock)
                        {
                            // Starting a code block
                            inCodeBlock = true;
                            codeBlockLanguage = line.TrimStart().Substring(3).Trim();
                            codeBlockLines.Clear();
                        }
                        else
                        {
                            // Ending a code block - render it
                            inCodeBlock = false;
                            AddCodeBlock(document, codeBlockLines, codeBlockLanguage);
                            codeBlockLines.Clear();
                            codeBlockLanguage = string.Empty;
                        }
                        continue;
                    }
                    
                    // If inside code block, collect lines
                    if (inCodeBlock)
                    {
                        codeBlockLines.Add(line);
                        continue;
                    }
                    
                    var paragraph = new Paragraph
                    {
                        Margin = new Thickness(0, 0, 0, 4) // Reduced default margin
                    };
                    
                    // Headers
                    if (line.StartsWith("# "))
                    {
                        paragraph.Margin = new Thickness(0, 8, 0, 4);
                        paragraph.Inlines.Add(new Run(line.Substring(2))
                        {
                            FontSize = 24,
                            FontWeight = FontWeights.Bold
                        });
                    }
                    else if (line.StartsWith("## "))
                    {
                        paragraph.Margin = new Thickness(0, 6, 0, 4);
                        paragraph.Inlines.Add(new Run(line.Substring(3))
                        {
                            FontSize = 20,
                            FontWeight = FontWeights.Bold
                        });
                    }
                    else if (line.StartsWith("### "))
                    {
                        paragraph.Margin = new Thickness(0, 4, 0, 4);
                        paragraph.Inlines.Add(new Run(line.Substring(4))
                        {
                            FontSize = 16,
                            FontWeight = FontWeights.Bold
                        });
                    }
                    // Inline code (single backticks)
                    else if (line.Contains("`") && !line.Contains("```"))
                    {
                        ProcessInlineCode(paragraph, line);
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
                        paragraph.Margin = new Thickness(indent * 10 + 20, 0, 0, 2);
                        paragraph.Inlines.Add(new Run("• " + line.TrimStart().Substring(2)));
                    }
                    // Numbered lists
                    else if (System.Text.RegularExpressions.Regex.IsMatch(line.TrimStart(), @"^\d+\.\s"))
                    {
                        var trimmed = line.TrimStart();
                        var indent = line.Length - trimmed.Length;
                        paragraph.Margin = new Thickness(indent * 10 + 20, 0, 0, 2);
                        paragraph.Inlines.Add(new Run(trimmed));
                    }
                    // Empty lines - smaller gap
                    else if (string.IsNullOrWhiteSpace(line))
                    {
                        paragraph.Margin = new Thickness(0, 0, 0, 2);
                        paragraph.Inlines.Add(new Run(" "));
                    }
                    // Normal text
                    else
                    {
                        paragraph.Inlines.Add(new Run(line));
                    }
                    
                    document.Blocks.Add(paragraph);
                }
                
                // Handle unclosed code block at end of document
                if (inCodeBlock && codeBlockLines.Count > 0)
                {
                    AddCodeBlock(document, codeBlockLines, codeBlockLanguage);
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
    
    private void AddCodeBlock(FlowDocument document, System.Collections.Generic.List<string> codeLines, string language)
    {
        var codeBackground = new SolidColorBrush(Color.FromRgb(40, 44, 52)); // Dark background
        var codeForeground = new SolidColorBrush(Color.FromRgb(171, 178, 191)); // Light text
        var codeFont = new FontFamily("Consolas, Courier New, monospace");
        
        // Create a Section to group the code block
        var section = new Section
        {
            Background = codeBackground,
            Padding = new Thickness(12),
            Margin = new Thickness(0, 8, 0, 8)
        };
        
        // Add language label if specified
        if (!string.IsNullOrWhiteSpace(language))
        {
            var langParagraph = new Paragraph(new Run(language)
            {
                Foreground = new SolidColorBrush(Color.FromRgb(130, 137, 150)),
                FontSize = 11,
                FontFamily = codeFont
            })
            {
                Margin = new Thickness(0, 0, 0, 8),
                Background = new SolidColorBrush(Color.FromRgb(33, 37, 43)),
                Padding = new Thickness(6, 2, 6, 2)
            };
            section.Blocks.Add(langParagraph);
        }
        
        // Add each line of code
        foreach (var codeLine in codeLines)
        {
            var codeParagraph = new Paragraph(new Run(codeLine)
            {
                FontFamily = codeFont,
                Foreground = codeForeground,
                FontSize = 13
            })
            {
                Margin = new Thickness(0, 0, 0, 0),
                LineHeight = 20
            };
            section.Blocks.Add(codeParagraph);
        }
        
        document.Blocks.Add(section);
    }
    
    private void ProcessInlineCode(Paragraph paragraph, string line)
    {
        var codeBackground = new SolidColorBrush(Color.FromRgb(60, 64, 72));
        var codeForeground = new SolidColorBrush(Color.FromRgb(230, 192, 123));
        var codeFont = new FontFamily("Consolas, Courier New, monospace");
        
        var parts = line.Split('`');
        for (int i = 0; i < parts.Length; i++)
        {
            if (i % 2 == 0)
            {
                // Normal text
                if (!string.IsNullOrEmpty(parts[i]))
                {
                    paragraph.Inlines.Add(new Run(parts[i]));
                }
            }
            else
            {
                // Inline code
                paragraph.Inlines.Add(new Run(parts[i])
                {
                    FontFamily = codeFont,
                    Background = codeBackground,
                    Foreground = codeForeground,
                    FontSize = 12
                });
            }
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
