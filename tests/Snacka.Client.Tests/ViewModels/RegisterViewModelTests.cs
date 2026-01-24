using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Moq;
using ReactiveUI;
using Snacka.Client.Services;
using Snacka.Client.ViewModels;

namespace Snacka.Client.Tests.ViewModels;

public class RegisterViewModelTests : IDisposable
{
    private readonly Mock<IApiClient> _mockApiClient;
    private readonly List<AuthResponse> _registerSuccessCalls;
    private readonly List<object> _switchToLoginCalls;

    public RegisterViewModelTests()
    {
        _mockApiClient = new Mock<IApiClient>();
        _registerSuccessCalls = new List<AuthResponse>();
        _switchToLoginCalls = new List<object>();
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    private RegisterViewModel CreateViewModel(string? initialInviteCode = null)
    {
        return new RegisterViewModel(
            _mockApiClient.Object,
            response => _registerSuccessCalls.Add(response),
            () => _switchToLoginCalls.Add(new object()),
            initialInviteCode
        );
    }

    private static AuthResponse CreateAuthResponse(
        Guid? userId = null,
        string username = "newuser",
        string email = "new@example.com")
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
    public void Username_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        var vm = CreateViewModel();
        var propertyChangedRaised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.Username))
                propertyChangedRaised = true;
        };

        // Act
        vm.Username = "newuser";

        // Assert
        Assert.True(propertyChangedRaised);
        Assert.Equal("newuser", vm.Username);
    }

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
    public void ConfirmPassword_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        var vm = CreateViewModel();
        var propertyChangedRaised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.ConfirmPassword))
                propertyChangedRaised = true;
        };

        // Act
        vm.ConfirmPassword = "secret123";

        // Assert
        Assert.True(propertyChangedRaised);
        Assert.Equal("secret123", vm.ConfirmPassword);
    }

    [Fact]
    public void InviteCode_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        var vm = CreateViewModel();
        var propertyChangedRaised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.InviteCode))
                propertyChangedRaised = true;
        };

        // Act
        vm.InviteCode = "INVITE123";

        // Assert
        Assert.True(propertyChangedRaised);
        Assert.Equal("INVITE123", vm.InviteCode);
    }

    [Fact]
    public void InviteCode_WithInitialValue_SetsInitialValue()
    {
        // Arrange & Act
        var vm = CreateViewModel(initialInviteCode: "PREINVITE");

        // Assert
        Assert.Equal("PREINVITE", vm.InviteCode);
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

    #endregion

    #region RegisterCommand Tests

    [Fact]
    public async Task RegisterCommand_SuccessfulRegistration_CallsOnRegisterSuccess()
    {
        // Arrange
        var expectedResponse = CreateAuthResponse(username: "newuser");
        _mockApiClient
            .Setup(x => x.RegisterAsync("newuser", "new@example.com", "password123", "INVITE123"))
            .ReturnsAsync(new ApiResult<AuthResponse> { Success = true, Data = expectedResponse });

        var vm = CreateViewModel();
        vm.Username = "newuser";
        vm.Email = "new@example.com";
        vm.Password = "password123";
        vm.InviteCode = "INVITE123";

        // Act
        await vm.RegisterCommand.Execute();

        // Assert
        Assert.Single(_registerSuccessCalls);
        Assert.Equal(expectedResponse.Username, _registerSuccessCalls[0].Username);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task RegisterCommand_FailedRegistration_SetsErrorMessage()
    {
        // Arrange
        _mockApiClient
            .Setup(x => x.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new ApiResult<AuthResponse> { Success = false, Error = "Email already registered" });

        var vm = CreateViewModel();
        vm.Username = "newuser";
        vm.Email = "existing@example.com";
        vm.Password = "password123";
        vm.InviteCode = "INVITE123";

        // Act
        await vm.RegisterCommand.Execute();

        // Assert
        Assert.Empty(_registerSuccessCalls);
        Assert.Equal("Email already registered", vm.ErrorMessage);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task RegisterCommand_FailedWithNullError_SetsDefaultErrorMessage()
    {
        // Arrange
        _mockApiClient
            .Setup(x => x.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new ApiResult<AuthResponse> { Success = false, Error = null });

        var vm = CreateViewModel();
        vm.Username = "newuser";
        vm.Email = "new@example.com";
        vm.Password = "password123";
        vm.InviteCode = "INVITE123";

        // Act
        await vm.RegisterCommand.Execute();

        // Assert
        Assert.Empty(_registerSuccessCalls);
        Assert.Equal("Registration failed", vm.ErrorMessage);
    }

    [Fact]
    public async Task RegisterCommand_WhileLoading_IsDisabled()
    {
        // Arrange
        var tcs = new TaskCompletionSource<ApiResult<AuthResponse>>();
        _mockApiClient
            .Setup(x => x.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(tcs.Task);

        var vm = CreateViewModel();
        vm.Username = "newuser";
        vm.Email = "new@example.com";
        vm.Password = "password123";
        vm.InviteCode = "INVITE123";

        // Act - start registration (don't await)
        var registerTask = vm.RegisterCommand.Execute().ToTask();

        // Assert - command should be disabled while loading
        Assert.True(vm.IsLoading);
        var canExecute = await vm.RegisterCommand.CanExecute.FirstAsync();
        Assert.False(canExecute);

        // Complete the registration
        tcs.SetResult(new ApiResult<AuthResponse> { Success = false, Error = "test" });
        await registerTask;

        // Assert - command should be enabled again
        Assert.False(vm.IsLoading);
        canExecute = await vm.RegisterCommand.CanExecute.FirstAsync();
        Assert.True(canExecute);
    }

    [Fact]
    public async Task RegisterCommand_SetsIsLoading_DuringExecution()
    {
        // Arrange
        var isLoadingValues = new List<bool>();
        var tcs = new TaskCompletionSource<ApiResult<AuthResponse>>();
        _mockApiClient
            .Setup(x => x.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(tcs.Task);

        var vm = CreateViewModel();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.IsLoading))
                isLoadingValues.Add(vm.IsLoading);
        };
        vm.Username = "newuser";
        vm.Email = "new@example.com";
        vm.Password = "password123";
        vm.InviteCode = "INVITE123";

        // Act
        var registerTask = vm.RegisterCommand.Execute().ToTask();
        tcs.SetResult(new ApiResult<AuthResponse> { Success = true, Data = CreateAuthResponse() });
        await registerTask;

        // Assert - should have transitioned from false -> true -> false
        Assert.Equal(2, isLoadingValues.Count);
        Assert.True(isLoadingValues[0]); // Set to true at start
        Assert.False(isLoadingValues[1]); // Set to false at end
    }

    #endregion

    #region SwitchToLoginCommand Tests

    [Fact]
    public async Task SwitchToLoginCommand_Executes_CallsCallback()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        await vm.SwitchToLoginCommand.Execute();

        // Assert
        Assert.Single(_switchToLoginCalls);
    }

    #endregion
}
