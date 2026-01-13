using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using Snacka.Client.Services;

namespace Snacka.Client.Controls;

/// <summary>
/// Self-contained ViewModel for the GIF picker component.
/// Handles its own API calls, loading state, and GIF cycling.
/// </summary>
public class GifPickerViewModel : ReactiveObject
{
    private readonly IApiClient _apiClient;

    private bool _isVisible;
    private bool _isLoading;
    private string _query = string.Empty;
    private string? _errorMessage;
    private GifResult? _currentGif;
    private List<GifResult> _results = new();
    private int _currentIndex;

    /// <summary>
    /// Raised when user clicks Send. The GifResult should be sent as a message.
    /// </summary>
    public event Action<GifResult>? SendRequested;

    public GifPickerViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;

        SendCommand = ReactiveCommand.Create(Send);
        NextGifCommand = ReactiveCommand.Create(NextGif);
        CancelCommand = ReactiveCommand.Create(Cancel);
    }

    public ICommand SendCommand { get; }
    public ICommand NextGifCommand { get; }
    public ICommand CancelCommand { get; }

    /// <summary>
    /// Whether the GIF picker is visible.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    /// <summary>
    /// Whether we're currently loading GIFs from the API.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    /// <summary>
    /// The search query being used.
    /// </summary>
    public string Query
    {
        get => _query;
        private set => this.RaiseAndSetIfChanged(ref _query, value);
    }

    /// <summary>
    /// Error message to display (e.g., "No GIFs found").
    /// </summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    /// <summary>
    /// The currently displayed GIF.
    /// </summary>
    public GifResult? CurrentGif
    {
        get => _currentGif;
        private set => this.RaiseAndSetIfChanged(ref _currentGif, value);
    }

    /// <summary>
    /// Starts a new GIF search. Shows the picker and loads results.
    /// </summary>
    public async Task StartSearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return;

        Query = query.Trim();
        _results.Clear();
        _currentIndex = 0;
        CurrentGif = null;
        ErrorMessage = null;
        IsVisible = true;
        IsLoading = true;

        try
        {
            var result = await _apiClient.SearchGifsAsync(Query, 10);

            if (result.Success && result.Data != null && result.Data.Results.Count > 0)
            {
                _results = new List<GifResult>(result.Data.Results);
                _currentIndex = 0;
                CurrentGif = _results[0];
                ErrorMessage = null;
            }
            else
            {
                ErrorMessage = $"No GIFs found for \"{Query}\"";
                CurrentGif = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GIF search failed: {ex.Message}");
            ErrorMessage = "Failed to search for GIFs";
            CurrentGif = null;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Shows the next GIF in the results (cycles back to start).
    /// </summary>
    public void NextGif()
    {
        if (_results.Count == 0)
            return;

        _currentIndex = (_currentIndex + 1) % _results.Count;
        CurrentGif = _results[_currentIndex];
    }

    /// <summary>
    /// Sends the current GIF and closes the picker.
    /// </summary>
    public void Send()
    {
        if (CurrentGif == null)
            return;

        var gif = CurrentGif;
        Cancel();
        SendRequested?.Invoke(gif);
    }

    /// <summary>
    /// Cancels the GIF picker without sending.
    /// </summary>
    public void Cancel()
    {
        IsVisible = false;
        IsLoading = false;
        Query = string.Empty;
        CurrentGif = null;
        ErrorMessage = null;
        _results.Clear();
        _currentIndex = 0;
    }
}
