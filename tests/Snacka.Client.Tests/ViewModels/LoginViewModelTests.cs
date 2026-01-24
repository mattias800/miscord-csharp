using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Moq;
using ReactiveUI;
using Snacka.Client.Services;
using Snacka.Client.ViewModels;

namespace Snacka.Client.Tests.ViewModels;

public class LoginViewModelTests : IDisposable
{
    private readonly Mock<IApiClient> _mockApiClient;
    private readonly List<AuthResponse> _loginSuccessCalls;
    private readonly List<object> _switchToRegisterCalls;

    public LoginViewModelTests()
    {
        _mockApiClient = new Mock<IApiClient>();
        _loginSuccessCalls = new List<AuthResponse>();
        _switchToRegisterCalls = new List<object>();
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    private LoginViewModel CreateViewModel(bool withRegisterCallback = true)
    {
        return new LoginViewModel(
            _mockApiClient.Object,
            response => _loginSuccessCalls.Add(response),
            withRegisterCallback ? () => _switchToRegisterCalls.Add(new object()) : null
        );
    }

    private static AuthResponse CreateAuthResponse(
        Guid? userId = null,
        string username = "testuser",
        string email = "test@example.com")
    {
        return new AuthResponse(
            UserId: userId ?? Guid.NewGuid(),
            Username: username,
            Email: email,
            IsServerAdmin: false,
            AccessToken: "test-access-token",
            RefreshToken: "test-refresh-token",
            ExpiresAt: DateTime.UtcNow.AddHours(1)
        );
    }

    #region Property Tests

    [Fact]
    public void Email_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        var vm = CreateViewModel();
        var propertyChangedRaised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.Email))
                propertyChangedRaised = true;
        };

        // Act
        vm.Email = "test@example.com";

        // Assert
        Assert.True(propertyChangedRaised);
        Assert.Equal("test@example.com", vm.Email);
    }

    [Fact]
    public void Password_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        var vm = CreateViewModel();
        var propertyChangedRaised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.Password))
                propertyChangedRaised = true;
        };

        // Act
        vm.Password = "secret123";

        // Assert
        Assert.True(propertyChangedRaised);
        Assert.Equal("secret123", vm.Password);
    }

    [Fact]
    public void IsLoading_InitialValue_IsFalse()
    {
        // Arrange & Act
        var vm = CreateViewModel();

        // Assert
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public void ErrorMessage_InitialValue_IsNull()
    {
        // Arrange & Act
        var vm = CreateViewModel();

        // Assert
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public void CanRegister_WithRegisterCallback_ReturnsTrue()
    {
        // Arrange & Act
        var vm = CreateViewModel(withRegisterCallback: true);

        // Assert
        Assert.True(vm.CanRegister);
        Assert.NotNull(vm.SwitchToRegisterCommand);
    }

    [Fact]
    public void CanRegister_WithoutRegisterCallback_ReturnsFalse()
    {
        // Arrange & Act
        var vm = CreateViewModel(withRegisterCallback: false);

        // Assert
        Assert.False(vm.CanRegister);
        Assert.Null(vm.SwitchToRegisterCommand);
    }

    #endregion

    #region LoginCommand Tests

    [Fact]
    public async Task LoginCommand_SuccessfulLogin_CallsOnLoginSuccess()
    {
        // Arrange
        var expectedResponse = CreateAuthResponse(username: "testuser");
        _mockApiClient
            .Setup(x => x.LoginAsync("test@example.com", "password123"))
            .ReturnsAsync(new ApiResult<AuthResponse> { Success = true, Data = expectedResponse });

        var vm = CreateViewModel();
        vm.Email = "test@example.com";
        vm.Password = "password123";

        // Act
        await vm.LoginCommand.Execute();

        // Assert
        Assert.Single(_loginSuccessCalls);
        Assert.Equal(expectedResponse.Username, _loginSuccessCalls[0].Username);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task LoginCommand_FailedLogin_SetsErrorMessage()
    {
        // Arrange
        _mockApiClient
            .Setup(x => x.LoginAsync("test@example.com", "wrongpassword"))
            .ReturnsAsync(new ApiResult<AuthResponse> { Success = false, Error = "Invalid credentials" });

        var vm = CreateViewModel();
        vm.Email = "test@example.com";
        vm.Password = "wrongpassword";

        // Act
        await vm.LoginCommand.Execute();

        // Assert
        Assert.Empty(_loginSuccessCalls);
        Assert.Equal("Invalid credentials", vm.ErrorMessage);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task LoginCommand_FailedLoginWithNullError_SetsDefaultErrorMessage()
    {
        // Arrange
        _mockApiClient
            .Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new ApiResult<AuthResponse> { Success = false, Error = null });

        var vm = CreateViewModel();
        vm.Email = "test@example.com";
        vm.Password = "password";

        // Act
        await vm.LoginCommand.Execute();

        // Assert
        Assert.Empty(_loginSuccessCalls);
        Assert.Equal("Login failed", vm.ErrorMessage);
    }

    [Fact]
    public async Task LoginCommand_Exception_SetsErrorMessage()
    {
        // Arrange
        _mockApiClient
            .Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Network error"));

        var vm = CreateViewModel();
        vm.Email = "test@example.com";
        vm.Password = "password";

        // Act
        await vm.LoginCommand.Execute();

        // Assert
        Assert.Empty(_loginSuccessCalls);
        Assert.Equal("Error: Network error", vm.ErrorMessage);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task LoginCommand_WhileLoading_IsDisabled()
    {
        // Arrange
        var tcs = new TaskCompletionSource<ApiResult<AuthResponse>>();
        _mockApiClient
            .Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(tcs.Task);

        var vm = CreateViewModel();
        vm.Email = "test@example.com";
        vm.Password = "password";

        // Act - start login (don't await)
        var loginTask = vm.LoginCommand.Execute().ToTask();

        // Assert - command should be disabled while loading
        Assert.True(vm.IsLoading);
        var canExecute = await vm.LoginCommand.CanExecute.FirstAsync();
        Assert.False(canExecute);

        // Complete the login
        tcs.SetResult(new ApiResult<AuthResponse> { Success = false, Error = "test" });
        await loginTask;

        // Assert - command should be enabled again
        Assert.False(vm.IsLoading);
        canExecute = await vm.LoginCommand.CanExecute.FirstAsync();
        Assert.True(canExecute);
    }

    [Fact]
    public async Task LoginCommand_ClearsErrorMessage_BeforeAttempt()
    {
        // Arrange
        _mockApiClient
            .Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new ApiResult<AuthResponse> { Success = true, Data = CreateAuthResponse() });

        var vm = CreateViewModel();
        vm.Email = "test@example.com";
        vm.Password = "password";
        vm.ErrorMessage = "Previous error";

        // Act
        await vm.LoginCommand.Execute();

        // Assert
        Assert.Null(vm.ErrorMessage);
    }

    #endregion

    #region SwitchToRegisterCommand Tests

    [Fact]
    public async Task SwitchToRegisterCommand_Executes_CallsCallback()
    {
        // Arrange
        var vm = CreateViewModel(withRegisterCallback: true);

        // Act
        await vm.SwitchToRegisterCommand!.Execute();

        // Assert
        Assert.Single(_switchToRegisterCalls);
    }

    #endregion
}
