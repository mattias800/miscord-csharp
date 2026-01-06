namespace Miscord.Client.Services;

public interface IScreenCaptureService
{
    /// <summary>
    /// Gets all available screen capture sources (displays and windows).
    /// </summary>
    IReadOnlyList<ScreenCaptureSource> GetAvailableSources();

    /// <summary>
    /// Gets only display sources.
    /// </summary>
    IReadOnlyList<ScreenCaptureSource> GetDisplays();

    /// <summary>
    /// Gets only window sources.
    /// </summary>
    IReadOnlyList<ScreenCaptureSource> GetWindows();
}
