using System.Reactive.Linq;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using Miscord.Client.ViewModels;

namespace Miscord.Client.Views;

public partial class DirectMessagesView : ReactiveUserControl<DirectMessagesViewModel>
{
    public DirectMessagesView()
    {
        InitializeComponent();
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
