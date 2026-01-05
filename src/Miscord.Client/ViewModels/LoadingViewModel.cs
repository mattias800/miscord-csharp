namespace Miscord.Client.ViewModels;

public class LoadingViewModel : ViewModelBase
{
    public LoadingViewModel(string message = "Loading...")
    {
        Message = message;
    }

    public string Message { get; }
}
