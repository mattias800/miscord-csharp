using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Miscord.Client.Services;
using Miscord.Shared.Models;

namespace Miscord.Client.Controls;

/// <summary>
/// A control that renders message content with markdown formatting and link preview cards.
/// Combines MarkdownTextBlock with LinkPreviewCard for URLs found in the content.
/// Also handles inline GIF display for Tenor URLs.
/// </summary>
public class MessageContentBlock : StackPanel
{
    private static IApiClient? _apiClient;
    private static readonly Dictionary<string, LinkPreview?> _previewCache = new();
    private static readonly Dictionary<string, Bitmap?> _gifCache = new();
    private static readonly HashSet<string> _pendingRequests = new();
    private static readonly object _cacheLock = new();

    // Regex to detect Tenor GIF URLs
    private static readonly Regex TenorGifRegex = new(
        @"^https?://(?:media\.)?tenor\.com/[^\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static readonly StyledProperty<string?> ContentProperty =
        AvaloniaProperty.Register<MessageContentBlock, string?>(nameof(Content));

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<MessageContentBlock, double>(nameof(FontSize), 15.0);

    public string? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>
    /// Sets the API client used for fetching link previews.
    /// Should be called during app initialization.
    /// </summary>
    public static void SetApiClient(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    static MessageContentBlock()
    {
        ContentProperty.Changed.AddClassHandler<MessageContentBlock>((x, _) => x.UpdateContent());
        FontSizeProperty.Changed.AddClassHandler<MessageContentBlock>((x, _) => x.UpdateContent());
    }

    public MessageContentBlock()
    {
        Spacing = 0;
        Orientation = Orientation.Vertical;
        HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    private void UpdateContent()
    {
        Children.Clear();

        if (string.IsNullOrEmpty(Content))
            return;

        var trimmedContent = Content.Trim();

        // Check if the entire message is a Tenor GIF URL
        if (TenorGifRegex.IsMatch(trimmedContent))
        {
            // Display the GIF inline
            DisplayInlineGif(trimmedContent);
            return;
        }

        // Add the markdown text block
        var markdownBlock = new MarkdownTextBlock
        {
            Markdown = Content,
            FontSize = FontSize
        };
        Children.Add(markdownBlock);

        // Extract URLs and fetch previews
        var urls = MarkdownParser.ExtractUrls(Content);

        // Only show preview for the first URL (to avoid clutter)
        if (urls.Count > 0)
        {
            FetchAndDisplayPreview(urls[0]);
        }
    }

    private void DisplayInlineGif(string url)
    {
        // Create a container for the GIF
        var container = new Border
        {
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            MaxWidth = 300,
            MaxHeight = 300,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Color.Parse("#2f3136"))
        };

        var image = new Image
        {
            Stretch = Stretch.Uniform,
            MaxWidth = 300,
            MaxHeight = 300
        };

        container.Child = image;
        Children.Add(container);

        // Load the GIF
        LoadGifAsync(url, image, container);
    }

    private async void LoadGifAsync(string url, Image imageControl, Border container)
    {
        // Check cache first
        lock (_cacheLock)
        {
            if (_gifCache.TryGetValue(url, out var cachedBitmap))
            {
                if (cachedBitmap != null)
                {
                    imageControl.Source = cachedBitmap;
                    container.Background = null;
                }
                return;
            }
        }

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            var bytes = await client.GetByteArrayAsync(url);
            using var stream = new MemoryStream(bytes);

            var bitmap = new Bitmap(stream);

            // Cache the bitmap
            lock (_cacheLock)
            {
                // Limit cache size
                if (_gifCache.Count > 100)
                {
                    var keysToRemove = _gifCache.Keys.Take(50).ToList();
                    foreach (var key in keysToRemove)
                    {
                        _gifCache.Remove(key);
                    }
                }
                _gifCache[url] = bitmap;
            }

            // Update UI on main thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                imageControl.Source = bitmap;
                container.Background = null;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load GIF: {ex.Message}");

            lock (_cacheLock)
            {
                _gifCache[url] = null;
            }
        }
    }

    private void FetchAndDisplayPreview(string url)
    {
        // Check cache first
        lock (_cacheLock)
        {
            if (_previewCache.TryGetValue(url, out var cachedPreview))
            {
                if (cachedPreview != null)
                {
                    AddPreviewCard(cachedPreview);
                }
                return;
            }

            // Check if request is already pending
            if (_pendingRequests.Contains(url))
            {
                // Schedule a check later
                SchedulePreviewCheck(url);
                return;
            }

            _pendingRequests.Add(url);
        }

        // Fetch preview asynchronously
        _ = FetchPreviewAsync(url);
    }

    private async Task FetchPreviewAsync(string url)
    {
        if (_apiClient == null)
        {
            lock (_cacheLock)
            {
                _pendingRequests.Remove(url);
                _previewCache[url] = null;
            }
            return;
        }

        try
        {
            var result = await _apiClient.GetLinkPreviewAsync(url);

            lock (_cacheLock)
            {
                _pendingRequests.Remove(url);
                _previewCache[url] = result.Success ? result.Data : null;
            }

            if (result.Success && result.Data != null)
            {
                // Update UI on the UI thread
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Only add if this control is still showing the same content
                    if (Content?.Contains(url) == true)
                    {
                        AddPreviewCard(result.Data);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to fetch link preview: {ex.Message}");

            lock (_cacheLock)
            {
                _pendingRequests.Remove(url);
                _previewCache[url] = null;
            }
        }
    }

    private void SchedulePreviewCheck(string url)
    {
        _ = Task.Run(async () =>
        {
            // Wait a bit for the pending request to complete
            await Task.Delay(500);

            lock (_cacheLock)
            {
                if (_previewCache.TryGetValue(url, out var preview) && preview != null)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (Content?.Contains(url) == true)
                        {
                            AddPreviewCard(preview);
                        }
                    });
                }
            }
        });
    }

    private void AddPreviewCard(LinkPreview preview)
    {
        // Avoid adding duplicate preview cards
        foreach (var child in Children)
        {
            if (child is LinkPreviewCard existingCard && existingCard.Preview?.Url == preview.Url)
                return;
        }

        var previewCard = new LinkPreviewCard
        {
            Preview = preview
        };
        Children.Add(previewCard);
    }

    /// <summary>
    /// Clears the preview cache. Useful when user wants to refresh previews.
    /// </summary>
    public static void ClearCache()
    {
        lock (_cacheLock)
        {
            _previewCache.Clear();
        }
    }
}
