using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Snacka.Client.Services;

namespace Snacka.Client.Controls;

/// <summary>
/// Control for displaying a GIF thumbnail in the picker grid.
/// </summary>
public class GifPreview : Border
{
    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#2f3136"));
    private static readonly IBrush HoverBrush = new SolidColorBrush(Color.Parse("#40444b"));
    private static readonly IBrush LoadingBrush = new SolidColorBrush(Color.Parse("#36393f"));

    // Cache for loaded images (shared across all GifPreview instances)
    private static readonly Dictionary<string, Bitmap> ImageCache = new();
    private static readonly object CacheLock = new();

    private Image? _image;
    private bool _isLoading;

    public static readonly StyledProperty<GifResult?> GifProperty =
        AvaloniaProperty.Register<GifPreview, GifResult?>(nameof(Gif));

    public GifResult? Gif
    {
        get => GetValue(GifProperty);
        set => SetValue(GifProperty, value);
    }

    /// <summary>
    /// Event raised when the GIF is clicked.
    /// </summary>
    public event EventHandler<GifResult>? GifClicked;

    static GifPreview()
    {
        GifProperty.Changed.AddClassHandler<GifPreview>((x, _) => x.UpdateContent());
    }

    public GifPreview()
    {
        Background = BackgroundBrush;
        CornerRadius = new CornerRadius(4);
        Width = 120;
        Height = 90;
        Margin = new Thickness(4);
        Cursor = new Cursor(StandardCursorType.Hand);
        ClipToBounds = true;

        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        PointerPressed += OnPointerPressed;
    }

    private void UpdateContent()
    {
        Child = null;

        if (Gif is null)
            return;

        // Create image control
        _image = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Show loading placeholder
        Background = LoadingBrush;

        Child = _image;

        // Load image asynchronously
        LoadImageAsync(Gif.PreviewUrl);
    }

    private async void LoadImageAsync(string url)
    {
        if (string.IsNullOrEmpty(url) || _image == null)
            return;

        _isLoading = true;

        // Check cache first
        lock (CacheLock)
        {
            if (ImageCache.TryGetValue(url, out var cachedBitmap))
            {
                _image.Source = cachedBitmap;
                Background = BackgroundBrush;
                _isLoading = false;
                return;
            }
        }

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            var bytes = await client.GetByteArrayAsync(url);
            using var stream = new MemoryStream(bytes);

            var bitmap = new Bitmap(stream);

            // Cache the bitmap
            lock (CacheLock)
            {
                // Limit cache size
                if (ImageCache.Count > 200)
                {
                    // Remove oldest entries (simple approach: clear half)
                    var keysToRemove = ImageCache.Keys.Take(100).ToList();
                    foreach (var key in keysToRemove)
                    {
                        ImageCache.Remove(key);
                    }
                }
                ImageCache[url] = bitmap;
            }

            // Update UI on main thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_image != null)
                {
                    _image.Source = bitmap;
                    Background = BackgroundBrush;
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load GIF preview: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (!_isLoading)
        {
            Background = HoverBrush;
        }
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (!_isLoading)
        {
            Background = BackgroundBrush;
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Gif != null)
        {
            GifClicked?.Invoke(this, Gif);
        }
    }

    /// <summary>
    /// Clears the image cache to free memory.
    /// </summary>
    public static void ClearCache()
    {
        lock (CacheLock)
        {
            ImageCache.Clear();
        }
    }
}
