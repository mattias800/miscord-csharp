using System.Net;
using System.Net.Http.Json;
using Miscord.Server.DTOs;

namespace Miscord.Server.Tests.Api;

[TestClass]
public class AuthApiTests
{
    [TestMethod]
    public async Task Register_WithValidData_ReturnsTokens()
    {
        // Arrange
        using var test = new IntegrationTestBase();
        var request = new RegisterRequest("testuser", "test@example.com", "Password123!");

        // Act
        var response = await test.Client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.IsNotNull(auth);
        Assert.AreEqual("testuser", auth.Username);
        Assert.IsFalse(string.IsNullOrEmpty(auth.AccessToken));
    }

    [TestMethod]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        using var test = new IntegrationTestBase();
        await test.RegisterUserAsync("user1", "duplicate@example.com", "Password123!");

        // Act
        var response = await test.Client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("user2", "duplicate@example.com", "Password123!"));

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        // Arrange
        using var test = new IntegrationTestBase();
        await test.RegisterUserAsync("testuser", "test@example.com", "Password123!");

        // Act
        var response = await test.Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("test@example.com", "Password123!"));

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.IsNotNull(auth);
        Assert.AreEqual("testuser", auth.Username);
    }

    [TestMethod]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        using var test = new IntegrationTestBase();
        await test.RegisterUserAsync("testuser", "test@example.com", "Password123!");

        // Act
        var response = await test.Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("test@example.com", "WrongPassword!"));

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task RefreshToken_WithValidToken_ReturnsNewTokens()
    {
        // Arrange
        using var test = new IntegrationTestBase();
        var auth = await test.RegisterUserAsync("testuser", "test@example.com", "Password123!");

        // Act
        var response = await test.Client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest(auth.RefreshToken));

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var newAuth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.IsNotNull(newAuth);
        Assert.AreEqual(auth.UserId, newAuth.UserId);
    }

    [TestMethod]
    public async Task GetProfile_WithValidToken_ReturnsProfile()
    {
        // Arrange
        using var test = new IntegrationTestBase();
        var auth = await test.RegisterUserAsync("testuser", "test@example.com", "Password123!");
        test.SetAuthToken(auth.AccessToken);

        // Act
        var response = await test.Client.GetAsync("/api/users/me");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.IsNotNull(profile);
        Assert.AreEqual("testuser", profile.Username);
        Assert.AreEqual("test@example.com", profile.Email);
    }

    [TestMethod]
    public async Task GetProfile_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        using var test = new IntegrationTestBase();

        // Act
        var response = await test.Client.GetAsync("/api/users/me");

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task UpdateProfile_WithValidData_UpdatesProfile()
    {
        // Arrange
        using var test = new IntegrationTestBase();
        var auth = await test.RegisterUserAsync("testuser", "test@example.com", "Password123!");
        test.SetAuthToken(auth.AccessToken);

        // Act
        var response = await test.Client.PutAsJsonAsync("/api/users/me",
            new UpdateProfileRequest("newname", "avatar.png", "Hello!"));

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.IsNotNull(profile);
        Assert.AreEqual("newname", profile.Username);
        Assert.AreEqual("avatar.png", profile.Avatar);
        Assert.AreEqual("Hello!", profile.Status);
    }
}
