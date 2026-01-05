using Avalonia.ReactiveUI;
using Miscord.Client.ViewModels;

namespace Miscord.Client.Views;

public partial class LoadingView : ReactiveUserControl<LoadingViewModel>
{
    public LoadingView()
    {
        InitializeComponent();
    }
}
