using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Miscord.Client.Services;
using Miscord.Client.ViewModels;

namespace Miscord.Client.Controls;

/// <summary>
/// Content for the message search popup.
/// </summary>
public partial class MessageSearchContent : UserControl
{
    public static readonly StyledProperty<MessageSearchViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<MessageSearchContent, MessageSearchViewModel?>(nameof(ViewModel));

    private TextBox? _searchInput;

    public MessageSearchContent()
    {
        InitializeComponent();

        // Focus search input when control is attached
        AttachedToVisualTree += OnAttachedToVisualTree;
        KeyDown += OnKeyDown;
    }

    public MessageSearchViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _searchInput = this.FindControl<TextBox>("SearchInput");
        if (_searchInput is not null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _searchInput.Focus();
                _searchInput.SelectAll();
            }, DispatcherPriority.Input);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is null) return;

        switch (e.Key)
        {
            case Key.Up:
                ViewModel.MoveUp();
                e.Handled = true;
                break;

            case Key.Down:
                ViewModel.MoveDown();
                e.Handled = true;
                break;

            case Key.Enter:
                if (ViewModel.Results.Count > 0)
                {
                    ViewModel.SelectCurrent();
                }
                e.Handled = true;
                break;

            case Key.Escape:
                ViewModel.Close();
                e.Handled = true;
                break;
        }
    }

    private void OnResultItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: MessageSearchResult result } && ViewModel is not null)
        {
            ViewModel.SelectResult(result);
            e.Handled = true;
        }
    }
}
