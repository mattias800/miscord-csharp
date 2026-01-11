using System.Reactive.Linq;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using Snacka.Client.ViewModels;

namespace Snacka.Client.Views;

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
