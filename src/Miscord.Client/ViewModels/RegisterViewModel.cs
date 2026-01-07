using System.Reactive;
using System.Reactive.Linq;
using Miscord.Client.Services;
using ReactiveUI;

namespace Miscord.Client.ViewModels;

public class RegisterViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly Action<AuthResponse> _onRegisterSuccess;
    private readonly Action _onSwitchToLogin;

    private string _username = string.Empty;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _inviteCode = string.Empty;
    private string? _errorMessage;
    private bool _isLoading;

    public RegisterViewModel(IApiClient apiClient, Action<AuthResponse> onRegisterSuccess, Action onSwitchToLogin, string? initialInviteCode = null)
    {
        _apiClient = apiClient;
        _onRegisterSuccess = onRegisterSuccess;
        _onSwitchToLogin = onSwitchToLogin;

        if (!string.IsNullOrEmpty(initialInviteCode))
        {
            _inviteCode = initialInviteCode;
        }

        var canRegister = this.WhenAnyValue(x => x.IsLoading, isLoading => !isLoading);

        RegisterCommand = ReactiveCommand.CreateFromTask(RegisterAsync, canRegister);
        SwitchToLoginCommand = ReactiveCommand.Create(() => _onSwitchToLogin());
    }

    public string Username
    {
        get => _username;
        set => this.RaiseAndSetIfChanged(ref _username, value);
    }

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

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => this.RaiseAndSetIfChanged(ref _confirmPassword, value);
    }

    public string InviteCode
    {
        get => _inviteCode;
        set => this.RaiseAndSetIfChanged(ref _inviteCode, value);
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

    public ReactiveCommand<Unit, Unit> RegisterCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToLoginCommand { get; }

    private async Task RegisterAsync()
    {
        ErrorMessage = null;
        IsLoading = true;

        try
        {
            var result = await _apiClient.RegisterAsync(Username, Email, Password, InviteCode);
            if (result.Success && result.Data is not null)
            {
                _onRegisterSuccess(result.Data);
            }
            else
            {
                ErrorMessage = result.Error ?? "Registration failed";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
