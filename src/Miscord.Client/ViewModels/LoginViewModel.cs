using System.Reactive;
using System.Reactive.Linq;
using Miscord.Client.Services;
using ReactiveUI;

namespace Miscord.Client.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly Action<AuthResponse> _onLoginSuccess;
    private readonly Action? _onSwitchToRegister;

    private string _email = string.Empty;
    private string _password = string.Empty;
    private string? _errorMessage;
    private bool _isLoading;

    public LoginViewModel(IApiClient apiClient, Action<AuthResponse> onLoginSuccess, Action? onSwitchToRegister)
    {
        _apiClient = apiClient;
        _onLoginSuccess = onLoginSuccess;
        _onSwitchToRegister = onSwitchToRegister;

        var canLogin = this.WhenAnyValue(x => x.IsLoading, isLoading => !isLoading);

        LoginCommand = ReactiveCommand.CreateFromTask(LoginAsync, canLogin);
        SwitchToRegisterCommand = _onSwitchToRegister is not null
            ? ReactiveCommand.Create(() => _onSwitchToRegister())
            : null;
    }

    public bool CanRegister => _onSwitchToRegister is not null;

    public string Email
    {
        get => _email;
        set => this.RaiseAndSetIfChanged(ref _email, value);
    }

    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public ReactiveCommand<Unit, Unit> LoginCommand { get; }
    public ReactiveCommand<Unit, Unit>? SwitchToRegisterCommand { get; }

    private async Task LoginAsync()
    {
        ErrorMessage = null;
        IsLoading = true;

        try
        {
            Console.WriteLine($"Attempting login with email: {Email}");
            var result = await _apiClient.LoginAsync(Email, Password);
            Console.WriteLine($"Login result: Success={result.Success}, Error={result.Error}");
            if (result.Success && result.Data is not null)
            {
                Console.WriteLine("Login successful!");
                _onLoginSuccess(result.Data);
            }
            else
            {
                ErrorMessage = result.Error ?? "Login failed";
                Console.WriteLine($"Login failed: {ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Login exception: {ex}");
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
