namespace Snacka.Server.Services;

public class TenorSettings
{
    public const string SectionName = "Tenor";

    /// <summary>
    /// Tenor API key (get from https://developers.google.com/tenor)
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Client key for attribution (identifies your app)
    /// </summary>
    public string ClientKey { get; set; } = "snacka";

    /// <summary>
    /// How long to cache GIF search results
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 15;
}
