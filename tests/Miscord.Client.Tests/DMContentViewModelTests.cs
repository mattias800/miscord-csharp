using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Miscord.Client.Controls;
using Miscord.Client.Services;
using Miscord.Client.ViewModels;
using Moq;

namespace Miscord.Client.Tests;

/// <summary>
/// E2E headless tests for DMContentViewModel.
/// These tests verify that the DM functionality works correctly after the refactor.
/// </summary>
public class DMContentViewModelTests
{
    private Mock<IApiClient> CreateMockApiClient()
    {
        var mock = new Mock<IApiClient>();

        mock.Setup(x => x.GetDirectMessagesAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(ApiResult<List<DirectMessageResponse>>.Ok(new List<DirectMessageResponse>()));

        mock.Setup(x => x.MarkConversationAsReadAsync(It.IsAny<Guid>()))
            .ReturnsAsync(ApiResult<bool>.Ok(true));

        mock.Setup(x => x.SendDirectMessageAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync((Guid recipientId, string content) =>
            {
                var message = new DirectMessageResponse(
                    Id: Guid.NewGuid(),
                    Content: content,
                    SenderId: Guid.NewGuid(),
                    SenderUsername: "testuser",
                    SenderEffectiveDisplayName: "testuser",
                    RecipientId: recipientId,
                    RecipientUsername: "recipient",
                    RecipientEffectiveDisplayName: "recipient",
                    CreatedAt: DateTime.UtcNow,
                    IsRead: false
                );
                return ApiResult<DirectMessageResponse>.Ok(message);
            });

        return mock;
    }

    private Mock<ISignalRService> CreateMockSignalR()
    {
        var mock = new Mock<ISignalRService>();
        mock.Setup(x => x.SendDMTypingAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    [AvaloniaFact]
    public void OpenConversation_WhenCalled_SetsRecipientInfo()
    {
        // Arrange
        var mockApi = CreateMockApiClient();
        var mockSignalR = CreateMockSignalR();
        var currentUserId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        var viewModel = new DMContentViewModel(
            mockApi.Object,
            mockSignalR.Object,
            currentUserId,
            error => { }
        );

        // Act
        viewModel.OpenConversation(recipientId, "TestRecipient");

        // Assert
        Assert.True(viewModel.IsOpen);
        Assert.Equal(recipientId, viewModel.RecipientId);
        Assert.Equal("TestRecipient", viewModel.RecipientName);
    }

    [AvaloniaFact]
    public void OpenConversation_WithSelf_DoesNotOpen()
    {
        // Arrange
        var mockApi = CreateMockApiClient();
        var mockSignalR = CreateMockSignalR();
        var currentUserId = Guid.NewGuid();

        var viewModel = new DMContentViewModel(
            mockApi.Object,
            mockSignalR.Object,
            currentUserId,
            error => { }
        );

        // Act - try to open DM with self
        viewModel.OpenConversation(currentUserId, "Self");

        // Assert - should not open
        Assert.False(viewModel.IsOpen);
        Assert.Null(viewModel.RecipientId);
    }

    [AvaloniaFact]
    public async Task SendMessage_WhenCalled_AddsMessageToList()
    {
        // Arrange
        var mockApi = CreateMockApiClient();
        var mockSignalR = CreateMockSignalR();
        var currentUserId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        var viewModel = new DMContentViewModel(
            mockApi.Object,
            mockSignalR.Object,
            currentUserId,
            error => { }
        );

        // Open conversation first
        viewModel.OpenConversation(recipientId, "TestRecipient");
        await Task.Delay(200); // Wait for messages to load

        // Set message input
        viewModel.MessageInput = "Hello, World!";

        // Act
        viewModel.SendMessageCommand.Execute().Subscribe();
        await Task.Delay(100); // Wait for command to complete

        // Assert
        Assert.Single(viewModel.Messages);
        Assert.Equal("Hello, World!", viewModel.Messages.First().Content);
        Assert.Empty(viewModel.MessageInput); // Input should be cleared
    }

    [AvaloniaFact]
    public void CloseCommand_WhenExecuted_ClearsState()
    {
        // Arrange
        var mockApi = CreateMockApiClient();
        var mockSignalR = CreateMockSignalR();
        var currentUserId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        var viewModel = new DMContentViewModel(
            mockApi.Object,
            mockSignalR.Object,
            currentUserId,
            error => { }
        );

        // Open conversation first
        viewModel.OpenConversation(recipientId, "TestRecipient");

        // Act
        viewModel.CloseCommand.Execute().Subscribe();

        // Assert
        Assert.False(viewModel.IsOpen);
        Assert.Null(viewModel.RecipientId);
        Assert.Null(viewModel.RecipientName);
        Assert.Empty(viewModel.Messages);
    }

    [AvaloniaFact]
    public async Task DMContentView_WithViewModel_BindsCorrectly()
    {
        // Arrange
        var mockApi = CreateMockApiClient();
        var mockSignalR = CreateMockSignalR();
        var currentUserId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        var viewModel = new DMContentViewModel(
            mockApi.Object,
            mockSignalR.Object,
            currentUserId,
            error => { }
        );

        // Open conversation
        viewModel.OpenConversation(recipientId, "TestRecipient");
        await Task.Delay(200);

        // Create the view with the ViewModel
        var view = new DMContentView { ViewModel = viewModel };
        var window = new Window { Content = view };
        window.Show();

        // Assert - View should be connected to ViewModel
        Assert.NotNull(view.ViewModel);
        Assert.Equal("TestRecipient", view.ViewModel.RecipientName);
        Assert.True(view.ViewModel.IsOpen);

        // Cleanup
        window.Close();
    }
}
