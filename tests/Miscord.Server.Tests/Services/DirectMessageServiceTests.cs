using Miscord.Server.Services;
using Miscord.Shared.Models;

namespace Miscord.Server.Tests.Services;

[TestClass]
public class DirectMessageServiceTests
{
    private static async Task<(User sender, User recipient)> CreateTestUsersAsync(Data.MiscordDbContext db)
    {
        var sender = new User
        {
            Username = "sender",
            Email = "sender@example.com",
            PasswordHash = "hash"
        };
        var recipient = new User
        {
            Username = "recipient",
            Email = "recipient@example.com",
            PasswordHash = "hash"
        };

        db.Users.AddRange(sender, recipient);
        await db.SaveChangesAsync();
        return (sender, recipient);
    }

    [TestMethod]
    public async Task SendMessageAsync_WithValidUsers_CreatesMessage()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var (sender, recipient) = await CreateTestUsersAsync(db);
        var service = new DirectMessageService(db);

        // Act
        var message = await service.SendMessageAsync(sender.Id, recipient.Id, "Hello!");

        // Assert
        Assert.IsNotNull(message);
        Assert.AreEqual("Hello!", message.Content);
        Assert.AreEqual(sender.Id, message.SenderId);
        Assert.AreEqual(recipient.Id, message.RecipientId);
        Assert.AreEqual("sender", message.SenderUsername);
        Assert.IsFalse(message.IsRead);
    }

    [TestMethod]
    public async Task SendMessageAsync_WithNonExistentSender_ThrowsException()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var (_, recipient) = await CreateTestUsersAsync(db);
        var service = new DirectMessageService(db);

        // Act & Assert
        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => service.SendMessageAsync(Guid.NewGuid(), recipient.Id, "Hello!"));
        Assert.AreEqual("Sender not found.", exception.Message);
    }

    [TestMethod]
    public async Task SendMessageAsync_WithNonExistentRecipient_ThrowsException()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var (sender, _) = await CreateTestUsersAsync(db);
        var service = new DirectMessageService(db);

        // Act & Assert
        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => service.SendMessageAsync(sender.Id, Guid.NewGuid(), "Hello!"));
        Assert.AreEqual("Recipient not found.", exception.Message);
    }

    [TestMethod]
    public async Task GetConversationAsync_ReturnsMessagesInOrder()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var (sender, recipient) = await CreateTestUsersAsync(db);
        var service = new DirectMessageService(db);

        await service.SendMessageAsync(sender.Id, recipient.Id, "Message 1");
        await service.SendMessageAsync(recipient.Id, sender.Id, "Message 2");
        await service.SendMessageAsync(sender.Id, recipient.Id, "Message 3");

        // Act
        var messages = (await service.GetConversationAsync(sender.Id, recipient.Id)).ToList();

        // Assert
        Assert.AreEqual(3, messages.Count);
        Assert.AreEqual("Message 1", messages[0].Content);
        Assert.AreEqual("Message 2", messages[1].Content);
        Assert.AreEqual("Message 3", messages[2].Content);
    }

    [TestMethod]
    public async Task GetConversationAsync_WithPagination_ReturnsCorrectSubset()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var (sender, recipient) = await CreateTestUsersAsync(db);
        var service = new DirectMessageService(db);

        for (int i = 1; i <= 10; i++)
        {
            await service.SendMessageAsync(sender.Id, recipient.Id, $"Message {i}");
        }

        // Act
        var messages = (await service.GetConversationAsync(sender.Id, recipient.Id, skip: 0, take: 3)).ToList();

        // Assert
        Assert.AreEqual(3, messages.Count);
    }

    [TestMethod]
    public async Task UpdateMessageAsync_ByAuthor_UpdatesContent()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var (sender, recipient) = await CreateTestUsersAsync(db);
        var service = new DirectMessageService(db);
        var message = await service.SendMessageAsync(sender.Id, recipient.Id, "Original");

        // Act
        var updated = await service.UpdateMessageAsync(message.Id, sender.Id, "Updated");

        // Assert
        Assert.AreEqual("Updated", updated.Content);
    }

    [TestMethod]
    public async Task UpdateMessageAsync_ByNonAuthor_ThrowsException()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var (sender, recipient) = await CreateTestUsersAsync(db);
        var service = new DirectMessageService(db);
        var message = await service.SendMessageAsync(sender.Id, recipient.Id, "Original");

        // Act & Assert
        await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(
            () => service.UpdateMessageAsync(message.Id, recipient.Id, "Updated"));
    }

    [TestMethod]
    public async Task DeleteMessageAsync_ByAuthor_DeletesMessage()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var (sender, recipient) = await CreateTestUsersAsync(db);
        var service = new DirectMessageService(db);
        var message = await service.SendMessageAsync(sender.Id, recipient.Id, "To delete");

        // Act
        await service.DeleteMessageAsync(message.Id, sender.Id);

        // Assert
        var messages = await service.GetConversationAsync(sender.Id, recipient.Id);
        Assert.AreEqual(0, messages.Count());
    }

    [TestMethod]
    public async Task DeleteMessageAsync_ByNonAuthor_ThrowsException()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var (sender, recipient) = await CreateTestUsersAsync(db);
        var service = new DirectMessageService(db);
        var message = await service.SendMessageAsync(sender.Id, recipient.Id, "To delete");

        // Act & Assert
        await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(
            () => service.DeleteMessageAsync(message.Id, recipient.Id));
    }

    [TestMethod]
    public async Task GetConversationsAsync_ReturnsConversationSummaries()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var (sender, recipient) = await CreateTestUsersAsync(db);
        var service = new DirectMessageService(db);

        await service.SendMessageAsync(sender.Id, recipient.Id, "Hello!");
        await service.SendMessageAsync(recipient.Id, sender.Id, "Hi there!");

        // Act
        var conversations = (await service.GetConversationsAsync(sender.Id)).ToList();

        // Assert
        Assert.AreEqual(1, conversations.Count);
        Assert.AreEqual(recipient.Id, conversations[0].UserId);
        Assert.AreEqual("recipient", conversations[0].Username);
        Assert.AreEqual("Hi there!", conversations[0].LastMessage?.Content);
    }

    [TestMethod]
    public async Task MarkAsReadAsync_MarksMessagesAsRead()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var (sender, recipient) = await CreateTestUsersAsync(db);
        var service = new DirectMessageService(db);

        await service.SendMessageAsync(sender.Id, recipient.Id, "Message 1");
        await service.SendMessageAsync(sender.Id, recipient.Id, "Message 2");

        // Act
        await service.MarkAsReadAsync(recipient.Id, sender.Id);

        // Assert
        var messages = (await service.GetConversationAsync(sender.Id, recipient.Id)).ToList();
        Assert.IsTrue(messages.All(m => m.IsRead));
    }
}
