using System.Text.Json;
using Microsoft.Extensions.Options;
using Miscord.Server.DTOs;

namespace Miscord.Server.Services;

public class TenorService : ITenorService
{
    private readonly HttpClient _httpClient;
    private readonly TenorSettings _settings;
    private readonly ILogger<TenorService> _logger;

    // Cache for search results
    private readonly Dictionary<string, (GifSearchResponse Response, DateTime FetchedAt)> _cache = new();
    private readonly object _cacheLock = new();
    private TimeSpan CacheDuration => TimeSpan.FromMinutes(_settings.CacheDurationMinutes);

    private const string TenorApiBaseUrl = "https://tenor.googleapis.com/v2";

    public TenorService(HttpClient httpClient, IOptions<TenorSettings> settings, ILogger<TenorService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<GifSearchResponse> SearchGifsAsync(string query, int limit = 20, string? pos = null)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogWarning("Tenor API key not configured");
            return new GifSearchResponse(new List<GifResult>(), null);
        }

        var cacheKey = $"search:{query}:{limit}:{pos ?? ""}";

        // Check cache
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow - cached.FetchedAt < CacheDuration)
            {
                return cached.Response;
            }
        }

        try
        {
            var url = $"{TenorApiBaseUrl}/search?key={_settings.ApiKey}&client_key={_settings.ClientKey}&q={Uri.EscapeDataString(query)}&limit={limit}&media_filter=gif,tinygif";
            if (!string.IsNullOrEmpty(pos))
            {
                url += $"&pos={Uri.EscapeDataString(pos)}";
            }

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var tenorResponse = JsonSerializer.Deserialize<TenorApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var result = MapToGifSearchResponse(tenorResponse);

            // Cache the result
            lock (_cacheLock)
            {
                _cache[cacheKey] = (result, DateTime.UtcNow);
                CleanCacheIfNeeded();
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search Tenor GIFs for query: {Query}", query);
            return new GifSearchResponse(new List<GifResult>(), null);
        }
    }

    public async Task<GifSearchResponse> GetTrendingGifsAsync(int limit = 20, string? pos = null)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogWarning("Tenor API key not configured");
            return new GifSearchResponse(new List<GifResult>(), null);
        }

        var cacheKey = $"trending:{limit}:{pos ?? ""}";

        // Check cache
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow - cached.FetchedAt < CacheDuration)
            {
                return cached.Response;
            }
        }

        try
        {
            var url = $"{TenorApiBaseUrl}/featured?key={_settings.ApiKey}&client_key={_settings.ClientKey}&limit={limit}&media_filter=gif,tinygif";
            if (!string.IsNullOrEmpty(pos))
            {
                url += $"&pos={Uri.EscapeDataString(pos)}";
            }

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var tenorResponse = JsonSerializer.Deserialize<TenorApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var result = MapToGifSearchResponse(tenorResponse);

            // Cache the result
            lock (_cacheLock)
            {
                _cache[cacheKey] = (result, DateTime.UtcNow);
                CleanCacheIfNeeded();
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch trending Tenor GIFs");
            return new GifSearchResponse(new List<GifResult>(), null);
        }
    }

    private static GifSearchResponse MapToGifSearchResponse(TenorApiResponse? tenorResponse)
    {
        if (tenorResponse?.Results == null)
        {
            return new GifSearchResponse(new List<GifResult>(), null);
        }

        var results = tenorResponse.Results.Select(r =>
        {
            // Get the preview (tinygif) and full GIF URLs
            var previewUrl = r.Media_Formats?.TryGetValue("tinygif", out var tinyGif) == true
                ? tinyGif.Url
                : r.Media_Formats?.TryGetValue("gif", out var gif) == true
                    ? gif.Url
                    : "";

            var fullUrl = r.Media_Formats?.TryGetValue("gif", out var fullGif) == true
                ? fullGif.Url
                : previewUrl;

            var dims = r.Media_Formats?.TryGetValue("gif", out var gifMedia) == true
                ? gifMedia.Dims
                : null;

            return new GifResult(
                Id: r.Id ?? "",
                Title: r.Title ?? r.Content_Description ?? "",
                PreviewUrl: previewUrl ?? "",
                Url: fullUrl ?? "",
                Width: dims?.Count > 0 ? dims[0] : 0,
                Height: dims?.Count > 1 ? dims[1] : 0
            );
        }).Where(r => !string.IsNullOrEmpty(r.Url)).ToList();

        return new GifSearchResponse(results, tenorResponse.Next);
    }

    private void CleanCacheIfNeeded()
    {
        if (_cache.Count > 500)
        {
            var oldEntries = _cache
                .Where(kv => DateTime.UtcNow - kv.Value.FetchedAt > CacheDuration)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in oldEntries)
            {
                _cache.Remove(key);
            }
        }
    }

    // Tenor API response models
    private record TenorApiResponse(
        List<TenorResult>? Results,
        string? Next
    );

    private record TenorResult(
        string? Id,
        string? Title,
        string? Content_Description,
        Dictionary<string, TenorMediaFormat>? Media_Formats
    );

    private record TenorMediaFormat(
        string? Url,
        List<int>? Dims,
        int? Duration,
        int? Size
    );
}
