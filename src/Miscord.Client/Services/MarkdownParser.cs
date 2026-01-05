using System.Text.RegularExpressions;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Miscord.Client.Services;

public static class MarkdownParser
{
    private static readonly IBrush CodeBackground = new SolidColorBrush(Color.Parse("#2f3136"));
    private static readonly IBrush CodeForeground = new SolidColorBrush(Color.Parse("#e9967a"));
    private static readonly IBrush TextForeground = new SolidColorBrush(Color.Parse("#dcddde"));
    private static readonly IBrush HeadingForeground = new SolidColorBrush(Color.Parse("#ffffff"));

    // Regex patterns for markdown
    private static readonly Regex CodeBlockRegex = new(@"```(\w+)?\n?([\s\S]*?)```", RegexOptions.Compiled);
    private static readonly Regex InlineCodeRegex = new(@"`([^`\n]+)`", RegexOptions.Compiled);
    private static readonly Regex BoldRegex = new(@"\*\*(.+?)\*\*|__(.+?)__", RegexOptions.Compiled);
    private static readonly Regex ItalicRegex = new(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)|(?<!_)_(?!_)(.+?)(?<!_)_(?!_)", RegexOptions.Compiled);

    // Block-level patterns
    private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex UnorderedListRegex = new(@"^[\s]*[-*+]\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex OrderedListRegex = new(@"^[\s]*(\d+)\.\s+(.+)$", RegexOptions.Compiled);

    public record MarkdownBlock(string Content, bool IsCodeBlock, string? Language = null);

    /// <summary>
    /// Splits markdown text into blocks (code blocks vs regular text).
    /// </summary>
    public static List<MarkdownBlock> ParseToBlocks(string text)
    {
        var blocks = new List<MarkdownBlock>();

        if (string.IsNullOrEmpty(text))
            return blocks;

        var lastIndex = 0;

        foreach (Match match in CodeBlockRegex.Matches(text))
        {
            // Add text before the code block
            if (match.Index > lastIndex)
            {
                var textContent = text.Substring(lastIndex, match.Index - lastIndex).Trim();
                if (!string.IsNullOrEmpty(textContent))
                {
                    blocks.Add(new MarkdownBlock(textContent, false));
                }
            }

            // Add the code block content with language hint
            var language = match.Groups[1].Value;
            var codeContent = match.Groups[2].Value.Trim();
            if (!string.IsNullOrEmpty(codeContent))
            {
                blocks.Add(new MarkdownBlock(codeContent, true, string.IsNullOrEmpty(language) ? null : language));
            }
            lastIndex = match.Index + match.Length;
        }

        // Add remaining text
        if (lastIndex < text.Length)
        {
            var remaining = text.Substring(lastIndex).Trim();
            if (!string.IsNullOrEmpty(remaining))
            {
                blocks.Add(new MarkdownBlock(remaining, false));
            }
        }

        return blocks;
    }

    /// <summary>
    /// Parses inline markdown (headings, lists, bold, italic, inline code) and returns Inline elements.
    /// </summary>
    public static List<Inline> ParseInlines(string text)
    {
        var inlines = new List<Inline>();

        if (string.IsNullOrEmpty(text))
            return inlines;

        var lines = text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');

            // Check for heading
            var headingMatch = HeadingRegex.Match(line);
            if (headingMatch.Success)
            {
                if (inlines.Count > 0)
                    inlines.Add(new LineBreak());

                var level = headingMatch.Groups[1].Value.Length;
                var content = headingMatch.Groups[2].Value;
                AddHeading(inlines, content, level);

                if (i < lines.Length - 1)
                    inlines.Add(new LineBreak());
                continue;
            }

            // Check for unordered list
            var ulMatch = UnorderedListRegex.Match(line);
            if (ulMatch.Success)
            {
                if (inlines.Count > 0 && i > 0 && !IsListLine(lines[i - 1]))
                    inlines.Add(new LineBreak());

                var content = ulMatch.Groups[1].Value;
                AddListItem(inlines, content, "•");

                if (i < lines.Length - 1)
                    inlines.Add(new LineBreak());
                continue;
            }

            // Check for ordered list
            var olMatch = OrderedListRegex.Match(line);
            if (olMatch.Success)
            {
                if (inlines.Count > 0 && i > 0 && !IsListLine(lines[i - 1]))
                    inlines.Add(new LineBreak());

                var number = olMatch.Groups[1].Value;
                var content = olMatch.Groups[2].Value;
                AddListItem(inlines, content, $"{number}.");

                if (i < lines.Length - 1)
                    inlines.Add(new LineBreak());
                continue;
            }

            // Regular text - parse inline markdown
            if (!string.IsNullOrEmpty(line))
            {
                ParseInlineFormatting(inlines, line);
            }

            if (i < lines.Length - 1)
                inlines.Add(new LineBreak());
        }

        return inlines;
    }

    private static bool IsListLine(string line)
    {
        var trimmed = line.TrimEnd('\r');
        return UnorderedListRegex.IsMatch(trimmed) || OrderedListRegex.IsMatch(trimmed);
    }

    private static void AddHeading(List<Inline> inlines, string content, int level)
    {
        var fontSize = level switch
        {
            1 => 24.0,
            2 => 20.0,
            3 => 18.0,
            4 => 16.0,
            5 => 14.0,
            _ => 13.0
        };

        var run = new Run(content)
        {
            FontWeight = FontWeight.Bold,
            FontSize = fontSize,
            Foreground = HeadingForeground
        };
        inlines.Add(run);
    }

    private static void AddListItem(List<Inline> inlines, string content, string bullet)
    {
        var bulletRun = new Run($"  {bullet} ")
        {
            Foreground = TextForeground
        };
        inlines.Add(bulletRun);

        ParseInlineFormatting(inlines, content);
    }

    private static void ParseInlineFormatting(List<Inline> inlines, string text)
    {
        // Split by inline code first
        var codeSegments = SplitByInlineCode(text);

        foreach (var segment in codeSegments)
        {
            if (segment.IsCode)
            {
                var codeRun = new Run(segment.Content)
                {
                    FontFamily = new FontFamily("Consolas, Monaco, 'Courier New', monospace"),
                    Background = CodeBackground,
                    Foreground = CodeForeground
                };
                inlines.Add(codeRun);
            }
            else
            {
                ParseBoldAndItalic(inlines, segment.Content);
            }
        }
    }

    private static List<(string Content, bool IsCode)> SplitByInlineCode(string text)
    {
        var segments = new List<(string Content, bool IsCode)>();
        var lastIndex = 0;

        foreach (Match match in InlineCodeRegex.Matches(text))
        {
            if (match.Index > lastIndex)
            {
                segments.Add((text.Substring(lastIndex, match.Index - lastIndex), false));
            }

            segments.Add((match.Groups[1].Value, true));
            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
        {
            segments.Add((text.Substring(lastIndex), false));
        }

        return segments;
    }

    private static void ParseBoldAndItalic(List<Inline> inlines, string text)
    {
        var boldSegments = SplitByBold(text);

        foreach (var segment in boldSegments)
        {
            if (segment.IsBold)
            {
                var italicSegments = SplitByItalic(segment.Content);
                foreach (var italicSegment in italicSegments)
                {
                    var run = new Run(italicSegment.Content)
                    {
                        FontWeight = FontWeight.Bold,
                        Foreground = TextForeground
                    };
                    if (italicSegment.IsItalic)
                    {
                        run.FontStyle = FontStyle.Italic;
                    }
                    inlines.Add(run);
                }
            }
            else
            {
                var italicSegments = SplitByItalic(segment.Content);
                foreach (var italicSegment in italicSegments)
                {
                    if (italicSegment.IsItalic)
                    {
                        var run = new Run(italicSegment.Content)
                        {
                            FontStyle = FontStyle.Italic,
                            Foreground = TextForeground
                        };
                        inlines.Add(run);
                    }
                    else if (!string.IsNullOrEmpty(italicSegment.Content))
                    {
                        inlines.Add(new Run(italicSegment.Content) { Foreground = TextForeground });
                    }
                }
            }
        }
    }

    private static List<(string Content, bool IsBold)> SplitByBold(string text)
    {
        var segments = new List<(string Content, bool IsBold)>();
        var lastIndex = 0;

        foreach (Match match in BoldRegex.Matches(text))
        {
            if (match.Index > lastIndex)
            {
                segments.Add((text.Substring(lastIndex, match.Index - lastIndex), false));
            }

            var content = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            segments.Add((content, true));
            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
        {
            segments.Add((text.Substring(lastIndex), false));
        }

        return segments;
    }

    private static List<(string Content, bool IsItalic)> SplitByItalic(string text)
    {
        var segments = new List<(string Content, bool IsItalic)>();
        var lastIndex = 0;

        foreach (Match match in ItalicRegex.Matches(text))
        {
            if (match.Index > lastIndex)
            {
                segments.Add((text.Substring(lastIndex, match.Index - lastIndex), false));
            }

            var content = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            segments.Add((content, true));
            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
        {
            segments.Add((text.Substring(lastIndex), false));
        }

        return segments;
    }
}
