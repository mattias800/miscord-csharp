using System.Reactive.Linq;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using Snacka.Client.ViewModels;

namespace Snacka.Client.Views;

public partial class RegisterView : ReactiveUserControl<RegisterViewModel>
{
    public RegisterView()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ViewModel?.RegisterCommand.CanExecute.FirstAsync().GetAwaiter().GetResult() == true)
        {
            ViewModel.RegisterCommand.Execute().Subscribe();
        }
    }
}
