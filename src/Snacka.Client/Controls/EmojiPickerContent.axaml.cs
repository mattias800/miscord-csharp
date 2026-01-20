using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Snacka.Client.Services;

namespace Snacka.Client.Controls;

/// <summary>
/// Content for emoji picker popup showing reaction emojis.
/// </summary>
public partial class EmojiPickerContent : UserControl
{
    public EmojiPickerContent()
    {
        InitializeComponent();

        // On Linux, set the emoji font for proper color emoji rendering
        if (EmojiRenderingService.IsLinux)
        {
            Loaded += OnLoaded;
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Find all TextBlocks in the emoji buttons and set the font
        SetEmojiFontRecursive(this);
    }

    private void SetEmojiFontRecursive(Control control)
    {
        if (control is TextBlock textBlock)
        {
            textBlock.FontFamily = EmojiRenderingService.GetEmojiFontFamily();
        }

        if (control is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Control childControl)
                {
                    SetEmojiFontRecursive(childControl);
                }
            }
        }
        else if (control is ContentControl contentControl && contentControl.Content is Control content)
        {
            SetEmojiFontRecursive(content);
        }
        else if (control is Decorator decorator && decorator.Child is Control decoratorChild)
        {
            SetEmojiFontRecursive(decoratorChild);
        }
    }

    // Event for when an emoji is selected
    public event EventHandler<string>? EmojiSelected;

    private void OnEmojiClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string emoji)
        {
            EmojiSelected?.Invoke(this, emoji);
        }
    }
}
