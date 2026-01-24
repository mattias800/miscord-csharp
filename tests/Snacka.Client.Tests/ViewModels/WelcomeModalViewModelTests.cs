using System.Reactive.Linq;
using Moq;
using Snacka.Client.Services;
using Snacka.Client.ViewModels;
using Xunit;

namespace Snacka.Client.Tests.ViewModels;

public class WelcomeModalViewModelTests
{
    private readonly Mock<ISettingsStore> _settingsStoreMock;
    private readonly UserSettings _userSettings;

    public WelcomeModalViewModelTests()
    {
        _userSettings = new UserSettings();
        _settingsStoreMock = new Mock<ISettingsStore>();
        _settingsStoreMock.Setup(x => x.Settings).Returns(_userSettings);
    }

    private WelcomeModalViewModel CreateViewModel()
    {
        return new WelcomeModalViewModel(_settingsStoreMock.Object);
    }

    #region Initialization Tests

    [Fact]
    public void Constructor_InitializesWithClosedState()
    {
        // Act
        var vm = CreateViewModel();

        // Assert
        Assert.False(vm.IsOpen);
    }

    [Fact]
    public void Constructor_InitializesCommands()
    {
        // Act
        var vm = CreateViewModel();

        // Assert
        Assert.NotNull(vm.CloseCommand);
        Assert.NotNull(vm.BrowseCommunitiesCommand);
        Assert.NotNull(vm.CreateCommunityCommand);
    }

    #endregion

    #region ShowIfFirstTime Tests

    [Fact]
    public void ShowIfFirstTime_WhenNotSeenBefore_OpensModal()
    {
        // Arrange
        _userSettings.HasSeenWelcome = false;
        var vm = CreateViewModel();

        // Act
        vm.ShowIfFirstTime();

        // Assert
        Assert.True(vm.IsOpen);
    }

    [Fact]
    public void ShowIfFirstTime_WhenAlreadySeen_DoesNotOpenModal()
    {
        // Arrange
        _userSettings.HasSeenWelcome = true;
        var vm = CreateViewModel();

        // Act
        vm.ShowIfFirstTime();

        // Assert
        Assert.False(vm.IsOpen);
    }

    #endregion

    #region Close Tests

    [Fact]
    public void Close_ClosesModal()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.IsOpen = true;

        // Act
        vm.Close();

        // Assert
        Assert.False(vm.IsOpen);
    }

    [Fact]
    public void Close_MarksAsSeenAndSaves()
    {
        // Arrange
        _userSettings.HasSeenWelcome = false;
        var vm = CreateViewModel();
        vm.IsOpen = true;

        // Act
        vm.Close();

        // Assert
        Assert.True(_userSettings.HasSeenWelcome);
        _settingsStoreMock.Verify(x => x.Save(), Times.Once);
    }

    [Fact]
    public void Close_RaisesPropertyChanged()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.IsOpen = true;

        var propertyChangedRaised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WelcomeModalViewModel.IsOpen))
                propertyChangedRaised = true;
        };

        // Act
        vm.Close();

        // Assert
        Assert.True(propertyChangedRaised);
    }

    #endregion

    #region BrowseCommunities Tests

    [Fact]
    public async Task BrowseCommunitiesCommand_ClosesModalAndRaisesEvent()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.IsOpen = true;

        var eventRaised = false;
        vm.BrowseCommunitiesRequested += () =>
        {
            eventRaised = true;
            return Task.CompletedTask;
        };

        // Act
        await vm.BrowseCommunitiesCommand.Execute().FirstAsync();

        // Assert
        Assert.False(vm.IsOpen);
        Assert.True(eventRaised);
        Assert.True(_userSettings.HasSeenWelcome);
    }

    #endregion

    #region CreateCommunity Tests

    [Fact]
    public async Task CreateCommunityCommand_ClosesModalAndRaisesEvent()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.IsOpen = true;

        var eventRaised = false;
        vm.CreateCommunityRequested += () =>
        {
            eventRaised = true;
            return Task.CompletedTask;
        };

        // Act
        await vm.CreateCommunityCommand.Execute().FirstAsync();

        // Assert
        Assert.False(vm.IsOpen);
        Assert.True(eventRaised);
        Assert.True(_userSettings.HasSeenWelcome);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_DisposesCommands()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert - should not throw
        vm.Dispose();
    }

    #endregion
}
