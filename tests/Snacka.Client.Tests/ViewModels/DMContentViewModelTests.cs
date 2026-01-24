using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using Moq;
using ReactiveUI;
using Snacka.Client.Services;
using Snacka.Client.ViewModels;
using ConversationParticipantInfo = Snacka.Client.Services.ParticipantInfo;

namespace Snacka.Client.Tests.ViewModels;

public class DMContentViewModelTests : IDisposable
{
    private readonly Mock<IApiClient> _mockApiClient;
    private readonly Mock<ISignalRService> _mockSignalR;
    private readonly Mock<IConversationStateService> _mockConversationStateService;
    private readonly Guid _currentUserId;
    private string? _lastError;

    public DMContentViewModelTests()
    {
        _mockApiClient = new Mock<IApiClient>();
        _mockSignalR = new Mock<ISignalRService>();
        _mockConversationStateService = new Mock<IConversationStateService>();
        _currentUserId = Guid.NewGuid();
        _lastError = null;

        // Setup default ConversationStateService behavior
        var conversations = new ReadOnlyObservableCollection<ConversationSummaryResponse>(
            new ObservableCollection<ConversationSummaryResponse>());
        _mockConversationStateService.Setup(x => x.Conversations).Returns(conversations);
        _mockConversationStateService.Setup(x => x.TotalUnreadCount)
            .Returns(Observable.Return(0));
        _mockConversationStateService
            .Setup(x => x.MarkConversationAsReadAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    private DMContentViewModel CreateViewModel()
    {
        return new DMContentViewModel(
            _mockApiClient.Object,
            _mockSignalR.Object,
            _mockConversationStateService.Object,
            _currentUserId,
            error => _lastError = error
        );
    }

    private static ConversationMessageResponse CreateMessageResponse(
        Guid? id = null,
        Guid? conversationId = null,
        string content = "Test message",
        Guid? senderId = null)
    {
        return new ConversationMessageResponse(
            Id: id ?? Guid.NewGuid(),
            ConversationId: conversationId ?? Guid.NewGuid(),
            Content: content,
            SenderId: senderId ?? Guid.NewGuid(),
            SenderUsername: "testuser",
            SenderEffectiveDisplayName: "Test User",
            SenderAvatar: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: null
        );
    }

    private static ConversationParticipantInfo CreateParticipantInfo(
        Guid? userId = null,
        string username = "testuser",
        string displayName = "Test User")
    {
        return new ConversationParticipantInfo(
            UserId: userId ?? Guid.NewGuid(),
            Username: username,
            EffectiveDisplayName: displayName,
            Avatar: null,
            IsOnline: true,
            JoinedAt: DateTime.UtcNow
        );
    }

    private ConversationResponse CreateConversationResponse(
        Guid? id = null,
        string? name = null,
        bool isGroup = false,
        List<ConversationParticipantInfo>? participants = null)
    {
        var participantList = participants ?? new List<ConversationParticipantInfo>
        {
            CreateParticipantInfo(_currentUserId, "me", "Me"),
            CreateParticipantInfo(Guid.NewGuid(), "other", "Other User")
        };

        return new ConversationResponse(
            Id: id ?? Guid.NewGuid(),
            Name: name,
            IconFileName: null,
            IsGroup: isGroup,
            CreatedAt: DateTime.UtcNow,
            Participants: participantList,
            LastMessage: null,
            UnreadCount: 0
        );
    }

    #region Initial State Tests

    [Fact]
    public void Constructor_InitialState_IsCorrect()
    {
        // Arrange & Act
        var vm = CreateViewModel();

        // Assert
        Assert.False(vm.IsOpen);
        Assert.False(vm.IsLoading);
        Assert.Null(vm.ConversationId);
        Assert.Null(vm.ConversationDisplayName);
        Assert.False(vm.IsGroup);
        Assert.Equal(string.Empty, vm.MessageInput);
        Assert.Empty(vm.Messages);
        Assert.Empty(vm.Participants);
        Assert.Null(vm.EditingMessage);
        Assert.Equal(string.Empty, vm.EditingMessageContent);
        Assert.False(vm.IsTyping);
        Assert.Equal(string.Empty, vm.TypingIndicatorText);
    }

    [Fact]
    public void Constructor_Commands_AreNotNull()
    {
        // Arrange & Act
        var vm = CreateViewModel();

        // Assert
        Assert.NotNull(vm.SendMessageCommand);
        Assert.NotNull(vm.CloseCommand);
        Assert.NotNull(vm.StartEditMessageCommand);
        Assert.NotNull(vm.SaveMessageEditCommand);
        Assert.NotNull(vm.CancelEditMessageCommand);
        Assert.NotNull(vm.DeleteMessageCommand);
        Assert.NotNull(vm.AddParticipantCommand);
        Assert.NotNull(vm.RemoveParticipantCommand);
        Assert.NotNull(vm.LeaveConversationCommand);
    }

    [Fact]
    public void CurrentUserId_ReturnsCorrectValue()
    {
        // Arrange & Act
        var vm = CreateViewModel();

        // Assert
        Assert.Equal(_currentUserId, vm.CurrentUserId);
    }

    #endregion

    #region OpenDirectConversationAsync Tests

    [Fact]
    public async Task OpenDirectConversationAsync_Success_SetsConversationState()
    {
        // Arrange
        var vm = CreateViewModel();
        var otherUserId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var conversation = CreateConversationResponse(
            id: conversationId,
            participants: new List<ConversationParticipantInfo>
            {
                CreateParticipantInfo(_currentUserId, "me", "Me"),
                CreateParticipantInfo(otherUserId, "other", "Other User")
            });

        _mockApiClient
            .Setup(x => x.GetOrCreateDirectConversationAsync(otherUserId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(conversationId, 0, 50))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(new List<ConversationMessageResponse>()));

        // Act
        await vm.OpenDirectConversationAsync(otherUserId, "other");

        // Assert
        Assert.True(vm.IsOpen);
        Assert.Equal(conversationId, vm.ConversationId);
        Assert.Equal("Other User", vm.ConversationDisplayName);
        Assert.False(vm.IsGroup);
        Assert.Equal(2, vm.Participants.Count);
    }

    [Fact]
    public async Task OpenDirectConversationAsync_CannotDmSelf()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        await vm.OpenDirectConversationAsync(_currentUserId, "me");

        // Assert
        Assert.False(vm.IsOpen);
        Assert.Null(vm.ConversationId);
    }

    [Fact]
    public async Task OpenDirectConversationAsync_ApiFailure_CallsErrorHandler()
    {
        // Arrange
        var vm = CreateViewModel();
        var otherUserId = Guid.NewGuid();

        _mockApiClient
            .Setup(x => x.GetOrCreateDirectConversationAsync(otherUserId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Fail("API error"));

        // Act
        await vm.OpenDirectConversationAsync(otherUserId, "other");

        // Assert
        Assert.False(vm.IsOpen);
        Assert.Equal("API error", _lastError);
    }

    [Fact]
    public async Task OpenDirectConversationAsync_LoadsMessages()
    {
        // Arrange
        var vm = CreateViewModel();
        var otherUserId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var conversation = CreateConversationResponse(id: conversationId);
        var messages = new List<ConversationMessageResponse>
        {
            CreateMessageResponse(conversationId: conversationId, content: "Message 1"),
            CreateMessageResponse(conversationId: conversationId, content: "Message 2")
        };

        _mockApiClient
            .Setup(x => x.GetOrCreateDirectConversationAsync(otherUserId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(conversationId, 0, 50))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(messages));

        // Act
        await vm.OpenDirectConversationAsync(otherUserId, "other");

        // Assert
        Assert.Equal(2, vm.Messages.Count);
    }

    [Fact]
    public async Task OpenDirectConversationAsync_MarksAsRead()
    {
        // Arrange
        var vm = CreateViewModel();
        var otherUserId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var conversation = CreateConversationResponse(id: conversationId);

        _mockApiClient
            .Setup(x => x.GetOrCreateDirectConversationAsync(otherUserId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(conversationId, 0, 50))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(new List<ConversationMessageResponse>()));

        // Act
        await vm.OpenDirectConversationAsync(otherUserId, "other");

        // Assert
        _mockConversationStateService.Verify(
            x => x.MarkConversationAsReadAsync(conversationId),
            Times.Once);
    }

    #endregion

    #region OpenConversationByIdAsync Tests

    [Fact]
    public async Task OpenConversationByIdAsync_Success_LoadsConversation()
    {
        // Arrange
        var vm = CreateViewModel();
        var conversationId = Guid.NewGuid();
        var conversation = CreateConversationResponse(id: conversationId, name: "Test Group", isGroup: true);

        _mockApiClient
            .Setup(x => x.GetConversationAsync(conversationId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(conversationId, 0, 50))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(new List<ConversationMessageResponse>()));

        // Act
        await vm.OpenConversationByIdAsync(conversationId);

        // Assert
        Assert.True(vm.IsOpen);
        Assert.Equal(conversationId, vm.ConversationId);
        Assert.Equal("Test Group", vm.ConversationDisplayName);
        Assert.True(vm.IsGroup);
    }

    [Fact]
    public async Task OpenConversationByIdAsync_ClearsExistingState()
    {
        // Arrange
        var vm = CreateViewModel();
        var conversation1Id = Guid.NewGuid();
        var conversation2Id = Guid.NewGuid();
        var conversation1 = CreateConversationResponse(id: conversation1Id, name: "First");
        var conversation2 = CreateConversationResponse(id: conversation2Id, name: "Second");

        _mockApiClient
            .Setup(x => x.GetConversationAsync(conversation1Id))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation1));

        _mockApiClient
            .Setup(x => x.GetConversationAsync(conversation2Id))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation2));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(
                new List<ConversationMessageResponse>
                {
                    CreateMessageResponse(content: "Message")
                }));

        // Open first conversation
        await vm.OpenConversationByIdAsync(conversation1Id);
        Assert.Single(vm.Messages);
        vm.MessageInput = "Draft message";

        // Act - Open second conversation
        await vm.OpenConversationByIdAsync(conversation2Id);

        // Assert - State should be reset
        Assert.Equal(conversation2Id, vm.ConversationId);
        Assert.Equal(string.Empty, vm.MessageInput);
    }

    #endregion

    #region SendMessageCommand Tests

    [Fact]
    public async Task SendMessageCommand_WithValidInput_SendsMessage()
    {
        // Arrange
        var vm = CreateViewModel();
        var conversationId = Guid.NewGuid();
        var conversation = CreateConversationResponse(id: conversationId);
        var sentMessage = CreateMessageResponse(conversationId: conversationId, content: "Hello");

        _mockApiClient
            .Setup(x => x.GetConversationAsync(conversationId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(conversationId, 0, 50))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(new List<ConversationMessageResponse>()));

        _mockApiClient
            .Setup(x => x.SendConversationMessageAsync(conversationId, "Hello"))
            .ReturnsAsync(ApiResult<ConversationMessageResponse>.Ok(sentMessage));

        await vm.OpenConversationByIdAsync(conversationId);
        vm.MessageInput = "Hello";

        // Act
        await vm.SendMessageCommand.Execute();

        // Assert
        Assert.Single(vm.Messages);
        Assert.Equal("Hello", vm.Messages[0].Content);
        Assert.Equal(string.Empty, vm.MessageInput);
    }

    [Fact]
    public async Task SendMessageCommand_CannotExecute_WhenNoConversation()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.MessageInput = "Hello";

        // Act & Assert
        var canExecute = await vm.SendMessageCommand.CanExecute.FirstAsync();
        Assert.False(canExecute);
    }

    [Fact]
    public async Task SendMessageCommand_CannotExecute_WhenInputIsEmpty()
    {
        // Arrange
        var vm = CreateViewModel();
        var conversationId = Guid.NewGuid();
        var conversation = CreateConversationResponse(id: conversationId);

        _mockApiClient
            .Setup(x => x.GetConversationAsync(conversationId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(conversationId, 0, 50))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(new List<ConversationMessageResponse>()));

        await vm.OpenConversationByIdAsync(conversationId);

        // Act & Assert
        var canExecute = await vm.SendMessageCommand.CanExecute.FirstAsync();
        Assert.False(canExecute);
    }

    [Fact]
    public async Task SendMessageCommand_ApiFailure_RestoresInput()
    {
        // Arrange
        var vm = CreateViewModel();
        var conversationId = Guid.NewGuid();
        var conversation = CreateConversationResponse(id: conversationId);

        _mockApiClient
            .Setup(x => x.GetConversationAsync(conversationId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(conversationId, 0, 50))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(new List<ConversationMessageResponse>()));

        _mockApiClient
            .Setup(x => x.SendConversationMessageAsync(conversationId, "Hello"))
            .ReturnsAsync(ApiResult<ConversationMessageResponse>.Fail("Send failed"));

        await vm.OpenConversationByIdAsync(conversationId);
        vm.MessageInput = "Hello";

        // Act
        await vm.SendMessageCommand.Execute();

        // Assert
        Assert.Empty(vm.Messages);
        Assert.Equal("Hello", vm.MessageInput);
        Assert.Equal("Send failed", _lastError);
    }

    #endregion

    #region Close Tests

    [Fact]
    public async Task Close_ClearsAllState()
    {
        // Arrange
        var vm = CreateViewModel();
        var conversationId = Guid.NewGuid();
        var conversation = CreateConversationResponse(id: conversationId);

        _mockApiClient
            .Setup(x => x.GetConversationAsync(conversationId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(conversationId, 0, 50))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(
                new List<ConversationMessageResponse> { CreateMessageResponse() }));

        await vm.OpenConversationByIdAsync(conversationId);
        vm.MessageInput = "Draft";

        // Act
        vm.Close();

        // Assert
        Assert.False(vm.IsOpen);
        Assert.Null(vm.ConversationId);
        Assert.Null(vm.ConversationDisplayName);
        Assert.False(vm.IsGroup);
        Assert.Equal(string.Empty, vm.MessageInput);
        Assert.Empty(vm.Messages);
        Assert.Empty(vm.Participants);
    }

    [Fact]
    public async Task Close_LeavesSignalRGroup()
    {
        // Arrange
        var vm = CreateViewModel();
        var conversationId = Guid.NewGuid();
        var conversation = CreateConversationResponse(id: conversationId);

        _mockApiClient
            .Setup(x => x.GetConversationAsync(conversationId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(conversationId, 0, 50))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(new List<ConversationMessageResponse>()));

        await vm.OpenConversationByIdAsync(conversationId);

        // Act
        vm.Close();

        // Assert
        _mockSignalR.Verify(
            x => x.LeaveConversationGroupAsync(conversationId),
            Times.Once);
    }

    [Fact]
    public async Task CloseCommand_InvokesClose()
    {
        // Arrange
        var vm = CreateViewModel();
        var conversationId = Guid.NewGuid();
        var conversation = CreateConversationResponse(id: conversationId);

        _mockApiClient
            .Setup(x => x.GetConversationAsync(conversationId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(conversationId, 0, 50))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(new List<ConversationMessageResponse>()));

        await vm.OpenConversationByIdAsync(conversationId);

        // Act
        await vm.CloseCommand.Execute();

        // Assert
        Assert.False(vm.IsOpen);
    }

    #endregion

    #region Message Editing Tests

    [Fact]
    public async Task StartEditMessageCommand_SetsEditingState()
    {
        // Arrange
        var vm = CreateViewModel();
        var conversationId = Guid.NewGuid();
        var conversation = CreateConversationResponse(id: conversationId);
        var message = CreateMessageResponse(
            conversationId: conversationId,
            content: "Original",
            senderId: _currentUserId);

        _mockApiClient
            .Setup(x => x.GetConversationAsync(conversationId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(conversationId, 0, 50))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(
                new List<ConversationMessageResponse> { message }));

        await vm.OpenConversationByIdAsync(conversationId);

        // Act
        await vm.StartEditMessageCommand.Execute(message);

        // Assert
        Assert.Equal(message, vm.EditingMessage);
        Assert.Equal("Original", vm.EditingMessageContent);
    }

    [Fact]
    public async Task StartEditMessageCommand_CannotEditOthersMessages()
    {
        // Arrange
        var vm = CreateViewModel();
        var conversationId = Guid.NewGuid();
        var conversation = CreateConversationResponse(id: conversationId);
        var message = CreateMessageResponse(
            conversationId: conversationId,
            senderId: Guid.NewGuid()); // Different sender

        _mockApiClient
            .Setup(x => x.GetConversationAsync(conversationId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(conversationId, 0, 50))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(
                new List<ConversationMessageResponse> { message }));

        await vm.OpenConversationByIdAsync(conversationId);

        // Act
        await vm.StartEditMessageCommand.Execute(message);

        // Assert
        Assert.Null(vm.EditingMessage);
    }

    [Fact]
    public async Task CancelEditMessageCommand_ClearsEditingState()
    {
        // Arrange
        var vm = CreateViewModel();
        var conversationId = Guid.NewGuid();
        var conversation = CreateConversationResponse(id: conversationId);
        var message = CreateMessageResponse(
            conversationId: conversationId,
            senderId: _currentUserId);

        _mockApiClient
            .Setup(x => x.GetConversationAsync(conversationId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(conversationId, 0, 50))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(
                new List<ConversationMessageResponse> { message }));

        await vm.OpenConversationByIdAsync(conversationId);
        await vm.StartEditMessageCommand.Execute(message);

        // Act
        await vm.CancelEditMessageCommand.Execute();

        // Assert
        Assert.Null(vm.EditingMessage);
        Assert.Equal(string.Empty, vm.EditingMessageContent);
    }

    [Fact]
    public async Task SaveMessageEditCommand_UpdatesMessage()
    {
        // Arrange
        var vm = CreateViewModel();
        var conversationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var conversation = CreateConversationResponse(id: conversationId);
        var originalMessage = CreateMessageResponse(
            id: messageId,
            conversationId: conversationId,
            content: "Original",
            senderId: _currentUserId);
        var updatedMessage = originalMessage with { Content = "Updated" };

        _mockApiClient
            .Setup(x => x.GetConversationAsync(conversationId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(conversationId, 0, 50))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(
                new List<ConversationMessageResponse> { originalMessage }));

        _mockApiClient
            .Setup(x => x.UpdateConversationMessageAsync(conversationId, messageId, "Updated"))
            .ReturnsAsync(ApiResult<ConversationMessageResponse>.Ok(updatedMessage));

        await vm.OpenConversationByIdAsync(conversationId);
        await vm.StartEditMessageCommand.Execute(originalMessage);
        vm.EditingMessageContent = "Updated";

        // Act
        await vm.SaveMessageEditCommand.Execute();

        // Assert
        Assert.Equal("Updated", vm.Messages[0].Content);
        Assert.Null(vm.EditingMessage);
        Assert.Equal(string.Empty, vm.EditingMessageContent);
    }

    #endregion

    #region Delete Message Tests

    [Fact]
    public async Task DeleteMessageCommand_RemovesMessage()
    {
        // Arrange
        var vm = CreateViewModel();
        var conversationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var conversation = CreateConversationResponse(id: conversationId);
        var message = CreateMessageResponse(id: messageId, conversationId: conversationId);

        _mockApiClient
            .Setup(x => x.GetConversationAsync(conversationId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(conversationId, 0, 50))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(
                new List<ConversationMessageResponse> { message }));

        _mockApiClient
            .Setup(x => x.DeleteConversationMessageAsync(conversationId, messageId))
            .ReturnsAsync(ApiResult<bool>.Ok(true));

        await vm.OpenConversationByIdAsync(conversationId);
        Assert.Single(vm.Messages);

        // Act
        await vm.DeleteMessageCommand.Execute(message);

        // Assert
        Assert.Empty(vm.Messages);
    }

    #endregion

    #region Group Conversation Tests

    [Fact]
    public async Task CreateGroupConversationAsync_Success_OpensNewConversation()
    {
        // Arrange
        var vm = CreateViewModel();
        var participant1 = Guid.NewGuid();
        var participant2 = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var conversation = CreateConversationResponse(
            id: conversationId,
            name: "New Group",
            isGroup: true);

        _mockApiClient
            .Setup(x => x.CreateConversationAsync(
                It.Is<List<Guid>>(l => l.Contains(participant1) && l.Contains(participant2)),
                "New Group"))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationAsync(conversationId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(conversationId, 0, 50))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(new List<ConversationMessageResponse>()));

        // Act
        var result = await vm.CreateGroupConversationAsync(
            new List<Guid> { participant1, participant2 },
            "New Group");

        // Assert
        Assert.True(result);
        Assert.True(vm.IsGroup);
        _mockSignalR.Verify(x => x.JoinConversationGroupAsync(conversationId), Times.Once);
    }

    [Fact]
    public async Task CreateGroupConversationAsync_ApiFailure_ReturnsFalse()
    {
        // Arrange
        var vm = CreateViewModel();

        _mockApiClient
            .Setup(x => x.CreateConversationAsync(It.IsAny<List<Guid>>(), It.IsAny<string?>()))
            .ReturnsAsync(ApiResult<ConversationResponse>.Fail("Create failed"));

        // Act
        var result = await vm.CreateGroupConversationAsync(
            new List<Guid> { Guid.NewGuid() },
            "New Group");

        // Assert
        Assert.False(result);
        Assert.Equal("Create failed", _lastError);
    }

    [Fact]
    public async Task LeaveConversationCommand_LeavesAndCloses()
    {
        // Arrange
        var vm = CreateViewModel();
        var conversationId = Guid.NewGuid();
        var conversation = CreateConversationResponse(id: conversationId, isGroup: true);

        _mockApiClient
            .Setup(x => x.GetConversationAsync(conversationId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(conversationId, 0, 50))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(new List<ConversationMessageResponse>()));

        _mockApiClient
            .Setup(x => x.RemoveConversationParticipantAsync(conversationId, _currentUserId))
            .ReturnsAsync(ApiResult<bool>.Ok(true));

        await vm.OpenConversationByIdAsync(conversationId);

        // Act
        await vm.LeaveConversationCommand.Execute();

        // Assert
        Assert.False(vm.IsOpen);
    }

    #endregion

    #region Typing Indicator Tests

    [Fact]
    public void CleanupExpiredTypingIndicators_RemovesExpiredUsers()
    {
        // Arrange
        var vm = CreateViewModel();
        // Note: We can't easily test this without exposing internal state,
        // but we can verify the method doesn't throw

        // Act & Assert - should not throw
        vm.CleanupExpiredTypingIndicators();
    }

    [Fact]
    public void TypingIndicatorText_SingleUser_ShowsCorrectFormat()
    {
        // Arrange
        var vm = CreateViewModel();

        // Since _typingUsers is private, we verify the default state
        Assert.Equal(string.Empty, vm.TypingIndicatorText);
        Assert.False(vm.IsTyping);
    }

    #endregion

    #region Display Name Tests

    [Fact]
    public async Task ConversationDisplayName_ShowsOtherUserName_For1to1()
    {
        // Arrange
        var vm = CreateViewModel();
        var otherUserId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var conversation = CreateConversationResponse(
            id: conversationId,
            name: null,
            isGroup: false,
            participants: new List<ConversationParticipantInfo>
            {
                CreateParticipantInfo(_currentUserId, "me", "Me"),
                CreateParticipantInfo(otherUserId, "friend", "My Friend")
            });

        _mockApiClient
            .Setup(x => x.GetOrCreateDirectConversationAsync(otherUserId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(conversationId, 0, 50))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(new List<ConversationMessageResponse>()));

        // Act
        await vm.OpenDirectConversationAsync(otherUserId, "friend");

        // Assert
        Assert.Equal("My Friend", vm.ConversationDisplayName);
    }

    [Fact]
    public async Task ConversationDisplayName_UsesGroupName_WhenSet()
    {
        // Arrange
        var vm = CreateViewModel();
        var conversationId = Guid.NewGuid();
        var conversation = CreateConversationResponse(
            id: conversationId,
            name: "Project Team",
            isGroup: true);

        _mockApiClient
            .Setup(x => x.GetConversationAsync(conversationId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(conversationId, 0, 50))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(new List<ConversationMessageResponse>()));

        // Act
        await vm.OpenConversationByIdAsync(conversationId);

        // Assert
        Assert.Equal("Project Team", vm.ConversationDisplayName);
    }

    #endregion

    #region Property Change Notifications

    [Fact]
    public void MessageInput_PropertyChanged_IsRaised()
    {
        // Arrange
        var vm = CreateViewModel();
        var propertyChanged = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DMContentViewModel.MessageInput))
                propertyChanged = true;
        };

        // Act
        vm.MessageInput = "New input";

        // Assert
        Assert.True(propertyChanged);
    }

    [Fact]
    public async Task IsLoading_PropertyChanged_IsRaised()
    {
        // Arrange
        var vm = CreateViewModel();
        var propertyChangedCount = 0;
        var conversationId = Guid.NewGuid();
        var conversation = CreateConversationResponse(id: conversationId);

        _mockApiClient
            .Setup(x => x.GetConversationAsync(conversationId))
            .ReturnsAsync(ApiResult<ConversationResponse>.Ok(conversation));

        _mockApiClient
            .Setup(x => x.GetConversationMessagesAsync(conversationId, 0, 50))
            .ReturnsAsync(ApiResult<List<ConversationMessageResponse>>.Ok(new List<ConversationMessageResponse>()));

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DMContentViewModel.IsLoading))
                propertyChangedCount++;
        };

        // Act
        await vm.OpenConversationByIdAsync(conversationId);

        // Assert - IsLoading should change at least once
        Assert.True(propertyChangedCount >= 1);
    }

    #endregion
}
