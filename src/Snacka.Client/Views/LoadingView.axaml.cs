using Avalonia.ReactiveUI;
using Snacka.Client.ViewModels;

namespace Snacka.Client.Views;

public partial class LoadingView : ReactiveUserControl<LoadingViewModel>
{
    public LoadingView()
    {
        InitializeComponent();
    }
}
