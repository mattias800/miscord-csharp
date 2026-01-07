using System.Net;
using System.Net.Http.Json;
using Miscord.Server.DTOs;

namespace Miscord.Server.Tests.Api;

[TestClass]
public class AdminApiTests
{
    [TestMethod]
    public async Task GetInvites_AsAdmin_ReturnsInvites()
    {
        // Arrange
        using var test = new IntegrationTestBase();
        var admin = await test.RegisterUserAsync("admin", "admin@example.com", "Password123!");
        test.SetAuthToken(admin.AccessToken);

        // Act
        var response = await test.Client.GetAsync("/api/admin/invites");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var invites = await response.Content.ReadFromJsonAsync<ServerInviteResponse[]>();
        Assert.IsNotNull(invites);
    }

    [TestMethod]
    public async Task GetInvites_AsNonAdmin_ReturnsForbidden()
    {
        // Arrange
        using var test = new IntegrationTestBase();
        // First user is admin
        await test.RegisterUserAsync("admin", "admin@example.com", "Password123!");
        // Second user is not admin
        var user = await test.RegisterUserAsync("user", "user@example.com", "Password123!");
        test.SetAuthToken(user.AccessToken);

        // Act
        var response = await test.Client.GetAsync("/api/admin/invites");

        // Assert
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task CreateInvite_AsAdmin_CreatesInvite()
    {
        // Arrange
        using var test = new IntegrationTestBase();
        var admin = await test.RegisterUserAsync("admin", "admin@example.com", "Password123!");
        test.SetAuthToken(admin.AccessToken);

        // Act
        var response = await test.Client.PostAsJsonAsync("/api/admin/invites", new CreateInviteRequest(MaxUses: 5));

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var invite = await response.Content.ReadFromJsonAsync<ServerInviteResponse>();
        Assert.IsNotNull(invite);
        Assert.AreEqual(5, invite.MaxUses);
        Assert.IsFalse(string.IsNullOrEmpty(invite.Code));
    }

    [TestMethod]
    public async Task RevokeInvite_AsAdmin_RevokesInvite()
    {
        // Arrange
        using var test = new IntegrationTestBase();
        var admin = await test.RegisterUserAsync("admin", "admin@example.com", "Password123!");
        test.SetAuthToken(admin.AccessToken);

        // Create an invite first
        var createResponse = await test.Client.PostAsJsonAsync("/api/admin/invites", new CreateInviteRequest());
        var invite = await createResponse.Content.ReadFromJsonAsync<ServerInviteResponse>();
        Assert.IsNotNull(invite);

        // Act
        var response = await test.Client.DeleteAsync($"/api/admin/invites/{invite.Id}");

        // Assert
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    [TestMethod]
    public async Task GetUsers_AsAdmin_ReturnsUsers()
    {
        // Arrange
        using var test = new IntegrationTestBase();
        var admin = await test.RegisterUserAsync("admin", "admin@example.com", "Password123!");
        test.SetAuthToken(admin.AccessToken);

        // Act
        var response = await test.Client.GetAsync("/api/admin/users");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<AdminUserResponse[]>();
        Assert.IsNotNull(users);
        Assert.AreEqual(1, users.Length);
        Assert.AreEqual("admin", users[0].Username);
        Assert.IsTrue(users[0].IsServerAdmin);
    }

    [TestMethod]
    public async Task SetUserAdminStatus_PromoteUser_MakesUserAdmin()
    {
        // Arrange
        using var test = new IntegrationTestBase();
        var admin = await test.RegisterUserAsync("admin", "admin@example.com", "Password123!");
        var user = await test.RegisterUserAsync("user", "user@example.com", "Password123!");
        test.SetAuthToken(admin.AccessToken);

        // Act
        var response = await test.Client.PutAsJsonAsync(
            $"/api/admin/users/{user.UserId}/admin",
            new SetAdminStatusRequest(true));

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var updatedUser = await response.Content.ReadFromJsonAsync<AdminUserResponse>();
        Assert.IsNotNull(updatedUser);
        Assert.IsTrue(updatedUser.IsServerAdmin);
    }

    [TestMethod]
    public async Task SetUserAdminStatus_DemoteSelf_ReturnsBadRequest()
    {
        // Arrange
        using var test = new IntegrationTestBase();
        var admin = await test.RegisterUserAsync("admin", "admin@example.com", "Password123!");
        test.SetAuthToken(admin.AccessToken);

        // Act
        var response = await test.Client.PutAsJsonAsync(
            $"/api/admin/users/{admin.UserId}/admin",
            new SetAdminStatusRequest(false));

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task DeleteUser_AsAdmin_DeletesUser()
    {
        // Arrange
        using var test = new IntegrationTestBase();
        var admin = await test.RegisterUserAsync("admin", "admin@example.com", "Password123!");
        var user = await test.RegisterUserAsync("user", "user@example.com", "Password123!");
        test.SetAuthToken(admin.AccessToken);

        // Act
        var response = await test.Client.DeleteAsync($"/api/admin/users/{user.UserId}");

        // Assert
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);

        // Verify user is deleted
        var usersResponse = await test.Client.GetAsync("/api/admin/users");
        var users = await usersResponse.Content.ReadFromJsonAsync<AdminUserResponse[]>();
        Assert.IsNotNull(users);
        Assert.AreEqual(1, users.Length);
        Assert.AreEqual("admin", users[0].Username);
    }

    [TestMethod]
    public async Task DeleteUser_DeleteSelf_ReturnsBadRequest()
    {
        // Arrange
        using var test = new IntegrationTestBase();
        var admin = await test.RegisterUserAsync("admin", "admin@example.com", "Password123!");
        test.SetAuthToken(admin.AccessToken);

        // Act
        var response = await test.Client.DeleteAsync($"/api/admin/users/{admin.UserId}");

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
