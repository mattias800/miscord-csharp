using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Snacka.Client.Services;
using ReactiveUI;

namespace Snacka.Client.ViewModels;

/// <summary>
/// ViewModel for the message search modal.
/// </summary>
public class MessageSearchViewModel : ViewModelBase, IDisposable
{
    private readonly IApiClient _apiClient;
    private readonly Guid _communityId;
    private readonly Action<MessageSearchResult> _onResultSelected;
    private readonly Action _onClose;
    private readonly IDisposable _searchSubscription;

    private string _searchQuery = string.Empty;
    private bool _isLoading;
    private int _selectedIndex = -1;
    private int _totalCount;
    private ObservableCollection<MessageSearchResult> _results = new();

    public MessageSearchViewModel(
        IApiClient apiClient,
        Guid communityId,
        Action<MessageSearchResult> onResultSelected,
        Action onClose)
    {
        _apiClient = apiClient;
        _communityId = communityId;
        _onResultSelected = onResultSelected;
        _onClose = onClose;

        // Auto-search with 1 second debounce
        _searchSubscription = this.WhenAnyValue(x => x.SearchQuery)
            .Throttle(TimeSpan.FromSeconds(1))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async _ => await SearchAsync());
    }

    /// <summary>
    /// The search query entered by the user.
    /// </summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set => this.RaiseAndSetIfChanged(ref _searchQuery, value);
    }

    /// <summary>
    /// Whether a search is currently in progress.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    /// <summary>
    /// The index of the currently selected result.
    /// </summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var clamped = Math.Clamp(value, -1, Math.Max(0, Results.Count - 1));
            this.RaiseAndSetIfChanged(ref _selectedIndex, clamped);
        }
    }

    /// <summary>
    /// Total number of matching messages.
    /// </summary>
    public int TotalCount
    {
        get => _totalCount;
        set => this.RaiseAndSetIfChanged(ref _totalCount, value);
    }

    /// <summary>
    /// The search results to display.
    /// </summary>
    public ObservableCollection<MessageSearchResult> Results
    {
        get => _results;
        private set => this.RaiseAndSetIfChanged(ref _results, value);
    }

    /// <summary>
    /// Whether there are any results.
    /// </summary>
    public bool HasResults => Results.Count > 0;

    /// <summary>
    /// Whether to show the "no results" message.
    /// </summary>
    public bool ShowNoResults => !IsLoading && !string.IsNullOrWhiteSpace(SearchQuery) && Results.Count == 0;

    /// <summary>
    /// Status text showing result count.
    /// </summary>
    public string StatusText => TotalCount > 0
        ? $"{TotalCount} result{(TotalCount != 1 ? "s" : "")} found"
        : "";

    /// <summary>
    /// Execute the search with the current query.
    /// </summary>
    public async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            Results.Clear();
            TotalCount = 0;
            SelectedIndex = -1;
            this.RaisePropertyChanged(nameof(HasResults));
            this.RaisePropertyChanged(nameof(ShowNoResults));
            this.RaisePropertyChanged(nameof(StatusText));
            return;
        }

        IsLoading = true;
        try
        {
            var response = await _apiClient.SearchMessagesAsync(_communityId, SearchQuery);
            if (response.Success && response.Data != null)
            {
                Results.Clear();
                foreach (var result in response.Data.Results)
                {
                    Results.Add(result);
                }
                TotalCount = response.Data.TotalCount;
                SelectedIndex = -1;
            }
            else
            {
                Results.Clear();
                TotalCount = 0;
            }
        }
        finally
        {
            IsLoading = false;
            this.RaisePropertyChanged(nameof(HasResults));
            this.RaisePropertyChanged(nameof(ShowNoResults));
            this.RaisePropertyChanged(nameof(StatusText));
        }
    }

    /// <summary>
    /// Move selection up.
    /// </summary>
    public void MoveUp()
    {
        if (SelectedIndex > 0)
            SelectedIndex--;
        else if (SelectedIndex == -1 && Results.Count > 0)
            SelectedIndex = 0;
    }

    /// <summary>
    /// Move selection down.
    /// </summary>
    public void MoveDown()
    {
        if (SelectedIndex == -1 && Results.Count > 0)
            SelectedIndex = 0;
        else if (SelectedIndex < Results.Count - 1)
            SelectedIndex++;
    }

    /// <summary>
    /// Select the currently highlighted result.
    /// </summary>
    public void SelectCurrent()
    {
        if (Results.Count > 0)
        {
            var index = SelectedIndex == -1 ? 0 : SelectedIndex;
            if (index < Results.Count)
            {
                _onResultSelected(Results[index]);
            }
        }
    }

    /// <summary>
    /// Select a specific result.
    /// </summary>
    public void SelectResult(MessageSearchResult result)
    {
        _onResultSelected(result);
    }

    /// <summary>
    /// Close the search modal.
    /// </summary>
    public void Close() => _onClose();

    public void Dispose()
    {
        _searchSubscription.Dispose();
    }
}
