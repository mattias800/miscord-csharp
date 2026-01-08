namespace Miscord.Server.DTOs;

/// <summary>
/// Response from GIF search/trending endpoints
/// </summary>
public record GifSearchResponse(
    List<GifResult> Results,
    string? NextPos  // For pagination (Tenor's "pos" parameter)
);

/// <summary>
/// A single GIF result
/// </summary>
public record GifResult(
    string Id,
    string Title,
    string PreviewUrl,    // Small preview for picker grid (tinygif)
    string Url,           // Full size GIF URL
    int Width,
    int Height
);
