using System.Reactive.Linq;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using Miscord.Client.ViewModels;

namespace Miscord.Client.Views;

public partial class LoginView : ReactiveUserControl<LoginViewModel>
{
    public LoginView()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ViewModel?.LoginCommand.CanExecute.FirstAsync().GetAwaiter().GetResult() == true)
        {
            ViewModel.LoginCommand.Execute().Subscribe();
        }
    }
}
