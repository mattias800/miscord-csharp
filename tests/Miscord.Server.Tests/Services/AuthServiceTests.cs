using Microsoft.Extensions.Options;
using Miscord.Server.DTOs;
using Miscord.Server.Services;

namespace Miscord.Server.Tests.Services;

[TestClass]
public class AuthServiceTests
{
    private static IOptions<JwtSettings> CreateJwtSettings() => Options.Create(new JwtSettings
    {
        SecretKey = "ThisIsATestSecretKeyThatIsLongEnoughForHmacSha256!",
        Issuer = "TestIssuer",
        Audience = "TestAudience",
        AccessTokenExpirationMinutes = 60,
        RefreshTokenExpirationDays = 7
    });

    [TestMethod]
    public async Task RegisterAsync_WithValidData_CreatesUserAndReturnsTokens()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service = new AuthService(db, CreateJwtSettings());
        var request = new RegisterRequest("testuser", "test@example.com", "Password123!");

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("testuser", result.Username);
        Assert.AreEqual("test@example.com", result.Email);
        Assert.IsFalse(string.IsNullOrEmpty(result.AccessToken));
        Assert.IsFalse(string.IsNullOrEmpty(result.RefreshToken));
        Assert.IsTrue(result.ExpiresAt > DateTime.UtcNow);
    }

    [TestMethod]
    public async Task RegisterAsync_WithDuplicateEmail_ThrowsException()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service = new AuthService(db, CreateJwtSettings());
        await service.RegisterAsync(new RegisterRequest("user1", "duplicate@example.com", "Password123!"));

        // Act & Assert
        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => service.RegisterAsync(new RegisterRequest("user2", "duplicate@example.com", "Password123!")));
        Assert.AreEqual("Email is already registered.", exception.Message);
    }

    [TestMethod]
    public async Task RegisterAsync_WithDuplicateUsername_ThrowsException()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service = new AuthService(db, CreateJwtSettings());
        await service.RegisterAsync(new RegisterRequest("duplicateuser", "user1@example.com", "Password123!"));

        // Act & Assert
        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => service.RegisterAsync(new RegisterRequest("duplicateuser", "user2@example.com", "Password123!")));
        Assert.AreEqual("Username is already taken.", exception.Message);
    }

    [TestMethod]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokens()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service = new AuthService(db, CreateJwtSettings());
        await service.RegisterAsync(new RegisterRequest("testuser", "test@example.com", "Password123!"));

        // Act
        var result = await service.LoginAsync(new LoginRequest("test@example.com", "Password123!"));

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("testuser", result.Username);
        Assert.IsFalse(string.IsNullOrEmpty(result.AccessToken));
    }

    [TestMethod]
    public async Task LoginAsync_WithInvalidPassword_ThrowsException()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service = new AuthService(db, CreateJwtSettings());
        await service.RegisterAsync(new RegisterRequest("testuser", "test@example.com", "Password123!"));

        // Act & Assert
        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => service.LoginAsync(new LoginRequest("test@example.com", "WrongPassword!")));
        Assert.AreEqual("Invalid email or password.", exception.Message);
    }

    [TestMethod]
    public async Task LoginAsync_WithNonExistentEmail_ThrowsException()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service = new AuthService(db, CreateJwtSettings());

        // Act & Assert
        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => service.LoginAsync(new LoginRequest("nonexistent@example.com", "Password123!")));
        Assert.AreEqual("Invalid email or password.", exception.Message);
    }

    [TestMethod]
    public async Task GetProfileAsync_WithValidUserId_ReturnsProfile()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service = new AuthService(db, CreateJwtSettings());
        var authResult = await service.RegisterAsync(new RegisterRequest("testuser", "test@example.com", "Password123!"));

        // Act
        var profile = await service.GetProfileAsync(authResult.UserId);

        // Assert
        Assert.IsNotNull(profile);
        Assert.AreEqual("testuser", profile.Username);
        Assert.AreEqual("test@example.com", profile.Email);
    }

    [TestMethod]
    public async Task GetProfileAsync_WithInvalidUserId_ThrowsException()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service = new AuthService(db, CreateJwtSettings());

        // Act & Assert
        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => service.GetProfileAsync(Guid.NewGuid()));
        Assert.AreEqual("User not found.", exception.Message);
    }

    [TestMethod]
    public async Task UpdateProfileAsync_WithValidData_UpdatesProfile()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service = new AuthService(db, CreateJwtSettings());
        var authResult = await service.RegisterAsync(new RegisterRequest("testuser", "test@example.com", "Password123!"));
        var updateRequest = new UpdateProfileRequest("newusername", "avatar.png", "Hello!");

        // Act
        var profile = await service.UpdateProfileAsync(authResult.UserId, updateRequest);

        // Assert
        Assert.AreEqual("newusername", profile.Username);
        Assert.AreEqual("avatar.png", profile.Avatar);
        Assert.AreEqual("Hello!", profile.Status);
    }

    [TestMethod]
    public async Task UpdateProfileAsync_WithDuplicateUsername_ThrowsException()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service = new AuthService(db, CreateJwtSettings());
        await service.RegisterAsync(new RegisterRequest("existinguser", "existing@example.com", "Password123!"));
        var authResult = await service.RegisterAsync(new RegisterRequest("testuser", "test@example.com", "Password123!"));

        // Act & Assert
        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => service.UpdateProfileAsync(authResult.UserId, new UpdateProfileRequest("existinguser", null, null)));
        Assert.AreEqual("Username is already taken.", exception.Message);
    }

    [TestMethod]
    public async Task RefreshTokenAsync_WithValidToken_ReturnsNewTokens()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service = new AuthService(db, CreateJwtSettings());
        var authResult = await service.RegisterAsync(new RegisterRequest("testuser", "test@example.com", "Password123!"));

        // Act
        var newTokens = await service.RefreshTokenAsync(authResult.RefreshToken);

        // Assert
        Assert.IsNotNull(newTokens);
        Assert.AreEqual(authResult.UserId, newTokens.UserId);
        Assert.IsFalse(string.IsNullOrEmpty(newTokens.AccessToken));
    }
}
