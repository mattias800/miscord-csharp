using System.Reactive;
using Miscord.Client.Services;
using ReactiveUI;

namespace Miscord.Client.ViewModels;

public class AccountSettingsViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly Action _onAccountDeleted;

    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private string? _passwordMessage;
    private bool _passwordMessageIsError;
    private bool _isChangingPassword;

    private string? _deleteAccountError;
    private bool _isDeletingAccount;
    private bool _showDeleteConfirmation;

    public AccountSettingsViewModel(IApiClient apiClient, Action onAccountDeleted)
    {
        _apiClient = apiClient;
        _onAccountDeleted = onAccountDeleted;

        var canChangePassword = this.WhenAnyValue(x => x.IsChangingPassword, isChanging => !isChanging);
        ChangePasswordCommand = ReactiveCommand.CreateFromTask(ChangePasswordAsync, canChangePassword);

        var canDelete = this.WhenAnyValue(x => x.IsDeletingAccount, isDeleting => !isDeleting);
        DeleteAccountCommand = ReactiveCommand.CreateFromTask(DeleteAccountAsync, canDelete);

        ShowDeleteConfirmationCommand = ReactiveCommand.Create(() => { ShowDeleteConfirmation = true; });
        CancelDeleteCommand = ReactiveCommand.Create(() => { ShowDeleteConfirmation = false; });
    }

    public string CurrentPassword
    {
        get => _currentPassword;
        set => this.RaiseAndSetIfChanged(ref _currentPassword, value);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => this.RaiseAndSetIfChanged(ref _newPassword, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => this.RaiseAndSetIfChanged(ref _confirmPassword, value);
    }

    public string? PasswordMessage
    {
        get => _passwordMessage;
        set => this.RaiseAndSetIfChanged(ref _passwordMessage, value);
    }

    public bool PasswordMessageIsError
    {
        get => _passwordMessageIsError;
        set => this.RaiseAndSetIfChanged(ref _passwordMessageIsError, value);
    }

    public bool IsChangingPassword
    {
        get => _isChangingPassword;
        set => this.RaiseAndSetIfChanged(ref _isChangingPassword, value);
    }

    public string? DeleteAccountError
    {
        get => _deleteAccountError;
        set => this.RaiseAndSetIfChanged(ref _deleteAccountError, value);
    }

    public bool IsDeletingAccount
    {
        get => _isDeletingAccount;
        set => this.RaiseAndSetIfChanged(ref _isDeletingAccount, value);
    }

    public bool ShowDeleteConfirmation
    {
        get => _showDeleteConfirmation;
        set => this.RaiseAndSetIfChanged(ref _showDeleteConfirmation, value);
    }

    public ReactiveCommand<Unit, Unit> ChangePasswordCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteAccountCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowDeleteConfirmationCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelDeleteCommand { get; }

    private async Task ChangePasswordAsync()
    {
        PasswordMessage = null;

        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            PasswordMessage = "Current password is required";
            PasswordMessageIsError = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            PasswordMessage = "New password is required";
            PasswordMessageIsError = true;
            return;
        }

        if (NewPassword.Length < 8)
        {
            PasswordMessage = "New password must be at least 8 characters";
            PasswordMessageIsError = true;
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            PasswordMessage = "Passwords do not match";
            PasswordMessageIsError = true;
            return;
        }

        IsChangingPassword = true;

        try
        {
            var result = await _apiClient.ChangePasswordAsync(CurrentPassword, NewPassword);
            if (result.Success)
            {
                PasswordMessage = "Password changed successfully";
                PasswordMessageIsError = false;
                CurrentPassword = string.Empty;
                NewPassword = string.Empty;
                ConfirmPassword = string.Empty;
            }
            else
            {
                PasswordMessage = result.Error ?? "Failed to change password";
                PasswordMessageIsError = true;
            }
        }
        finally
        {
            IsChangingPassword = false;
        }
    }

    private async Task DeleteAccountAsync()
    {
        DeleteAccountError = null;
        IsDeletingAccount = true;

        try
        {
            var result = await _apiClient.DeleteAccountAsync();
            if (result.Success)
            {
                _onAccountDeleted();
            }
            else
            {
                DeleteAccountError = result.Error ?? "Failed to delete account";
            }
        }
        finally
        {
            IsDeletingAccount = false;
        }
    }
}
