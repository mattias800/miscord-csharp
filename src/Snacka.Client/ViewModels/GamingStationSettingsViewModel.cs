using ReactiveUI;
using Snacka.Client.Services;

namespace Snacka.Client.ViewModels;

public class GamingStationSettingsViewModel : ViewModelBase
{
    private readonly ISettingsStore _settingsStore;
    private readonly Action? _onSettingsChanged;

    public GamingStationSettingsViewModel(ISettingsStore settingsStore, Action? onSettingsChanged = null)
    {
        _settingsStore = settingsStore;
        _onSettingsChanged = onSettingsChanged;
    }

    /// <summary>
    /// Whether gaming station mode is enabled on this device.
    /// When enabled, this device can be remotely controlled by the owner from another device.
    /// </summary>
    public bool IsGamingStationEnabled
    {
        get => _settingsStore.Settings.IsGamingStationEnabled;
        set
        {
            if (_settingsStore.Settings.IsGamingStationEnabled != value)
            {
                _settingsStore.Settings.IsGamingStationEnabled = value;
                _settingsStore.Save();
                this.RaisePropertyChanged();
                _onSettingsChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// The display name for this gaming station.
    /// </summary>
    public string GamingStationDisplayName
    {
        get => string.IsNullOrEmpty(_settingsStore.Settings.GamingStationDisplayName)
            ? Environment.MachineName
            : _settingsStore.Settings.GamingStationDisplayName;
        set
        {
            var newValue = string.IsNullOrWhiteSpace(value) ? "" : value;
            if (_settingsStore.Settings.GamingStationDisplayName != newValue)
            {
                _settingsStore.Settings.GamingStationDisplayName = newValue;
                _settingsStore.Save();
                this.RaisePropertyChanged();
                _onSettingsChanged?.Invoke();
            }
        }
    }
}
