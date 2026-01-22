using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using Snacka.Client.Services;

namespace Snacka.Client.ViewModels;

/// <summary>
/// ViewModel for community discovery and joining.
/// Handles loading discoverable communities and joining them.
/// </summary>
public class CommunityDiscoveryViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly Func<Task> _onCommunityJoined;

    private bool _isOpen;
    private bool _isLoading;
    private ObservableCollection<CommunityResponse> _communities = new();
    private string? _errorMessage;
    private Guid? _joiningCommunityId;

    public CommunityDiscoveryViewModel(IApiClient apiClient, Func<Task> onCommunityJoined)
    {
        _apiClient = apiClient;
        _onCommunityJoined = onCommunityJoined;

        OpenCommand = ReactiveCommand.CreateFromTask(OpenAsync);
        CloseCommand = ReactiveCommand.Create(Close);
        JoinCommunityCommand = ReactiveCommand.CreateFromTask<CommunityResponse>(JoinCommunityAsync);
    }

    public bool IsOpen
    {
        get => _isOpen;
        set => this.RaiseAndSetIfChanged(ref _isOpen, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public ObservableCollection<CommunityResponse> Communities => _communities;

    public bool HasNoCommunities => _communities.Count == 0;

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public Guid? JoiningCommunityId
    {
        get => _joiningCommunityId;
        set => this.RaiseAndSetIfChanged(ref _joiningCommunityId, value);
    }

    public ReactiveCommand<Unit, Unit> OpenCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }
    public ReactiveCommand<CommunityResponse, Unit> JoinCommunityCommand { get; }

    public async Task OpenAsync()
    {
        IsOpen = true;
        await LoadCommunitiesAsync();
    }

    private void Close()
    {
        IsOpen = false;
    }

    private async Task LoadCommunitiesAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var result = await _apiClient.DiscoverCommunitiesAsync();
            if (result.Success && result.Data is not null)
            {
                _communities.Clear();
                foreach (var community in result.Data)
                    _communities.Add(community);

                this.RaisePropertyChanged(nameof(HasNoCommunities));
            }
            else
            {
                ErrorMessage = result.Error ?? "Failed to load communities";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading communities: {ex.Message}");
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task JoinCommunityAsync(CommunityResponse community)
    {
        JoiningCommunityId = community.Id;
        ErrorMessage = null;

        try
        {
            var result = await _apiClient.JoinCommunityAsync(community.Id);
            if (result.Success)
            {
                IsOpen = false;
                await _onCommunityJoined();
            }
            else
            {
                ErrorMessage = result.Error ?? "Failed to join community";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error joining community: {ex.Message}");
            ErrorMessage = ex.Message;
        }
        finally
        {
            JoiningCommunityId = null;
        }
    }
}
