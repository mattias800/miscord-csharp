namespace Snacka.Server.Services;

public sealed class TurnSettings
{
    public const string SectionName = "Turn";

    /// <summary>
    /// Whether TURN server is enabled.
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// TURN server hostname or IP address.
    /// </summary>
    public string Host { get; init; } = "";

    /// <summary>
    /// Shared secret for generating time-limited credentials.
    /// Must match the static-auth-secret in turnserver.conf.
    /// </summary>
    public string Secret { get; init; } = "";

    /// <summary>
    /// TURN server port (UDP/TCP).
    /// </summary>
    public int Port { get; init; } = 3478;

    /// <summary>
    /// TURN server TLS port.
    /// </summary>
    public int TlsPort { get; init; } = 5349;

    /// <summary>
    /// Credential TTL in seconds. Default is 24 hours.
    /// </summary>
    public int CredentialTtlSeconds { get; init; } = 86400;
}
