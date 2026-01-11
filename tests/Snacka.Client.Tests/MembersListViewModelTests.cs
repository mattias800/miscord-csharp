using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Snacka.Client.Controls;
using Snacka.Client.Services;
using Snacka.Client.ViewModels;
using Snacka.Shared.Models;
using Moq;

namespace Snacka.Client.Tests;

/// <summary>
/// E2E headless tests for MembersListViewModel.
/// These tests verify that the member management functionality works correctly after the refactor.
/// </summary>
public class MembersListViewModelTests
{
    private static CommunityMemberResponse CreateMember(
        Guid? userId = null,
        string username = "testuser",
        UserRole role = UserRole.Member,
        bool isOnline = true)
    {
        return new CommunityMemberResponse(
            UserId: userId ?? Guid.NewGuid(),
            Username: username,
            DisplayName: null,
            DisplayNameOverride: null,
            EffectiveDisplayName: username,
            Avatar: null,
            IsOnline: isOnline,
            Role: role,
            JoinedAt: DateTime.UtcNow
        );
    }

    private Mock<IApiClient> CreateMockApiClient()
    {
        var mock = new Mock<IApiClient>();

        mock.Setup(x => x.UpdateMyNicknameAsync(It.IsAny<Guid>(), It.IsAny<string?>()))
            .ReturnsAsync((Guid communityId, string? nickname) =>
            {
                var member = CreateMember(username: "testuser");
                return ApiResult<CommunityMemberResponse>.Ok(member);
            });

        mock.Setup(x => x.UpdateMemberRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserRole>()))
            .ReturnsAsync((Guid communityId, Guid userId, UserRole role) =>
            {
                var member = CreateMember(userId: userId, username: "promoted_user", role: role);
                return ApiResult<CommunityMemberResponse>.Ok(member);
            });

        return mock;
    }

    [AvaloniaFact]
    public void SortedMembers_CurrentUserFirst()
    {
        // Arrange
        var mockApi = CreateMockApiClient();
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var communityId = Guid.NewGuid();

        var members = new ObservableCollection<CommunityMemberResponse>
        {
            CreateMember(userId: otherUserId, username: "anotheruser"),
            CreateMember(userId: currentUserId, username: "currentuser")
        };

        var viewModel = new MembersListViewModel(
            mockApi.Object,
            currentUserId,
            members,
            () => communityId,
            member => { },
            error => { }
        );

        // Act
        var sortedMembers = viewModel.SortedMembers.ToList();

        // Assert - Current user should be first
        Assert.Equal(currentUserId, sortedMembers.First().UserId);
    }

    [AvaloniaFact]
    public void CanManageMembers_WhenOwner_ReturnsTrue()
    {
        // Arrange
        var mockApi = CreateMockApiClient();
        var currentUserId = Guid.NewGuid();
        var communityId = Guid.NewGuid();
        var members = new ObservableCollection<CommunityMemberResponse>();

        var viewModel = new MembersListViewModel(
            mockApi.Object,
            currentUserId,
            members,
            () => communityId,
            member => { },
            error => { }
        );

        // Act
        viewModel.UpdateCurrentUserRole(UserRole.Owner);

        // Assert
        Assert.True(viewModel.CanManageMembers);
    }

    [AvaloniaFact]
    public void CanManageMembers_WhenAdmin_ReturnsFalse()
    {
        // Arrange
        var mockApi = CreateMockApiClient();
        var currentUserId = Guid.NewGuid();
        var communityId = Guid.NewGuid();
        var members = new ObservableCollection<CommunityMemberResponse>();

        var viewModel = new MembersListViewModel(
            mockApi.Object,
            currentUserId,
            members,
            () => communityId,
            member => { },
            error => { }
        );

        // Act
        viewModel.UpdateCurrentUserRole(UserRole.Admin);

        // Assert - Only owner can manage members (promote/demote)
        Assert.False(viewModel.CanManageMembers);
    }

    [AvaloniaFact]
    public void StartDMCommand_WhenExecuted_InvokesCallback()
    {
        // Arrange
        var mockApi = CreateMockApiClient();
        var currentUserId = Guid.NewGuid();
        var communityId = Guid.NewGuid();
        var members = new ObservableCollection<CommunityMemberResponse>();

        CommunityMemberResponse? dmTarget = null;
        var viewModel = new MembersListViewModel(
            mockApi.Object,
            currentUserId,
            members,
            () => communityId,
            member => dmTarget = member,
            error => { }
        );

        var targetMember = CreateMember(username: "target");

        // Act
        viewModel.StartDMCommand.Execute(targetMember).Subscribe();

        // Assert
        Assert.NotNull(dmTarget);
        Assert.Equal(targetMember.UserId, dmTarget.UserId);
    }

    [AvaloniaFact]
    public async Task PromoteToAdmin_WhenOwner_UpdatesMember()
    {
        // Arrange
        var mockApi = CreateMockApiClient();
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var communityId = Guid.NewGuid();

        var targetMember = CreateMember(userId: targetUserId, username: "target");
        var members = new ObservableCollection<CommunityMemberResponse> { targetMember };

        var viewModel = new MembersListViewModel(
            mockApi.Object,
            currentUserId,
            members,
            () => communityId,
            member => { },
            error => { }
        );

        // Set as owner so we can manage members
        viewModel.UpdateCurrentUserRole(UserRole.Owner);

        // Act
        viewModel.PromoteToAdminCommand.Execute(targetMember).Subscribe();
        await Task.Delay(100); // Wait for async operation

        // Assert
        mockApi.Verify(x => x.UpdateMemberRoleAsync(communityId, targetUserId, UserRole.Admin), Times.Once);
    }

    [AvaloniaFact]
    public async Task MembersListView_WithViewModel_BindsCorrectly()
    {
        // Arrange
        var mockApi = CreateMockApiClient();
        var currentUserId = Guid.NewGuid();
        var communityId = Guid.NewGuid();

        var members = new ObservableCollection<CommunityMemberResponse>
        {
            CreateMember(userId: currentUserId, username: "currentuser", role: UserRole.Owner)
        };

        var viewModel = new MembersListViewModel(
            mockApi.Object,
            currentUserId,
            members,
            () => communityId,
            member => { },
            error => { }
        );

        viewModel.UpdateCurrentUserRole(UserRole.Owner);

        // Create the view with the ViewModel
        var view = new MembersListView { ViewModel = viewModel };
        var window = new Window { Content = view };
        window.Show();

        await Task.Delay(100);

        // Assert - View should be connected to ViewModel
        Assert.NotNull(view.ViewModel);
        Assert.Single(view.ViewModel.SortedMembers);
        Assert.True(view.ViewModel.CanManageMembers);

        // Cleanup
        window.Close();
    }
}
