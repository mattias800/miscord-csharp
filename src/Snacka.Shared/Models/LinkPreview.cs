namespace Snacka.Shared.Models;

/// <summary>
/// Represents OpenGraph metadata for a URL link preview.
/// </summary>
public record LinkPreview(
    string Url,
    string? Title,
    string? Description,
    string? ImageUrl,
    string? SiteName,
    string? Type,
    string? FaviconUrl,
    string? PreviewUrl = null,
    string? ArtistName = null
);
