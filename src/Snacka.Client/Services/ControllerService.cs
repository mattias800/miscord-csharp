using System.Collections.ObjectModel;
using HidSharp;
using ReactiveUI;

namespace Snacka.Client.Services;

/// <summary>
/// Represents a game controller device.
/// </summary>
public record ControllerDevice(
    string Id,
    string Name,
    string Manufacturer,
    int VendorId,
    int ProductId,
    HidDevice? HidDevice = null
);

/// <summary>
/// Represents the current state of a controller's inputs.
/// </summary>
public class ControllerState : ReactiveObject
{
    private readonly float[] _axes = new float[8];
    private readonly bool[] _buttons = new bool[32];
    private int _hatSwitch = -1;

    public float[] Axes => _axes;
    public bool[] Buttons => _buttons;

    public int HatSwitch
    {
        get => _hatSwitch;
        set => this.RaiseAndSetIfChanged(ref _hatSwitch, value);
    }

    public void SetAxis(int index, float value)
    {
        if (index >= 0 && index < _axes.Length)
        {
            _axes[index] = value;
            this.RaisePropertyChanged(nameof(Axes));
        }
    }

    public void SetButton(int index, bool pressed)
    {
        if (index >= 0 && index < _buttons.Length)
        {
            _buttons[index] = pressed;
            this.RaisePropertyChanged(nameof(Buttons));
        }
    }

    public void Reset()
    {
        Array.Fill(_axes, 0f);
        Array.Fill(_buttons, false);
        _hatSwitch = -1;
        this.RaisePropertyChanged(nameof(Axes));
        this.RaisePropertyChanged(nameof(Buttons));
        this.RaisePropertyChanged(nameof(HatSwitch));
    }
}

/// <summary>
/// Service for enumerating and reading game controller input via HID.
/// </summary>
public interface IControllerService : IDisposable
{
    ObservableCollection<ControllerDevice> AvailableControllers { get; }
    ControllerDevice? SelectedController { get; set; }
    ControllerState CurrentState { get; }
    bool IsReading { get; }

    void RefreshControllers();
    void StartReading();
    void StopReading();
}

public class ControllerService : ReactiveObject, IControllerService
{
    private readonly ObservableCollection<ControllerDevice> _availableControllers = new();
    private ControllerDevice? _selectedController;
    private readonly ControllerState _currentState = new();
    private bool _isReading;
    private HidStream? _currentStream;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;

    // Common game controller usage pages
    private const int GenericDesktopPage = 0x01;
    private const int GameControlsPage = 0x05;

    // Common game controller usages
    private const int JoystickUsage = 0x04;
    private const int GamePadUsage = 0x05;
    private const int MultiAxisControllerUsage = 0x08;

    public ControllerService()
    {
        RefreshControllers();
    }

    public ObservableCollection<ControllerDevice> AvailableControllers => _availableControllers;

    public ControllerDevice? SelectedController
    {
        get => _selectedController;
        set
        {
            if (_selectedController != value)
            {
                StopReading();
                this.RaiseAndSetIfChanged(ref _selectedController, value);
            }
        }
    }

    public ControllerState CurrentState => _currentState;

    public bool IsReading
    {
        get => _isReading;
        private set => this.RaiseAndSetIfChanged(ref _isReading, value);
    }

    public void RefreshControllers()
    {
        var wasReading = IsReading;
        var previousSelection = _selectedController?.Id;

        StopReading();
        _availableControllers.Clear();

        try
        {
            var devices = DeviceList.Local.GetHidDevices();

            foreach (var device in devices)
            {
                try
                {
                    // Check if this is a game controller by examining usage page and usage
                    var reportDescriptor = device.GetReportDescriptor();
                    var isGameController = false;

                    foreach (var deviceItem in reportDescriptor.DeviceItems)
                    {
                        foreach (var usage in deviceItem.Usages.GetAllValues())
                        {
                            var page = (usage >> 16) & 0xFFFF;
                            var usageId = usage & 0xFFFF;

                            if (page == GenericDesktopPage &&
                                (usageId == JoystickUsage || usageId == GamePadUsage || usageId == MultiAxisControllerUsage))
                            {
                                isGameController = true;
                                break;
                            }

                            if (page == GameControlsPage)
                            {
                                isGameController = true;
                                break;
                            }
                        }

                        if (isGameController) break;
                    }

                    if (isGameController)
                    {
                        var controller = new ControllerDevice(
                            $"{device.VendorID:X4}:{device.ProductID:X4}",
                            device.GetProductName() ?? $"Controller {device.ProductID:X4}",
                            device.GetManufacturer() ?? "Unknown",
                            device.VendorID,
                            device.ProductID,
                            device
                        );

                        _availableControllers.Add(controller);
                        Console.WriteLine($"ControllerService: Found controller: {controller.Name} ({controller.Id})");
                    }
                }
                catch (Exception ex)
                {
                    // Skip devices that fail to query
                    Console.WriteLine($"ControllerService: Failed to query device: {ex.Message}");
                }
            }

            Console.WriteLine($"ControllerService: Found {_availableControllers.Count} game controllers");

            // Restore previous selection if still available
            if (previousSelection != null)
            {
                var restored = _availableControllers.FirstOrDefault(c => c.Id == previousSelection);
                if (restored != null)
                {
                    _selectedController = restored;
                    this.RaisePropertyChanged(nameof(SelectedController));

                    if (wasReading)
                    {
                        StartReading();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ControllerService: Failed to enumerate controllers: {ex.Message}");
        }
    }

    public void StartReading()
    {
        if (_selectedController?.HidDevice == null || IsReading)
            return;

        try
        {
            _currentStream = _selectedController.HidDevice.Open();
            _readCts = new CancellationTokenSource();
            IsReading = true;

            _readTask = Task.Run(() => ReadLoop(_readCts.Token));
            Console.WriteLine($"ControllerService: Started reading from {_selectedController.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ControllerService: Failed to start reading: {ex.Message}");
            IsReading = false;
        }
    }

    public void StopReading()
    {
        if (!IsReading)
            return;

        _readCts?.Cancel();

        try
        {
            _readTask?.Wait(TimeSpan.FromMilliseconds(500));
        }
        catch { }

        _currentStream?.Dispose();
        _currentStream = null;
        _readCts?.Dispose();
        _readCts = null;
        _readTask = null;

        _currentState.Reset();
        IsReading = false;

        Console.WriteLine("ControllerService: Stopped reading");
    }

    private void ReadLoop(CancellationToken cancellationToken)
    {
        if (_currentStream == null || _selectedController?.HidDevice == null)
            return;

        var device = _selectedController.HidDevice;
        var buffer = new byte[device.GetMaxInputReportLength()];

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var bytesRead = _currentStream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    ParseInputReport(buffer, bytesRead);
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine($"ControllerService: Read error: {ex.Message}");
                }
                break;
            }
        }
    }

    private void ParseInputReport(byte[] data, int length)
    {
        // Use simple byte parsing which works reliably for most controllers
        // The HID descriptor parsing is complex and varies by controller
        ParseSimpleReport(data, length);
    }

    private void ParseSimpleReport(byte[] data, int length)
    {
        // Fallback simple parsing for common controller formats
        // This handles controllers that don't have proper HID descriptors

        if (length < 4)
            return;

        // Many controllers use bytes 1-4 for axes (skip report ID in byte 0)
        var offset = 1;

        // X and Y axes (common format)
        if (length > offset + 1)
        {
            _currentState.SetAxis(0, (data[offset] - 128) / 127f);      // X
            _currentState.SetAxis(1, (data[offset + 1] - 128) / 127f);  // Y
        }

        // Z and Rz axes
        if (length > offset + 3)
        {
            _currentState.SetAxis(2, (data[offset + 2] - 128) / 127f);  // Z
            _currentState.SetAxis(5, (data[offset + 3] - 128) / 127f);  // Rz
        }

        // Buttons typically in bytes 5-6
        if (length > offset + 5)
        {
            var buttons1 = data[offset + 4];
            var buttons2 = data[offset + 5];

            for (int i = 0; i < 8; i++)
            {
                _currentState.SetButton(i, (buttons1 & (1 << i)) != 0);
                _currentState.SetButton(i + 8, (buttons2 & (1 << i)) != 0);
            }
        }
    }

    public void Dispose()
    {
        StopReading();
    }
}
