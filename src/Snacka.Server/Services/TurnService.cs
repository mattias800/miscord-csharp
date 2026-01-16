using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Snacka.Server.Services;

/// <summary>
/// Represents an ICE server configuration for WebRTC.
/// </summary>
public record IceServer(
    string[] Urls,
    string? Username = null,
    string? Credential = null
);

/// <summary>
/// Response containing ICE server configurations.
/// </summary>
public record IceServersResponse(
    List<IceServer> IceServers,
    int TtlSeconds
);

/// <summary>
/// Service for generating TURN server credentials and ICE server configurations.
/// Uses time-limited credentials with HMAC-SHA1 (RFC 5766 / coturn static-auth-secret).
/// </summary>
public interface ITurnService
{
    /// <summary>
    /// Gets ICE server configurations including STUN and optionally TURN servers.
    /// </summary>
    /// <param name="userId">User ID to include in credential generation.</param>
    IceServersResponse GetIceServers(Guid userId);

    /// <summary>
    /// Whether TURN server is configured and enabled.
    /// </summary>
    bool IsTurnEnabled { get; }
}

public class TurnService : ITurnService
{
    private readonly TurnSettings _settings;
    private readonly ILogger<TurnService> _logger;

    // Public STUN servers (always included)
    private static readonly string[] StunServers =
    [
        "stun:stun.l.google.com:19302",
        "stun:stun1.l.google.com:19302"
    ];

    public TurnService(IOptions<TurnSettings> settings, ILogger<TurnService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        if (_settings.Enabled)
        {
            if (string.IsNullOrEmpty(_settings.Host))
            {
                _logger.LogWarning("TURN is enabled but Host is not configured");
            }
            else if (string.IsNullOrEmpty(_settings.Secret))
            {
                _logger.LogWarning("TURN is enabled but Secret is not configured");
            }
            else
            {
                _logger.LogInformation("TURN server configured: {Host}:{Port}", _settings.Host, _settings.Port);
            }
        }
    }

    public bool IsTurnEnabled => _settings.Enabled &&
                                  !string.IsNullOrEmpty(_settings.Host) &&
                                  !string.IsNullOrEmpty(_settings.Secret);

    public IceServersResponse GetIceServers(Guid userId)
    {
        var iceServers = new List<IceServer>
        {
            // Always include public STUN servers
            new IceServer(StunServers)
        };

        if (IsTurnEnabled)
        {
            // Generate time-limited credentials
            var (username, credential) = GenerateCredentials(userId);

            // Add TURN servers with various transports
            var turnUrls = new List<string>
            {
                // UDP (fastest, may be blocked)
                $"turn:{_settings.Host}:{_settings.Port}",
                // TCP (works through most firewalls)
                $"turn:{_settings.Host}:{_settings.Port}?transport=tcp"
            };

            // Add TLS if configured
            if (_settings.TlsPort > 0)
            {
                turnUrls.Add($"turns:{_settings.Host}:{_settings.TlsPort}?transport=tcp");
            }

            iceServers.Add(new IceServer(
                turnUrls.ToArray(),
                username,
                credential
            ));
        }

        return new IceServersResponse(iceServers, _settings.CredentialTtlSeconds);
    }

    /// <summary>
    /// Generates time-limited TURN credentials using the coturn static-auth-secret mechanism.
    /// Username format: timestamp:userId
    /// Credential: Base64(HMAC-SHA1(secret, username))
    /// </summary>
    private (string username, string credential) GenerateCredentials(Guid userId)
    {
        // Timestamp when credentials expire (Unix timestamp)
        var expiry = DateTimeOffset.UtcNow.AddSeconds(_settings.CredentialTtlSeconds).ToUnixTimeSeconds();

        // Username format: expiry_timestamp:user_identifier
        var username = $"{expiry}:{userId}";

        // Generate HMAC-SHA1 of username using shared secret
        var secretBytes = Encoding.UTF8.GetBytes(_settings.Secret);
        var usernameBytes = Encoding.UTF8.GetBytes(username);

        using var hmac = new HMACSHA1(secretBytes);
        var hash = hmac.ComputeHash(usernameBytes);
        var credential = Convert.ToBase64String(hash);

        return (username, credential);
    }
}
