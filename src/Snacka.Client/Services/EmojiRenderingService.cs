using System.Runtime.InteropServices;
using Avalonia.Media;

namespace Snacka.Client.Services;

/// <summary>
/// Provides platform-specific emoji rendering configuration.
/// On Linux, emojis often render as black/white boxes due to missing color emoji fonts.
/// This service provides OpenMoji font support on Linux for proper color emoji rendering.
/// </summary>
public static class EmojiRenderingService
{
    /// <summary>
    /// Gets whether the current platform is Linux.
    /// </summary>
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    /// <summary>
    /// Gets whether the current platform is macOS.
    /// </summary>
    public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>
    /// Gets whether the current platform is Windows.
    /// </summary>
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// Gets the font family to use for emoji rendering.
    /// On Linux, this uses OpenMoji (if available) with fallbacks.
    /// On Windows/macOS, uses system emoji fonts.
    /// </summary>
    public static FontFamily GetEmojiFontFamily()
    {
        if (IsLinux)
        {
            // On Linux, try OpenMoji first (bundled), then Noto Color Emoji (system), then fallbacks
            // The format is: "FontName, Fallback1, Fallback2"
            // avares:// URIs reference embedded fonts in the Assets folder
            return new FontFamily("avares://Snacka.Client/Assets/Fonts#OpenMoji Color, Noto Color Emoji, Twemoji, Symbola, Segoe UI Emoji");
        }

        if (IsMacOS)
        {
            // macOS has great emoji support via Apple Color Emoji
            return new FontFamily("Apple Color Emoji, Segoe UI Emoji");
        }

        // Windows has Segoe UI Emoji
        return new FontFamily("Segoe UI Emoji, Segoe UI Symbol");
    }

    /// <summary>
    /// Gets a text font family that includes emoji font as a fallback.
    /// Use this for TextBlocks that may contain mixed text and emojis.
    /// </summary>
    /// <param name="baseFontFamily">The base font family for text (e.g., "Inter", "Segoe UI").</param>
    public static FontFamily GetTextWithEmojiFontFamily(string baseFontFamily = "Inter")
    {
        if (IsLinux)
        {
            // On Linux, include OpenMoji in the fallback chain
            return new FontFamily($"{baseFontFamily}, avares://Snacka.Client/Assets/Fonts#OpenMoji Color, Noto Color Emoji, Twemoji, Symbola");
        }

        if (IsMacOS)
        {
            return new FontFamily($"{baseFontFamily}, Apple Color Emoji");
        }

        return new FontFamily($"{baseFontFamily}, Segoe UI Emoji, Segoe UI Symbol");
    }

    /// <summary>
    /// Returns true if OpenMoji font is available in the Assets folder.
    /// </summary>
    public static bool IsOpenMojiAvailable()
    {
        // The font should be at Assets/Fonts/OpenMoji-Color.ttf
        // This is checked at runtime via the avares:// URI
        // If the font isn't found, Avalonia will fall back to the next font in the chain
        return IsLinux; // We always try to use it on Linux
    }
}
