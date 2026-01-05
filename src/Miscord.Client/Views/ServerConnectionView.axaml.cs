using System.Reactive.Linq;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using Miscord.Client.ViewModels;

namespace Miscord.Client.Views;

public partial class ServerConnectionView : ReactiveUserControl<ServerConnectionViewModel>
{
    public ServerConnectionView()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || ViewModel is null) return;

        if (ViewModel.IsConnected)
        {
            if (ViewModel.ContinueCommand.CanExecute.FirstAsync().GetAwaiter().GetResult())
                ViewModel.ContinueCommand.Execute().Subscribe();
        }
        else
        {
            if (ViewModel.ConnectCommand.CanExecute.FirstAsync().GetAwaiter().GetResult())
                ViewModel.ConnectCommand.Execute().Subscribe();
        }
    }
}
