using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Miscord.Client.Services;

namespace Miscord.Client.Controls;

/// <summary>
/// A control that renders markdown-formatted text.
/// Supports: # headings, **bold**, *italic*, `inline code`, ```code blocks```, - lists, 1. numbered lists, URLs
/// Code blocks are rendered as full-width boxes with syntax highlighting.
/// URLs are rendered as clickable links.
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

    /// <summary>
    /// Handles pointer pressed events to detect clicks on URLs.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        // Find the TextBlock that was clicked
        if (e.Source is TextBlock textBlock && textBlock.Inlines != null)
        {
            // Check if any inline is a LinkRun
            var linkRuns = textBlock.Inlines.OfType<LinkRun>().ToList();
            if (linkRuns.Count == 1)
            {
                // Single link in the text block - open it
                MarkdownParser.OpenUrl(linkRuns[0].Url);
                e.Handled = true;
            }
            else if (linkRuns.Count > 1)
            {
                // Multiple links - try to determine which one was clicked
                // For now, we'll use a simple heuristic based on position
                var url = GetClickedLinkUrl(textBlock, e.GetPosition(textBlock), linkRuns);
                if (!string.IsNullOrEmpty(url))
                {
                    MarkdownParser.OpenUrl(url);
                    e.Handled = true;
                }
            }
        }
    }

    /// <summary>
    /// Attempts to determine which link was clicked based on position.
    /// This is a simplified approach - in a real implementation, you'd use text hit testing.
    /// </summary>
    private static string? GetClickedLinkUrl(TextBlock textBlock, Point position, List<LinkRun> linkRuns)
    {
        if (linkRuns.Count == 0) return null;

        // For simplicity, if there are multiple links, we approximate by position
        // This assumes links are roughly evenly distributed in the text
        var textWidth = textBlock.Bounds.Width;
        if (textWidth <= 0) return linkRuns[0].Url;

        var relativeX = position.X / textWidth;
        var linkIndex = (int)(relativeX * linkRuns.Count);
        linkIndex = Math.Clamp(linkIndex, 0, linkRuns.Count - 1);

        return linkRuns[linkIndex].Url;
    }

    /// <summary>
    /// Changes cursor to hand when hovering over links.
    /// </summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (e.Source is TextBlock textBlock && textBlock.Inlines != null)
        {
            var hasLinks = textBlock.Inlines.OfType<LinkRun>().Any();
            Cursor = hasLinks ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
        }
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
