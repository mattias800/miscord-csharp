using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Snacka.Client.Services;
using Snacka.Shared.Models;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Snacka.Client.Controls;

/// <summary>
/// A control that displays a link preview card with title, description, and image.
/// </summary>
public class LinkPreviewCard : Border
{
    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#2f3136"));
    private static readonly IBrush CardBorderBrush = new SolidColorBrush(Color.Parse("#202225"));
    private static readonly IBrush AccentBrush = new SolidColorBrush(Color.Parse("#5865f2"));
    private static readonly IBrush TitleBrush = new SolidColorBrush(Color.Parse("#00aff4"));
    private static readonly IBrush DescriptionBrush = new SolidColorBrush(Color.Parse("#b9bbbe"));
    private static readonly IBrush SiteNameBrush = new SolidColorBrush(Color.Parse("#72767d"));

    // Cache for loaded images to prevent reloading during scroll
    private static readonly Dictionary<string, Bitmap> ImageCache = new();
    private static readonly HashSet<string> LoadingImages = new();
    private static readonly object CacheLock = new();

    public static readonly StyledProperty<LinkPreview?> PreviewProperty =
        AvaloniaProperty.Register<LinkPreviewCard, LinkPreview?>(nameof(Preview));

    public LinkPreview? Preview
    {
        get => GetValue(PreviewProperty);
        set => SetValue(PreviewProperty, value);
    }

    static LinkPreviewCard()
    {
        PreviewProperty.Changed.AddClassHandler<LinkPreviewCard>((x, _) => x.UpdateContent());
    }

    public LinkPreviewCard()
    {
        Background = BackgroundBrush;
        base.BorderBrush = CardBorderBrush;
        BorderThickness = new Thickness(0, 0, 0, 0);
        CornerRadius = new CornerRadius(4);
        Margin = new Thickness(0, 8, 0, 0);
        MaxWidth = 400;
        HorizontalAlignment = HorizontalAlignment.Left;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (Preview?.Url == null)
            return;

        var point = e.GetCurrentPoint(this);

        // Right-click: show context menu
        if (point.Properties.IsRightButtonPressed)
        {
            ShowLinkContextMenu(Preview.Url);
            e.Handled = true;
        }
        // Left-click: open link
        else if (point.Properties.IsLeftButtonPressed)
        {
            OpenUrl(Preview.Url);
            e.Handled = true;
        }
    }

    private void OpenUrl(string url)
    {
        try
        {
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
    /// Shows a context menu for a link with Open and Copy options.
    /// </summary>
    private void ShowLinkContextMenu(string url)
    {
        var contextMenu = new ContextMenu();

        var openItem = new MenuItem { Header = "Open link" };
        openItem.Click += (_, _) => OpenUrl(url);

        var copyItem = new MenuItem { Header = "Copy link" };
        copyItem.Click += async (_, _) => await CopyToClipboard(url);

        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(copyItem);

        contextMenu.Open(this);
    }

    /// <summary>
    /// Copies text to the system clipboard.
    /// </summary>
    private async Task CopyToClipboard(string text)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard != null)
        {
            await topLevel.Clipboard.SetTextAsync(text);
        }
    }

    private void UpdateContent()
    {
        Child = null;

        if (Preview == null)
            return;

        var mainPanel = new DockPanel
        {
            LastChildFill = true
        };

        // Left accent bar
        var accentBar = new Border
        {
            Width = 4,
            Background = AccentBrush,
            CornerRadius = new CornerRadius(4, 0, 0, 4)
        };
        DockPanel.SetDock(accentBar, Dock.Left);
        mainPanel.Children.Add(accentBar);

        // Content panel
        var contentPanel = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(12, 10)
        };

        // Site name
        if (!string.IsNullOrEmpty(Preview.SiteName))
        {
            var siteNameText = new TextBlock
            {
                Text = Preview.SiteName.ToUpperInvariant(),
                Foreground = SiteNameBrush,
                FontSize = 11,
                FontWeight = FontWeight.Medium
            };
            contentPanel.Children.Add(siteNameText);
        }

        // Title
        if (!string.IsNullOrEmpty(Preview.Title))
        {
            var titleText = new TextBlock
            {
                Text = Preview.Title,
                Foreground = TitleBrush,
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 350
            };
            contentPanel.Children.Add(titleText);
        }

        // Description
        if (!string.IsNullOrEmpty(Preview.Description))
        {
            var descText = new TextBlock
            {
                Text = TruncateDescription(Preview.Description, 150),
                Foreground = DescriptionBrush,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 350
            };
            contentPanel.Children.Add(descText);
        }

        // Image (if available, display below the text)
        if (!string.IsNullOrEmpty(Preview.ImageUrl))
        {
            var imageContainer = new Border
            {
                Margin = new Thickness(0, 8, 0, 0),
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                MaxWidth = 350,
                MaxHeight = 200
            };

            // Load image asynchronously
            LoadImageAsync(Preview.ImageUrl, imageContainer);

            contentPanel.Children.Add(imageContainer);
        }

        mainPanel.Children.Add(contentPanel);
        Child = mainPanel;
    }

    private static string TruncateDescription(string description, int maxLength)
    {
        if (description.Length <= maxLength)
            return description;

        return description[..(maxLength - 3)] + "...";
    }

    private static void LoadImageAsync(string imageUrl, Border container)
    {
        // Check cache first - if cached, display immediately without async
        lock (CacheLock)
        {
            if (ImageCache.TryGetValue(imageUrl, out var cachedBitmap))
            {
                // Use cached image immediately (synchronously)
                container.Child = new Image
                {
                    Source = cachedBitmap,
                    Stretch = Stretch.Uniform,
                    MaxWidth = 350,
                    MaxHeight = 200
                };
                return;
            }

            // Check if already loading
            if (LoadingImages.Contains(imageUrl))
            {
                // Wait for the other load to complete, then use cache
                _ = WaitForCacheAndDisplayAsync(imageUrl, container);
                return;
            }

            // Mark as loading
            LoadingImages.Add(imageUrl);
        }

        // Start async load
        _ = LoadImageFromNetworkAsync(imageUrl, container);
    }

    private static async Task LoadImageFromNetworkAsync(string imageUrl, Border container)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; SnackaBot/1.0)");

            var imageData = await httpClient.GetByteArrayAsync(imageUrl);
            using var stream = new MemoryStream(imageData);

            var bitmap = new Bitmap(stream);

            // Cache the bitmap
            lock (CacheLock)
            {
                ImageCache[imageUrl] = bitmap;
                LoadingImages.Remove(imageUrl);
            }

            // Create the image on the UI thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                container.Child = new Image
                {
                    Source = bitmap,
                    Stretch = Stretch.Uniform,
                    MaxWidth = 350,
                    MaxHeight = 200
                };
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load preview image: {ex.Message}");
            lock (CacheLock)
            {
                LoadingImages.Remove(imageUrl);
            }
        }
    }

    private static async Task WaitForCacheAndDisplayAsync(string imageUrl, Border container)
    {
        // Wait up to 10 seconds for the image to be cached
        for (int i = 0; i < 100; i++)
        {
            await Task.Delay(100);

            lock (CacheLock)
            {
                if (ImageCache.TryGetValue(imageUrl, out var bitmap))
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        container.Child = new Image
                        {
                            Source = bitmap,
                            Stretch = Stretch.Uniform,
                            MaxWidth = 350,
                            MaxHeight = 200
                        };
                    });
                    return;
                }

                // If no longer loading and not in cache, give up
                if (!LoadingImages.Contains(imageUrl))
                    return;
            }
        }
    }
}
