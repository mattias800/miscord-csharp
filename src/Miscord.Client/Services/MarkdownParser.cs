using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Miscord.Client.Services;

/// <summary>
/// A Run that represents a clickable URL link.
/// </summary>
public class LinkRun : Run
{
    private static readonly IBrush LinkForeground = new SolidColorBrush(Color.Parse("#00aff4"));

    public string Url { get; }

    public LinkRun(string displayText, string url) : base(displayText)
    {
        Url = url;
        Foreground = LinkForeground;
        TextDecorations = Avalonia.Media.TextDecorations.Underline;
    }
}

/// <summary>
/// A Run that represents a @mention (user mention).
/// </summary>
public class MentionRun : Run
{
    private static readonly IBrush MentionForeground = new SolidColorBrush(Color.Parse("#dee0fc"));
    private static readonly IBrush MentionBackground = new SolidColorBrush(Color.Parse("rgba(88, 101, 242, 0.3)"));

    public string Username { get; }

    public MentionRun(string username) : base($"@{username}")
    {
        Username = username;
        Foreground = MentionForeground;
        Background = MentionBackground;
        FontWeight = FontWeight.Medium;
    }
}

public static class MarkdownParser
{
    private static readonly IBrush CodeBackground = new SolidColorBrush(Color.Parse("#2f3136"));
    private static readonly IBrush CodeForeground = new SolidColorBrush(Color.Parse("#e9967a"));
    private static readonly IBrush TextForeground = new SolidColorBrush(Color.Parse("#dcddde"));
    private static readonly IBrush HeadingForeground = new SolidColorBrush(Color.Parse("#ffffff"));
    private static readonly IBrush LinkForeground = new SolidColorBrush(Color.Parse("#00aff4"));

    // Regex patterns for markdown
    private static readonly Regex CodeBlockRegex = new(@"```(\w+)?\n?([\s\S]*?)```", RegexOptions.Compiled);
    private static readonly Regex InlineCodeRegex = new(@"`([^`\n]+)`", RegexOptions.Compiled);
    private static readonly Regex BoldRegex = new(@"\*\*(.+?)\*\*|__(.+?)__", RegexOptions.Compiled);
    private static readonly Regex ItalicRegex = new(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)|(?<!_)_(?!_)(.+?)(?<!_)_(?!_)", RegexOptions.Compiled);

    // URL regex - matches http://, https://, and www. URLs
    private static readonly Regex UrlRegex = new(
        @"(https?://[^\s<>\[\]""'`]+|www\.[^\s<>\[\]""'`]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Mention regex - matches @username (alphanumeric and underscore, 1-32 chars)
    private static readonly Regex MentionRegex = new(@"@(\w{1,32})", RegexOptions.Compiled);

    // Block-level patterns
    private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex UnorderedListRegex = new(@"^[\s]*[-*+]\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex OrderedListRegex = new(@"^[\s]*(\d+)\.\s+(.+)$", RegexOptions.Compiled);

    public record MarkdownBlock(string Content, bool IsCodeBlock, string? Language = null);

    /// <summary>
    /// Extracts all URLs from the given text.
    /// </summary>
    public static List<string> ExtractUrls(string text)
    {
        var urls = new List<string>();
        if (string.IsNullOrEmpty(text)) return urls;

        foreach (Match match in UrlRegex.Matches(text))
        {
            var url = match.Value;
            // Ensure URL has protocol
            if (url.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;
            urls.Add(url);
        }
        return urls;
    }

    /// <summary>
    /// Opens a URL in the default browser.
    /// </summary>
    public static void OpenUrl(string url)
    {
        try
        {
            // Ensure URL has protocol
            if (url.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to open URL: {ex.Message}");
        }
    }

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
    /// Parses inline markdown (headings, lists, bold, italic, inline code, URLs) and returns Inline elements.
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
                    AddTextWithUrls(inlines, italicSegment.Content, FontWeight.Bold,
                        italicSegment.IsItalic ? FontStyle.Italic : FontStyle.Normal);
                }
            }
            else
            {
                var italicSegments = SplitByItalic(segment.Content);
                foreach (var italicSegment in italicSegments)
                {
                    if (italicSegment.IsItalic)
                    {
                        AddTextWithUrls(inlines, italicSegment.Content, FontWeight.Normal, FontStyle.Italic);
                    }
                    else if (!string.IsNullOrEmpty(italicSegment.Content))
                    {
                        AddTextWithUrls(inlines, italicSegment.Content, FontWeight.Normal, FontStyle.Normal);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Adds text to inlines, converting URLs to clickable links and @mentions to highlighted spans.
    /// </summary>
    private static void AddTextWithUrls(List<Inline> inlines, string text, FontWeight weight, FontStyle style)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Combine URL and mention matches and process them in order
        var urlMatches = UrlRegex.Matches(text).Cast<Match>().Select(m => (m, IsUrl: true));
        var mentionMatches = MentionRegex.Matches(text).Cast<Match>().Select(m => (m, IsUrl: false));
        var allMatches = urlMatches.Concat(mentionMatches)
            .OrderBy(x => x.m.Index)
            .ToList();

        var lastIndex = 0;
        foreach (var (match, isUrl) in allMatches)
        {
            // Skip if this match overlaps with a previous one
            if (match.Index < lastIndex) continue;

            // Add text before the match
            if (match.Index > lastIndex)
            {
                var beforeText = text.Substring(lastIndex, match.Index - lastIndex);
                inlines.Add(new Run(beforeText)
                {
                    Foreground = TextForeground,
                    FontWeight = weight,
                    FontStyle = style
                });
            }

            if (isUrl)
            {
                // Add the URL as a clickable link
                var url = match.Value;
                // Ensure URL has protocol for the stored value
                var fullUrl = url.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                    ? "https://" + url
                    : url;
                inlines.Add(new LinkRun(url, fullUrl));
            }
            else
            {
                // Add the mention as a highlighted span
                var username = match.Groups[1].Value;
                inlines.Add(new MentionRun(username));
            }

            lastIndex = match.Index + match.Length;
        }

        // Add remaining text after the last match
        if (lastIndex < text.Length)
        {
            var afterText = text.Substring(lastIndex);
            inlines.Add(new Run(afterText)
            {
                Foreground = TextForeground,
                FontWeight = weight,
                FontStyle = style
            });
        }
        else if (lastIndex == 0)
        {
            // No matches found, add the whole text
            inlines.Add(new Run(text)
            {
                Foreground = TextForeground,
                FontWeight = weight,
                FontStyle = style
            });
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
