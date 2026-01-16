using System.Collections.ObjectModel;
using System.Windows.Input;
using Snacka.Client.Services;
using ReactiveUI;

namespace Snacka.Client.ViewModels;

/// <summary>
/// Wrapper for controller device selection, allowing a "None" option.
/// </summary>
public record ControllerDeviceItem(ControllerDevice? Device, string DisplayName, string DisplayManufacturer)
{
    public static ControllerDeviceItem None => new(null, "None", "No controller selected");
    public static ControllerDeviceItem FromDevice(ControllerDevice device) => new(device, device.Name, device.Manufacturer);
}

public class ControllerSettingsViewModel : ViewModelBase, IDisposable
{
    private readonly IControllerService _controllerService;
    private readonly ISettingsStore _settingsStore;
    private ControllerDeviceItem? _selectedControllerItem;

    public ControllerSettingsViewModel(IControllerService controllerService, ISettingsStore settingsStore)
    {
        _controllerService = controllerService;
        _settingsStore = settingsStore;

        ControllerItems = new ObservableCollection<ControllerDeviceItem>();
        ControllerItems.Add(ControllerDeviceItem.None);

        RefreshCommand = ReactiveCommand.Create(Refresh);
        StartTestCommand = ReactiveCommand.Create(StartTest);
        StopTestCommand = ReactiveCommand.Create(StopTest);

        // Build initial list and sync selection with service
        RefreshControllerItems();
        _selectedControllerItem = _controllerService.SelectedController != null
            ? ControllerItems.FirstOrDefault(c => c.Device?.Id == _controllerService.SelectedController.Id)
            : ControllerDeviceItem.None;
    }

    public ObservableCollection<ControllerDeviceItem> ControllerItems { get; }

    public ObservableCollection<ControllerDevice> AvailableControllers =>
        _controllerService.AvailableControllers;

    public ControllerDeviceItem? SelectedControllerItem
    {
        get => _selectedControllerItem ?? ControllerDeviceItem.None;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedControllerItem, value);
            _controllerService.SelectedController = value?.Device;
            this.RaisePropertyChanged(nameof(SelectedController));
        }
    }

    public ControllerDevice? SelectedController => _selectedControllerItem?.Device;

    public ControllerState CurrentState => _controllerService.CurrentState;

    public bool IsReading => _controllerService.IsReading;

    public bool RumbleEnabled
    {
        get => _settingsStore.Settings.ControllerRumbleEnabled;
        set
        {
            if (_settingsStore.Settings.ControllerRumbleEnabled != value)
            {
                _settingsStore.Settings.ControllerRumbleEnabled = value;
                _settingsStore.Save();
                this.RaisePropertyChanged();
            }
        }
    }

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
        RefreshControllerItems();
        this.RaisePropertyChanged(nameof(AvailableControllers));
    }

    private void RefreshControllerItems()
    {
        ControllerItems.Clear();
        ControllerItems.Add(ControllerDeviceItem.None);

        foreach (var device in _controllerService.AvailableControllers)
        {
            ControllerItems.Add(ControllerDeviceItem.FromDevice(device));
        }

        this.RaisePropertyChanged(nameof(ControllerItems));
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
