using System.Collections.ObjectModel;
using System.Windows.Input;
using Snacka.Client.Services;
using ReactiveUI;

namespace Snacka.Client.ViewModels;

public class ControllerSettingsViewModel : ViewModelBase, IDisposable
{
    private readonly IControllerService _controllerService;
    private ControllerDevice? _selectedController;

    public ControllerSettingsViewModel(IControllerService controllerService)
    {
        _controllerService = controllerService;

        RefreshCommand = ReactiveCommand.Create(Refresh);
        StartTestCommand = ReactiveCommand.Create(StartTest);
        StopTestCommand = ReactiveCommand.Create(StopTest);

        // Sync selection with service
        _selectedController = _controllerService.SelectedController;
    }

    public ObservableCollection<ControllerDevice> AvailableControllers =>
        _controllerService.AvailableControllers;

    public ControllerDevice? SelectedController
    {
        get => _selectedController;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedController, value);
            _controllerService.SelectedController = value;
        }
    }

    public ControllerState CurrentState => _controllerService.CurrentState;

    public bool IsReading => _controllerService.IsReading;

    // Expose individual axes for easier binding
    public float AxisX => _controllerService.CurrentState.Axes[0];
    public float AxisY => _controllerService.CurrentState.Axes[1];
    public float AxisZ => _controllerService.CurrentState.Axes[2];
    public float AxisRx => _controllerService.CurrentState.Axes[3];
    public float AxisRy => _controllerService.CurrentState.Axes[4];
    public float AxisRz => _controllerService.CurrentState.Axes[5];

    public ICommand RefreshCommand { get; }
    public ICommand StartTestCommand { get; }
    public ICommand StopTestCommand { get; }

    private void Refresh()
    {
        _controllerService.RefreshControllers();
        this.RaisePropertyChanged(nameof(AvailableControllers));
    }

    private void StartTest()
    {
        _controllerService.StartReading();
        this.RaisePropertyChanged(nameof(IsReading));

        // Subscribe to state changes to update UI
        _controllerService.CurrentState.PropertyChanged += OnStateChanged;
    }

    private void StopTest()
    {
        _controllerService.CurrentState.PropertyChanged -= OnStateChanged;
        _controllerService.StopReading();
        this.RaisePropertyChanged(nameof(IsReading));
        NotifyAxesChanged();
    }

    private void OnStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Update all axis properties when state changes
        Avalonia.Threading.Dispatcher.UIThread.Post(NotifyAxesChanged);
    }

    private void NotifyAxesChanged()
    {
        this.RaisePropertyChanged(nameof(AxisX));
        this.RaisePropertyChanged(nameof(AxisY));
        this.RaisePropertyChanged(nameof(AxisZ));
        this.RaisePropertyChanged(nameof(AxisRx));
        this.RaisePropertyChanged(nameof(AxisRy));
        this.RaisePropertyChanged(nameof(AxisRz));
        this.RaisePropertyChanged(nameof(CurrentState));
    }

    public void Dispose()
    {
        _controllerService.CurrentState.PropertyChanged -= OnStateChanged;
        _controllerService.StopReading();
    }
}
