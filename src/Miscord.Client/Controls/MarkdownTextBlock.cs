using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Miscord.Client.Services;

namespace Miscord.Client.Controls;

/// <summary>
/// A control that renders markdown-formatted text.
/// Supports: # headings, **bold**, *italic*, `inline code`, ```code blocks```, - lists, 1. numbered lists
/// Code blocks are rendered as full-width boxes with syntax highlighting.
/// </summary>
public class MarkdownTextBlock : StackPanel
{
    private static readonly IBrush CodeBlockBackground = new SolidColorBrush(Color.Parse("#1e1e1e"));
    private static readonly IBrush TextForeground = new SolidColorBrush(Color.Parse("#dcddde"));

    // Syntax highlighter instance - can be replaced with a different implementation
    private static readonly ISyntaxHighlighter SyntaxHighlighter = new SimpleSyntaxHighlighter();

    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownTextBlock, string?>(nameof(Markdown));

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<MarkdownTextBlock, double>(nameof(FontSize), 15.0);

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    static MarkdownTextBlock()
    {
        MarkdownProperty.Changed.AddClassHandler<MarkdownTextBlock>((x, _) => x.UpdateContent());
        FontSizeProperty.Changed.AddClassHandler<MarkdownTextBlock>((x, _) => x.UpdateContent());
    }

    public MarkdownTextBlock()
    {
        Spacing = 4;
    }

    private void UpdateContent()
    {
        Children.Clear();

        if (string.IsNullOrEmpty(Markdown))
            return;

        var blocks = MarkdownParser.ParseToBlocks(Markdown);

        foreach (var block in blocks)
        {
            if (block.IsCodeBlock)
            {
                // Create a full-width code block with syntax highlighting
                var codeText = new TextBlock
                {
                    FontFamily = new FontFamily("Consolas, Monaco, 'Courier New', monospace"),
                    FontSize = FontSize,
                    TextWrapping = TextWrapping.Wrap,
                    Padding = new Thickness(12, 8)
                };

                // Apply syntax highlighting
                var highlightedInlines = SyntaxHighlighter.Highlight(block.Content, block.Language);
                foreach (var inline in highlightedInlines)
                {
                    codeText.Inlines?.Add(inline);
                }

                var codeBorder = new Border
                {
                    Background = CodeBlockBackground,
                    CornerRadius = new CornerRadius(4),
                    Child = codeText,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(0, 4)
                };

                Children.Add(codeBorder);
            }
            else
            {
                // Create a TextBlock with inline formatting
                var textBlock = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = TextForeground,
                    FontSize = FontSize
                };

                var inlines = MarkdownParser.ParseInlines(block.Content);
                foreach (var inline in inlines)
                {
                    textBlock.Inlines?.Add(inline);
                }

                if (textBlock.Inlines?.Count > 0)
                {
                    Children.Add(textBlock);
                }
            }
        }
    }
}
