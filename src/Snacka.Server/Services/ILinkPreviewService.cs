using Snacka.Shared.Models;

namespace Snacka.Server.Services;

public interface ILinkPreviewService
{
    /// <summary>
    /// Fetches OpenGraph metadata for a URL.
    /// </summary>
    /// <param name="url">The URL to fetch metadata for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>LinkPreview with metadata, or null if fetch failed.</returns>
    Task<LinkPreview?> GetLinkPreviewAsync(string url, CancellationToken cancellationToken = default);
}
