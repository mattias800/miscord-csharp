namespace Miscord.Client.Services;

/// <summary>
/// Static utility class for video color space conversions.
/// Supports RGB, BGR, and I420 (YUV420p) formats commonly used in video encoding/decoding.
/// </summary>
public static class ColorSpaceConverter
{
    /// <summary>
    /// Converts RGB24 to I420 (YUV420p) format for video encoding.
    /// </summary>
    public static byte[] RgbToI420(byte[] rgb, int width, int height)
    {
        // I420 format: Y plane (width*height), U plane (width/2 * height/2), V plane (width/2 * height/2)
        var ySize = width * height;
        var uvSize = (width / 2) * (height / 2);
        var i420 = new byte[ySize + uvSize * 2];

        var yPlane = i420.AsSpan(0, ySize);
        var uPlane = i420.AsSpan(ySize, uvSize);
        var vPlane = i420.AsSpan(ySize + uvSize, uvSize);

        for (int j = 0; j < height; j++)
        {
            for (int i = 0; i < width; i++)
            {
                var rgbIndex = (j * width + i) * 3;
                var r = rgb[rgbIndex];
                var g = rgb[rgbIndex + 1];
                var b = rgb[rgbIndex + 2];

                // RGB to Y
                var y = (byte)Math.Clamp((66 * r + 129 * g + 25 * b + 128) / 256 + 16, 0, 255);
                yPlane[j * width + i] = y;

                // Subsample U and V (every 2x2 block)
                if (j % 2 == 0 && i % 2 == 0)
                {
                    var uvIndex = (j / 2) * (width / 2) + (i / 2);
                    uPlane[uvIndex] = (byte)Math.Clamp((-38 * r - 74 * g + 112 * b + 128) / 256 + 128, 0, 255);
                    vPlane[uvIndex] = (byte)Math.Clamp((112 * r - 94 * g - 18 * b + 128) / 256 + 128, 0, 255);
                }
            }
        }

        return i420;
    }

    /// <summary>
    /// Converts BGR24 to I420 (YUV420p) format for video encoding.
    /// BGR is commonly used by OpenCV and some capture APIs.
    /// </summary>
    public static byte[] BgrToI420(byte[] bgr, int width, int height)
    {
        // I420 format: Y plane (width*height), U plane (width/2 * height/2), V plane (width/2 * height/2)
        var ySize = width * height;
        var uvSize = (width / 2) * (height / 2);
        var i420 = new byte[ySize + uvSize * 2];

        var yPlane = i420.AsSpan(0, ySize);
        var uPlane = i420.AsSpan(ySize, uvSize);
        var vPlane = i420.AsSpan(ySize + uvSize, uvSize);

        for (int j = 0; j < height; j++)
        {
            for (int i = 0; i < width; i++)
            {
                var bgrIndex = (j * width + i) * 3;
                var b = bgr[bgrIndex];
                var g = bgr[bgrIndex + 1];
                var r = bgr[bgrIndex + 2];

                // RGB to Y
                var y = (byte)Math.Clamp((66 * r + 129 * g + 25 * b + 128) / 256 + 16, 0, 255);
                yPlane[j * width + i] = y;

                // Subsample U and V (every 2x2 block)
                if (j % 2 == 0 && i % 2 == 0)
                {
                    var uvIndex = (j / 2) * (width / 2) + (i / 2);
                    uPlane[uvIndex] = (byte)Math.Clamp((-38 * r - 74 * g + 112 * b + 128) / 256 + 128, 0, 255);
                    vPlane[uvIndex] = (byte)Math.Clamp((112 * r - 94 * g - 18 * b + 128) / 256 + 128, 0, 255);
                }
            }
        }

        return i420;
    }

    /// <summary>
    /// Converts I420 (YUV420p) to RGB24 format for display.
    /// </summary>
    public static byte[] I420ToRgb(byte[] i420, int width, int height)
    {
        // I420 format: Y plane (width*height), U plane (width/2 * height/2), V plane (width/2 * height/2)
        var ySize = width * height;
        var uvSize = (width / 2) * (height / 2);
        var rgb = new byte[width * height * 3];

        var yPlane = i420.AsSpan(0, ySize);
        var uPlane = i420.AsSpan(ySize, uvSize);
        var vPlane = i420.AsSpan(ySize + uvSize, uvSize);

        for (int j = 0; j < height; j++)
        {
            for (int i = 0; i < width; i++)
            {
                var yIndex = j * width + i;
                var uvIndex = (j / 2) * (width / 2) + (i / 2);

                var y = yPlane[yIndex] - 16;
                var u = uPlane[uvIndex] - 128;
                var v = vPlane[uvIndex] - 128;

                // YUV to RGB conversion
                var r = (298 * y + 409 * v + 128) >> 8;
                var g = (298 * y - 100 * u - 208 * v + 128) >> 8;
                var b = (298 * y + 516 * u + 128) >> 8;

                var rgbIndex = (j * width + i) * 3;
                rgb[rgbIndex] = (byte)Math.Clamp(r, 0, 255);
                rgb[rgbIndex + 1] = (byte)Math.Clamp(g, 0, 255);
                rgb[rgbIndex + 2] = (byte)Math.Clamp(b, 0, 255);
            }
        }

        return rgb;
    }

    /// <summary>
    /// Converts BGR24 to RGB24 format by swapping red and blue channels.
    /// </summary>
    public static byte[] BgrToRgb(byte[] bgr, int width, int height)
    {
        var rgb = new byte[bgr.Length];
        for (int i = 0; i < bgr.Length; i += 3)
        {
            rgb[i] = bgr[i + 2];     // R = B
            rgb[i + 1] = bgr[i + 1]; // G = G
            rgb[i + 2] = bgr[i];     // B = R
        }
        return rgb;
    }
}
