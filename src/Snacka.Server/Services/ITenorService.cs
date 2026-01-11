using Snacka.Server.DTOs;

namespace Snacka.Server.Services;

public interface ITenorService
{
    /// <summary>
    /// Search for GIFs matching the query
    /// </summary>
    Task<GifSearchResponse> SearchGifsAsync(string query, int limit = 20, string? pos = null);

    /// <summary>
    /// Get trending GIFs
    /// </summary>
    Task<GifSearchResponse> GetTrendingGifsAsync(int limit = 20, string? pos = null);
}
