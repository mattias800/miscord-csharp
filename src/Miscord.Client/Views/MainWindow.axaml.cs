using Avalonia.ReactiveUI;
using Miscord.Client.ViewModels;

namespace Miscord.Client.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
