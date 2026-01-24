using System.Reactive;
using ReactiveUI;
using Snacka.Client.Services;

namespace Snacka.Client.ViewModels;

/// <summary>
/// ViewModel for the welcome modal shown to first-time users.
/// Handles the modal state and user choices (browse communities or create one).
/// </summary>
public class WelcomeModalViewModel : ReactiveObject, IDisposable
{
    private readonly ISettingsStore _settingsStore;
    private bool _isOpen;

    /// <summary>
    /// Raised when the user chooses to browse communities.
    /// The parent should open the community discovery view.
    /// </summary>
    public event Func<Task>? BrowseCommunitiesRequested;

    /// <summary>
    /// Raised when the user chooses to create a community.
    /// The parent should initiate community creation.
    /// </summary>
    public event Func<Task>? CreateCommunityRequested;

    /// <summary>
    /// Creates a new WelcomeModalViewModel.
    /// </summary>
    public WelcomeModalViewModel(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore;

        CloseCommand = ReactiveCommand.Create(Close);
        BrowseCommunitiesCommand = ReactiveCommand.CreateFromTask(BrowseCommunitiesAsync);
        CreateCommunityCommand = ReactiveCommand.CreateFromTask(CreateCommunityAsync);
    }

    #region Properties

    /// <summary>
    /// Whether the welcome modal is open.
    /// </summary>
    public bool IsOpen
    {
        get => _isOpen;
        set => this.RaiseAndSetIfChanged(ref _isOpen, value);
    }

    #endregion

    #region Commands

    /// <summary>
    /// Command to close the welcome modal.
    /// </summary>
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    /// <summary>
    /// Command to browse existing communities.
    /// </summary>
    public ReactiveCommand<Unit, Unit> BrowseCommunitiesCommand { get; }

    /// <summary>
    /// Command to create a new community.
    /// </summary>
    public ReactiveCommand<Unit, Unit> CreateCommunityCommand { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Shows the welcome modal if the user hasn't seen it yet.
    /// Call this after loading communities when there are none.
    /// </summary>
    public void ShowIfFirstTime()
    {
        if (!_settingsStore.Settings.HasSeenWelcome)
        {
            IsOpen = true;
        }
    }

    /// <summary>
    /// Closes the welcome modal and marks it as seen.
    /// </summary>
    public void Close()
    {
        IsOpen = false;
        _settingsStore.Settings.HasSeenWelcome = true;
        _settingsStore.Save();
    }

    private async Task BrowseCommunitiesAsync()
    {
        Close();
        if (BrowseCommunitiesRequested != null)
        {
            await BrowseCommunitiesRequested();
        }
    }

    private async Task CreateCommunityAsync()
    {
        Close();
        if (CreateCommunityRequested != null)
        {
            await CreateCommunityRequested();
        }
    }

    #endregion

    public void Dispose()
    {
        CloseCommand.Dispose();
        BrowseCommunitiesCommand.Dispose();
        CreateCommunityCommand.Dispose();
    }
}
