using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using Avalonia.VisualTree;
using Miscord.Client.ViewModels;

namespace Miscord.Client.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();

        // Global keyboard shortcut handler at Window level for Cmd+K / Ctrl+K
        this.AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.ImageFilePickerProvider = SelectImageFileAsync;
            }
        };
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        var cmdOrCtrl = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

        // Handle Cmd+K or Cmd+T (Mac) / Ctrl+K or Ctrl+T (Windows/Linux) for quick switcher
        if ((e.Key == Key.K || e.Key == Key.T) && e.KeyModifiers == cmdOrCtrl)
        {
            var mainAppView = this.FindDescendantOfType<MainAppView>();
            mainAppView?.OpenQuickSwitcher();
            e.Handled = true;
        }
        // Handle Cmd+F (Mac) or Ctrl+F (Windows/Linux) for message search
        else if (e.Key == Key.F && e.KeyModifiers == cmdOrCtrl)
        {
            var mainAppView = this.FindDescendantOfType<MainAppView>();
            mainAppView?.OpenMessageSearch();
            e.Handled = true;
        }
    }

    private async Task<IStorageFile?> SelectImageFileAsync()
    {
        var topLevel = GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
            return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select an image",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp"],
                    MimeTypes = ["image/png", "image/jpeg", "image/webp"]
                }
            ]
        });

        return files.Count > 0 ? files[0] : null;
    }
}
