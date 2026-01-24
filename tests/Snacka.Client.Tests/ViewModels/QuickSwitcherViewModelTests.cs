using Moq;
using Snacka.Client.Services;
using Snacka.Client.ViewModels;
using Snacka.Shared.Models;

namespace Snacka.Client.Tests.ViewModels;

public class QuickSwitcherViewModelTests : IDisposable
{
    private readonly Mock<ISettingsStore> _mockSettingsStore;
    private readonly UserSettings _settings;
    private readonly List<QuickSwitcherItem> _selectedItems;
    private readonly List<object> _closeCalls;
    private readonly Guid _currentUserId;

    public QuickSwitcherViewModelTests()
    {
        _mockSettingsStore = new Mock<ISettingsStore>();
        _settings = new UserSettings();
        _mockSettingsStore.Setup(x => x.Settings).Returns(_settings);

        _selectedItems = new List<QuickSwitcherItem>();
        _closeCalls = new List<object>();
        _currentUserId = Guid.NewGuid();
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    private QuickSwitcherViewModel CreateViewModel(
        IEnumerable<ChannelResponse>? textChannels = null,
        IEnumerable<VoiceChannelViewModel>? voiceChannels = null,
        IEnumerable<CommunityMemberResponse>? members = null)
    {
        return new QuickSwitcherViewModel(
            textChannels ?? Array.Empty<ChannelResponse>(),
            voiceChannels ?? Array.Empty<VoiceChannelViewModel>(),
            members ?? Array.Empty<CommunityMemberResponse>(),
            _currentUserId,
            _mockSettingsStore.Object,
            item => _selectedItems.Add(item),
            () => _closeCalls.Add(new object())
        );
    }

    private static ChannelResponse CreateTextChannel(
        Guid? id = null,
        string name = "general",
        string? topic = null)
    {
        return new ChannelResponse(
            Id: id ?? Guid.NewGuid(),
            Name: name,
            Topic: topic,
            CommunityId: Guid.NewGuid(),
            Type: ChannelType.Text,
            Position: 0,
            CreatedAt: DateTime.UtcNow
        );
    }

    private static CommunityMemberResponse CreateMember(
        Guid? userId = null,
        string username = "testuser",
        string? displayName = null)
    {
        return new CommunityMemberResponse(
            UserId: userId ?? Guid.NewGuid(),
            Username: username,
            DisplayName: displayName,
            DisplayNameOverride: null,
            EffectiveDisplayName: displayName ?? username,
            Avatar: null,
            IsOnline: true,
            Role: UserRole.Member,
            JoinedAt: DateTime.UtcNow
        );
    }

    #region Initial State Tests

    [Fact]
    public void Constructor_WithNoRecentItems_StartsWithEmptyFilteredItems()
    {
        // Arrange & Act
        var vm = CreateViewModel();

        // Assert
        Assert.Empty(vm.FilteredItems);
        Assert.Equal(-1, vm.SelectedIndex);
        Assert.False(vm.HasItems);
        Assert.Equal("RECENT", vm.HeaderText);
    }

    [Fact]
    public void Constructor_WithRecentItems_LoadsRecentItems()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        _settings.RecentQuickSwitcherItems = new List<RecentQuickSwitcherItem>
        {
            new(QuickSwitcherItemType.TextChannel, channelId, "general", DateTime.UtcNow)
        };

        var channels = new[] { CreateTextChannel(id: channelId, name: "general") };

        // Act
        var vm = CreateViewModel(textChannels: channels);

        // Assert
        Assert.Single(vm.FilteredItems);
        Assert.Equal("general", vm.FilteredItems[0].Name);
        Assert.True(vm.HasItems);
    }

    [Fact]
    public void Constructor_WithRecentItems_FiltersOutNonExistentItems()
    {
        // Arrange
        var existingChannelId = Guid.NewGuid();
        var deletedChannelId = Guid.NewGuid();

        _settings.RecentQuickSwitcherItems = new List<RecentQuickSwitcherItem>
        {
            new(QuickSwitcherItemType.TextChannel, existingChannelId, "existing", DateTime.UtcNow),
            new(QuickSwitcherItemType.TextChannel, deletedChannelId, "deleted", DateTime.UtcNow.AddMinutes(-1))
        };

        var channels = new[] { CreateTextChannel(id: existingChannelId, name: "existing") };

        // Act
        var vm = CreateViewModel(textChannels: channels);

        // Assert
        Assert.Single(vm.FilteredItems);
        Assert.Equal("existing", vm.FilteredItems[0].Name);
    }

    #endregion

    #region Search Tests

    [Fact]
    public void SearchQuery_WithMatchingChannel_ReturnsChannel()
    {
        // Arrange
        var channels = new[]
        {
            CreateTextChannel(name: "general"),
            CreateTextChannel(name: "random"),
            CreateTextChannel(name: "announcements")
        };
        var vm = CreateViewModel(textChannels: channels);

        // Act
        vm.SearchQuery = "gen";

        // Assert
        Assert.Single(vm.FilteredItems);
        Assert.Equal("general", vm.FilteredItems[0].Name);
        Assert.Equal("RESULTS", vm.HeaderText);
    }

    [Fact]
    public void SearchQuery_WithMultipleMatches_ReturnsSortedByScore()
    {
        // Arrange
        var channels = new[]
        {
            CreateTextChannel(name: "general-chat"),
            CreateTextChannel(name: "general"),
            CreateTextChannel(name: "my-general-stuff")
        };
        var vm = CreateViewModel(textChannels: channels);

        // Act
        vm.SearchQuery = "general";

        // Assert
        Assert.Equal(3, vm.FilteredItems.Count);
        // Exact match should be first (or near-exact)
        Assert.Equal("general", vm.FilteredItems[0].Name);
    }

    [Fact]
    public void SearchQuery_WithHashPrefix_OnlySearchesChannels()
    {
        // Arrange
        var channels = new[] { CreateTextChannel(name: "general") };
        var members = new[] { CreateMember(username: "general_user") };
        var vm = CreateViewModel(textChannels: channels, members: members);

        // Act
        vm.SearchQuery = "#gen";

        // Assert
        Assert.Single(vm.FilteredItems);
        Assert.Equal(QuickSwitcherItemType.TextChannel, vm.FilteredItems[0].Type);
    }

    [Fact]
    public void SearchQuery_WithAtPrefix_OnlySearchesUsers()
    {
        // Arrange
        var channels = new[] { CreateTextChannel(name: "john-channel") };
        var members = new[] { CreateMember(username: "john") };
        var vm = CreateViewModel(textChannels: channels, members: members);

        // Act
        vm.SearchQuery = "@john";

        // Assert
        Assert.Single(vm.FilteredItems);
        Assert.Equal(QuickSwitcherItemType.User, vm.FilteredItems[0].Type);
    }

    [Fact]
    public void SearchQuery_ExcludesCurrentUser()
    {
        // Arrange
        var members = new[]
        {
            CreateMember(userId: _currentUserId, username: "myself"),
            CreateMember(username: "other")
        };
        var vm = CreateViewModel(members: members);

        // Act
        vm.SearchQuery = "m";

        // Assert
        // Should only find "other", not "myself"
        Assert.DoesNotContain(vm.FilteredItems, i => i.Name == "myself");
    }

    [Fact]
    public void SearchQuery_WhenCleared_LoadsRecentItems()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        _settings.RecentQuickSwitcherItems = new List<RecentQuickSwitcherItem>
        {
            new(QuickSwitcherItemType.TextChannel, channelId, "recent", DateTime.UtcNow)
        };

        var channels = new[]
        {
            CreateTextChannel(id: channelId, name: "recent"),
            CreateTextChannel(name: "other")
        };
        var vm = CreateViewModel(textChannels: channels);

        // Act - Search then clear
        vm.SearchQuery = "other";
        Assert.Single(vm.FilteredItems);
        Assert.Equal("other", vm.FilteredItems[0].Name);

        vm.SearchQuery = "";

        // Assert - Should show recent items again
        Assert.Single(vm.FilteredItems);
        Assert.Equal("recent", vm.FilteredItems[0].Name);
        Assert.Equal("RECENT", vm.HeaderText);
    }

    #endregion

    #region Navigation Tests

    [Fact]
    public void MoveDown_WithNoSelection_SelectsFirstItem()
    {
        // Arrange
        var channels = new[]
        {
            CreateTextChannel(name: "first"),
            CreateTextChannel(name: "second")
        };
        var vm = CreateViewModel(textChannels: channels);
        vm.SearchQuery = ""; // Will load recent items
        vm.SearchQuery = "f"; // Search for items

        // Act
        vm.MoveDown();

        // Assert
        Assert.Equal(0, vm.SelectedIndex);
    }

    [Fact]
    public void MoveDown_WithSelection_MovesToNextItem()
    {
        // Arrange
        var channels = new[]
        {
            CreateTextChannel(name: "alpha"),
            CreateTextChannel(name: "beta")
        };
        var vm = CreateViewModel(textChannels: channels);
        vm.SearchQuery = "a"; // Should match "alpha" and "beta" (both contain 'a')

        // Both channels should be in results
        Assert.True(vm.FilteredItems.Count >= 2, $"Expected at least 2 items, got {vm.FilteredItems.Count}");
        vm.SelectedIndex = 0;

        // Act
        vm.MoveDown();

        // Assert
        Assert.Equal(1, vm.SelectedIndex);
    }

    [Fact]
    public void MoveDown_AtLastItem_StaysAtLastItem()
    {
        // Arrange
        var channels = new[]
        {
            CreateTextChannel(name: "alpha"),
            CreateTextChannel(name: "beta")
        };
        var vm = CreateViewModel(textChannels: channels);
        vm.SearchQuery = "a"; // Should match "alpha" and "beta" (both contain 'a')

        // Both channels should be in results
        Assert.True(vm.FilteredItems.Count >= 2, $"Expected at least 2 items, got {vm.FilteredItems.Count}");
        vm.SelectedIndex = vm.FilteredItems.Count - 1; // Set to last item

        // Act
        vm.MoveDown();

        // Assert
        Assert.Equal(vm.FilteredItems.Count - 1, vm.SelectedIndex);
    }

    [Fact]
    public void MoveUp_AtFirstItem_StaysAtFirstItem()
    {
        // Arrange
        var channels = new[]
        {
            CreateTextChannel(name: "first"),
            CreateTextChannel(name: "second")
        };
        var vm = CreateViewModel(textChannels: channels);
        vm.SearchQuery = "i"; // Should match both
        vm.SelectedIndex = 0;

        // Act
        vm.MoveUp();

        // Assert
        Assert.Equal(0, vm.SelectedIndex);
    }

    [Fact]
    public void MoveUp_WithNoSelection_SelectsFirstItem()
    {
        // Arrange
        var channels = new[]
        {
            CreateTextChannel(name: "first"),
            CreateTextChannel(name: "second")
        };
        var vm = CreateViewModel(textChannels: channels);
        vm.SearchQuery = "i"; // Should match both

        // Act
        vm.MoveUp();

        // Assert
        Assert.Equal(0, vm.SelectedIndex);
    }

    #endregion

    #region Selection Tests

    [Fact]
    public void SelectCurrent_WithItems_CallsOnItemSelected()
    {
        // Arrange
        var channels = new[] { CreateTextChannel(name: "general") };
        var vm = CreateViewModel(textChannels: channels);
        vm.SearchQuery = "gen";

        // Act
        vm.SelectCurrent();

        // Assert
        Assert.Single(_selectedItems);
        Assert.Equal("general", _selectedItems[0].Name);
    }

    [Fact]
    public void SelectCurrent_WithNoItems_DoesNothing()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.SearchQuery = "nonexistent";

        // Act
        vm.SelectCurrent();

        // Assert
        Assert.Empty(_selectedItems);
    }

    [Fact]
    public void SelectCurrent_AddsToRecentItems()
    {
        // Arrange
        var channels = new[] { CreateTextChannel(name: "general") };
        var vm = CreateViewModel(textChannels: channels);
        vm.SearchQuery = "gen";

        // Act
        vm.SelectCurrent();

        // Assert
        Assert.Single(_settings.RecentQuickSwitcherItems);
        Assert.Equal("general", _settings.RecentQuickSwitcherItems[0].Name);
        _mockSettingsStore.Verify(x => x.Save(), Times.Once);
    }

    [Fact]
    public void SelectItem_CallsOnItemSelected()
    {
        // Arrange
        var channels = new[] { CreateTextChannel(name: "general") };
        var vm = CreateViewModel(textChannels: channels);
        vm.SearchQuery = "gen";
        var item = vm.FilteredItems[0];

        // Act
        vm.SelectItem(item);

        // Assert
        Assert.Single(_selectedItems);
        Assert.Equal(item, _selectedItems[0]);
    }

    #endregion

    #region Close Tests

    [Fact]
    public void Close_CallsOnClose()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.Close();

        // Assert
        Assert.Single(_closeCalls);
    }

    #endregion

    #region QuickSwitcherItem Tests

    [Fact]
    public void QuickSwitcherItem_TextChannel_HasHashIcon()
    {
        // Arrange & Act
        var item = new QuickSwitcherItem(QuickSwitcherItemType.TextChannel, Guid.NewGuid(), "general");

        // Assert
        Assert.Equal("#", item.Icon);
    }

    [Fact]
    public void QuickSwitcherItem_VoiceChannel_HasSpeakerIcon()
    {
        // Arrange & Act
        var item = new QuickSwitcherItem(QuickSwitcherItemType.VoiceChannel, Guid.NewGuid(), "voice");

        // Assert
        Assert.Equal("🔊", item.Icon);
    }

    [Fact]
    public void QuickSwitcherItem_User_HasAtIcon()
    {
        // Arrange & Act
        var item = new QuickSwitcherItem(QuickSwitcherItemType.User, Guid.NewGuid(), "john");

        // Assert
        Assert.Equal("@", item.Icon);
    }

    #endregion
}
