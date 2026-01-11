using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using Snacka.Client.ViewModels;

namespace Snacka.Client.Views;

public partial class DirectMessagesView : ReactiveUserControl<DirectMessagesViewModel>
{
    public DirectMessagesView()
    {
        InitializeComponent();
    }

    // Formatting toolbar handlers
    private void OnBoldClick(object? sender, RoutedEventArgs e) => WrapSelectionWith("**");
    private void OnItalicClick(object? sender, RoutedEventArgs e) => WrapSelectionWith("*");
    private void OnCodeClick(object? sender, RoutedEventArgs e) => WrapSelectionWith("`");

    private void WrapSelectionWith(string wrapper)
    {
        var textBox = this.FindControl<TextBox>("MessageInputBox");
        if (textBox == null || ViewModel == null) return;

        var text = ViewModel.MessageInput ?? "";
        var selStart = textBox.SelectionStart;
        var selEnd = textBox.SelectionEnd;

        if (selStart > selEnd)
            (selStart, selEnd) = (selEnd, selStart);

        var selectedText = selEnd > selStart ? text.Substring(selStart, selEnd - selStart) : "";

        if (string.IsNullOrEmpty(selectedText))
        {
            // No selection - insert wrapper pair and place cursor between them
            var newText = text.Insert(selStart, wrapper + wrapper);
            ViewModel.MessageInput = newText;
            textBox.SelectionStart = selStart + wrapper.Length;
            textBox.SelectionEnd = selStart + wrapper.Length;
        }
        else
        {
            // Wrap the selected text
            var newText = text.Substring(0, selStart) + wrapper + selectedText + wrapper + text.Substring(selEnd);
            ViewModel.MessageInput = newText;
            textBox.SelectionStart = selStart;
            textBox.SelectionEnd = selEnd + wrapper.Length * 2;
        }

        textBox.Focus();
    }

    // Called from XAML for message input TextBox
    // Enter sends message, Shift+Enter inserts newline
    public void OnMessageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            // Shift+Enter = newline, let it through
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                return;

            // Enter only = send message
            if (ViewModel?.SendMessageCommand.CanExecute.FirstAsync().GetAwaiter().GetResult() == true)
            {
                ViewModel.SendMessageCommand.Execute().Subscribe();
            }
            e.Handled = true;
        }
    }

    // Called from XAML for message edit TextBox
    // Enter saves edit, Shift+Enter inserts newline
    public void OnEditMessageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            // Shift+Enter = newline, let it through
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                return;

            // Enter only = save edit
            if (ViewModel?.SaveMessageEditCommand.CanExecute.FirstAsync().GetAwaiter().GetResult() == true)
            {
                ViewModel.SaveMessageEditCommand.Execute().Subscribe();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ViewModel?.CancelEditMessageCommand.Execute().Subscribe();
            e.Handled = true;
        }
    }
}
