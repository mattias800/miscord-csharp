using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Snacka.Shared.Models;

namespace Snacka.Server.Services;

public partial class LinkPreviewService : ILinkPreviewService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LinkPreviewService> _logger;

    // Cache to avoid repeated fetches for the same URL
    private readonly Dictionary<string, (LinkPreview? Preview, DateTime FetchedAt)> _cache = new();
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);
    private readonly object _cacheLock = new();

    // YouTube URL patterns
    private static readonly Regex YouTubeRegex = new(
        @"(?:youtube\.com/watch\?v=|youtu\.be/|youtube\.com/embed/)([a-zA-Z0-9_-]{11})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public LinkPreviewService(HttpClient httpClient, ILogger<LinkPreviewService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Configure HttpClient with browser-like user agent to avoid being blocked
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.5");
    }

    public async Task<LinkPreview?> GetLinkPreviewAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        // Normalize URL
        if (url.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        // Only allow http/https
        if (uri.Scheme != "http" && uri.Scheme != "https")
            return null;

        // Check cache
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(url, out var cached) && DateTime.UtcNow - cached.FetchedAt < _cacheDuration)
            {
                return cached.Preview;
            }
        }

        try
        {
            var preview = await FetchPreviewAsync(uri, cancellationToken);

            // Cache the result (even null to avoid repeated failed fetches)
            lock (_cacheLock)
            {
                _cache[url] = (preview, DateTime.UtcNow);

                // Clean old entries if cache gets too large
                if (_cache.Count > 1000)
                {
                    var oldEntries = _cache
                        .Where(kv => DateTime.UtcNow - kv.Value.FetchedAt > _cacheDuration)
                        .Select(kv => kv.Key)
                        .ToList();

                    foreach (var key in oldEntries)
                        _cache.Remove(key);
                }
            }

            return preview;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch link preview for {Url}", url);
            return null;
        }
    }

    private async Task<LinkPreview?> FetchPreviewAsync(Uri uri, CancellationToken cancellationToken)
    {
        // Check for YouTube URLs and use oEmbed API
        var youtubeMatch = YouTubeRegex.Match(uri.ToString());
        if (youtubeMatch.Success)
        {
            return await FetchYouTubePreviewAsync(uri.ToString(), youtubeMatch.Groups[1].Value, cancellationToken);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";

        // Handle direct image URLs
        if (contentType.StartsWith("image/"))
        {
            return new LinkPreview(
                Url: uri.ToString(),
                Title: null,
                Description: null,
                ImageUrl: uri.ToString(),
                SiteName: uri.Host,
                Type: "image",
                FaviconUrl: GetFaviconUrl(uri)
            );
        }

        // Only parse HTML content
        if (!contentType.Contains("html"))
            return null;

        // Read limited amount of content (some sites put meta tags at the end, so we need more)
        var content = await ReadLimitedContentAsync(response.Content, 700 * 1024, cancellationToken);

        if (string.IsNullOrEmpty(content))
            return null;

        return ParseOpenGraphTags(uri, content);
    }

    private static async Task<string> ReadLimitedContentAsync(HttpContent httpContent, int maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await httpContent.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var buffer = new char[maxBytes];
        var charsRead = await reader.ReadBlockAsync(buffer, 0, maxBytes);

        return new string(buffer, 0, charsRead);
    }

    private LinkPreview? ParseOpenGraphTags(Uri uri, string html)
    {
        var ogTitle = ExtractMetaContent(html, "og:title") ?? ExtractMetaContent(html, "twitter:title");
        var ogDescription = ExtractMetaContent(html, "og:description") ?? ExtractMetaContent(html, "twitter:description") ?? ExtractMetaContent(html, "description");
        var ogImage = ExtractMetaContent(html, "og:image") ?? ExtractMetaContent(html, "twitter:image");
        var ogSiteName = ExtractMetaContent(html, "og:site_name");
        var ogType = ExtractMetaContent(html, "og:type");

        // Fall back to <title> tag if no og:title
        ogTitle ??= ExtractTitleTag(html);

        // Skip if we have no meaningful content
        if (string.IsNullOrEmpty(ogTitle) && string.IsNullOrEmpty(ogDescription) && string.IsNullOrEmpty(ogImage))
            return null;

        // Make relative image URLs absolute
        if (!string.IsNullOrEmpty(ogImage) && !ogImage.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            ogImage = new Uri(uri, ogImage).ToString();
        }

        return new LinkPreview(
            Url: uri.ToString(),
            Title: TruncateString(ogTitle, 200),
            Description: TruncateString(ogDescription, 500),
            ImageUrl: ogImage,
            SiteName: ogSiteName ?? uri.Host,
            Type: ogType,
            FaviconUrl: GetFaviconUrl(uri)
        );
    }

    private static string? ExtractMetaContent(string html, string property)
    {
        // Try property attribute (OpenGraph style)
        var propertyMatch = Regex.Match(html,
            $@"<meta[^>]+property=[""']{Regex.Escape(property)}[""'][^>]+content=[""']([^""']*)[""']",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (propertyMatch.Success)
            return WebUtility.HtmlDecode(propertyMatch.Groups[1].Value.Trim());

        // Try content before property
        propertyMatch = Regex.Match(html,
            $@"<meta[^>]+content=[""']([^""']*)[""'][^>]+property=[""']{Regex.Escape(property)}[""']",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (propertyMatch.Success)
            return WebUtility.HtmlDecode(propertyMatch.Groups[1].Value.Trim());

        // Try name attribute (standard meta tags like description)
        var nameMatch = Regex.Match(html,
            $@"<meta[^>]+name=[""']{Regex.Escape(property)}[""'][^>]+content=[""']([^""']*)[""']",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (nameMatch.Success)
            return WebUtility.HtmlDecode(nameMatch.Groups[1].Value.Trim());

        // Try content before name
        nameMatch = Regex.Match(html,
            $@"<meta[^>]+content=[""']([^""']*)[""'][^>]+name=[""']{Regex.Escape(property)}[""']",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (nameMatch.Success)
            return WebUtility.HtmlDecode(nameMatch.Groups[1].Value.Trim());

        return null;
    }

    private static string? ExtractTitleTag(string html)
    {
        var match = Regex.Match(html, @"<title[^>]*>([^<]*)</title>", RegexOptions.IgnoreCase);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value.Trim()) : null;
    }

    private static string GetFaviconUrl(Uri uri)
    {
        return $"{uri.Scheme}://{uri.Host}/favicon.ico";
    }

    private static string? TruncateString(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// Fetches YouTube video preview using the oEmbed API.
    /// </summary>
    private async Task<LinkPreview?> FetchYouTubePreviewAsync(string originalUrl, string videoId, CancellationToken cancellationToken)
    {
        try
        {
            var oEmbedUrl = $"https://www.youtube.com/oembed?url=https://www.youtube.com/watch?v={videoId}&format=json";

            _logger.LogInformation("Fetching YouTube oEmbed for video {VideoId}", videoId);

            var response = await _httpClient.GetAsync(oEmbedUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("YouTube oEmbed returned {StatusCode} for video {VideoId}", response.StatusCode, videoId);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var oEmbed = JsonSerializer.Deserialize<YouTubeOEmbed>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (oEmbed == null)
                return null;

            // YouTube thumbnail URL - use maxresdefault for best quality, fallback to hqdefault
            var thumbnailUrl = $"https://img.youtube.com/vi/{videoId}/maxresdefault.jpg";

            return new LinkPreview(
                Url: originalUrl,
                Title: oEmbed.Title,
                Description: $"by {oEmbed.Author_Name}",
                ImageUrl: thumbnailUrl,
                SiteName: "YouTube",
                Type: "video",
                FaviconUrl: "https://www.youtube.com/favicon.ico"
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch YouTube preview for video {VideoId}", videoId);
            return null;
        }
    }

    private record YouTubeOEmbed(
        string? Title,
        string? Author_Name,
        string? Author_Url,
        string? Thumbnail_Url,
        int? Thumbnail_Width,
        int? Thumbnail_Height
    );
}
