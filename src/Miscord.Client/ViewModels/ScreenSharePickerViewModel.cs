using System.Collections.ObjectModel;
using System.Windows.Input;
using ReactiveUI;
using Miscord.Client.Services;

namespace Miscord.Client.ViewModels;

public class ScreenSharePickerViewModel : ViewModelBase
{
    private readonly IScreenCaptureService _screenCaptureService;
    private readonly Action<ScreenShareSettings?> _onComplete;

    private bool _showDisplays = true;
    private bool _showWindows;
    private ScreenCaptureSource? _selectedSource;
    private ScreenShareResolution _selectedResolution;
    private ScreenShareFramerate _selectedFramerate;

    public ScreenSharePickerViewModel(IScreenCaptureService screenCaptureService, Action<ScreenShareSettings?> onComplete)
    {
        _screenCaptureService = screenCaptureService;
        _onComplete = onComplete;

        Displays = new ObservableCollection<ScreenCaptureSource>();
        Windows = new ObservableCollection<ScreenCaptureSource>();

        // Default to 1080p @ 30fps (good balance for most use cases)
        _selectedResolution = ScreenShareResolution.HD1080;
        _selectedFramerate = ScreenShareFramerate.Fps30;

        ShareCommand = ReactiveCommand.Create(OnShare);
        CancelCommand = ReactiveCommand.Create(OnCancel);
        RefreshCommand = ReactiveCommand.Create(RefreshSources);

        // Load sources
        RefreshSources();
    }

    public ObservableCollection<ScreenCaptureSource> Displays { get; }
    public ObservableCollection<ScreenCaptureSource> Windows { get; }

    // Resolution and framerate options
    public IReadOnlyList<ScreenShareResolution> Resolutions => ScreenShareResolution.All;
    public IReadOnlyList<ScreenShareFramerate> Framerates => ScreenShareFramerate.All;

    public ScreenShareResolution SelectedResolution
    {
        get => _selectedResolution;
        set => this.RaiseAndSetIfChanged(ref _selectedResolution, value);
    }

    public ScreenShareFramerate SelectedFramerate
    {
        get => _selectedFramerate;
        set => this.RaiseAndSetIfChanged(ref _selectedFramerate, value);
    }

    public bool ShowDisplays
    {
        get => _showDisplays;
        set
        {
            this.RaiseAndSetIfChanged(ref _showDisplays, value);
            if (value)
            {
                ShowWindows = false;
            }
        }
    }

    public bool ShowWindows
    {
        get => _showWindows;
        set
        {
            this.RaiseAndSetIfChanged(ref _showWindows, value);
            if (value)
            {
                ShowDisplays = false;
            }
        }
    }

    public ScreenCaptureSource? SelectedSource
    {
        get => _selectedSource;
        set => this.RaiseAndSetIfChanged(ref _selectedSource, value);
    }

    public bool HasWindows => Windows.Count > 0;

    public ICommand ShareCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand RefreshCommand { get; }

    private void RefreshSources()
    {
        Displays.Clear();
        Windows.Clear();

        foreach (var display in _screenCaptureService.GetDisplays())
        {
            Displays.Add(display);
        }

        foreach (var window in _screenCaptureService.GetWindows())
        {
            Windows.Add(window);
        }

        // Select first display by default
        if (Displays.Count > 0)
        {
            SelectedSource = Displays[0];
        }

        this.RaisePropertyChanged(nameof(HasWindows));
    }

    private void OnShare()
    {
        if (SelectedSource != null)
        {
            var settings = new ScreenShareSettings(SelectedSource, SelectedResolution, SelectedFramerate);
            _onComplete(settings);
        }
        else
        {
            _onComplete(null);
        }
    }

    private void OnCancel()
    {
        _onComplete(null);
    }
}
