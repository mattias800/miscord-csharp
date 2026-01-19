using System.Text.Json.Serialization;

namespace Snacka.Client.Services;

#region Validation DTOs

/// <summary>
/// Result from the native capture tool's validate command.
/// </summary>
public record CaptureValidationResult
{
    [JsonPropertyName("platform")]
    public string Platform { get; init; } = "";

    [JsonPropertyName("gpuVendor")]
    public string? GpuVendor { get; init; }

    [JsonPropertyName("gpuModel")]
    public string? GpuModel { get; init; }

    [JsonPropertyName("driverName")]
    public string? DriverName { get; init; }

    [JsonPropertyName("capabilities")]
    public CodecCapabilities? Capabilities { get; init; }

    [JsonPropertyName("canCapture")]
    public bool CanCapture { get; init; }

    [JsonPropertyName("canEncodeH264")]
    public bool CanEncodeH264 { get; init; }

    [JsonPropertyName("issues")]
    public List<CaptureValidationIssue> Issues { get; init; } = new();

    [JsonPropertyName("info")]
    public ValidationInfo? Info { get; init; }
}

/// <summary>
/// Codec capabilities reported by the native tool.
/// </summary>
public record CodecCapabilities
{
    [JsonPropertyName("h264Encode")]
    public bool H264Encode { get; init; }

    [JsonPropertyName("h264Decode")]
    public bool H264Decode { get; init; }

    [JsonPropertyName("hevcEncode")]
    public bool HevcEncode { get; init; }

    [JsonPropertyName("hevcDecode")]
    public bool HevcDecode { get; init; }
}

/// <summary>
/// A validation issue or informational message from the native tool.
/// </summary>
public record CaptureValidationIssue
{
    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "info";

    [JsonPropertyName("code")]
    public string Code { get; init; } = "";

    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    [JsonPropertyName("suggestions")]
    public List<string> Suggestions { get; init; } = new();

    /// <summary>
    /// Returns true if this is an error-level issue.
    /// </summary>
    public bool IsError => Severity == "error";

    /// <summary>
    /// Returns true if this is a warning-level issue.
    /// </summary>
    public bool IsWarning => Severity == "warning";
}

/// <summary>
/// Additional diagnostic info from validation.
/// </summary>
public record ValidationInfo
{
    [JsonPropertyName("drmDevice")]
    public string? DrmDevice { get; init; }

    [JsonPropertyName("h264Profiles")]
    public List<string> H264Profiles { get; init; } = new();

    [JsonPropertyName("h264Entrypoints")]
    public List<string> H264Entrypoints { get; init; } = new();
}

#endregion

/// <summary>
/// Service for detecting and reporting system capabilities at startup.
/// Checks hardware acceleration support for video encoding/decoding and
/// outputs diagnostics to stdout for testing and debugging.
/// </summary>
public interface ISystemCapabilityService
{
    /// <summary>
    /// Gets whether a hardware video encoder is available.
    /// Hardware encoders: VideoToolbox (macOS), NVENC/AMF/QuickSync (Windows), VAAPI (Linux).
    /// </summary>
    bool IsHardwareEncoderAvailable { get; }

    /// <summary>
    /// Gets the name of the detected hardware encoder, or null if none available.
    /// Examples: "VideoToolbox", "NVENC", "AMF", "QuickSync", "VAAPI"
    /// </summary>
    string? HardwareEncoderName { get; }

    /// <summary>
    /// Gets whether a hardware video decoder is available.
    /// Hardware decoders: VideoToolbox (macOS), Media Foundation (Windows), VAAPI (Linux).
    /// </summary>
    bool IsHardwareDecoderAvailable { get; }

    /// <summary>
    /// Gets the name of the detected hardware decoder, or null if none available.
    /// Examples: "VideoToolbox", "MediaFoundation", "VAAPI"
    /// </summary>
    string? HardwareDecoderName { get; }

    /// <summary>
    /// Gets whether native capture tools are available (for screen share/camera with hardware encoding).
    /// </summary>
    bool IsNativeCaptureAvailable { get; }

    /// <summary>
    /// Gets the native capture tool name, or null if none available.
    /// Examples: "SnackaCaptureVideoToolbox", "SnackaCaptureWindows", "SnackaCaptureLinux"
    /// </summary>
    string? NativeCaptureName { get; }

    /// <summary>
    /// Gets whether full hardware acceleration is available (encoder + decoder + native capture).
    /// </summary>
    bool IsFullHardwareAccelerationAvailable { get; }

    /// <summary>
    /// Gets any warning messages that should be displayed to the user.
    /// Empty if all capabilities are available.
    /// </summary>
    IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Gets the detailed validation result from the native capture tool.
    /// Only available on Linux after ValidateAsync has been called.
    /// </summary>
    CaptureValidationResult? ValidationResult { get; }

    /// <summary>
    /// Gets whether the validation has any issues that should be shown to the user.
    /// </summary>
    bool HasValidationIssues { get; }

    /// <summary>
    /// Gets whether the validation warning banner has been dismissed by the user.
    /// </summary>
    bool IsValidationWarningDismissed { get; }

    /// <summary>
    /// Runs detailed validation using the native capture tool.
    /// Currently only implemented for Linux.
    /// </summary>
    Task ValidateAsync();

    /// <summary>
    /// Dismisses the validation warning banner. The dismissal is persisted.
    /// </summary>
    void DismissValidationWarning();
}
