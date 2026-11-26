namespace Asteria.Client.Rendering;

/// <summary>
/// Managed pixel storage and transparency inspection ported from Minecraft Java 26.3
/// NativeImage. Image decoding and GPU upload remain responsibilities of the platform backend.
/// </summary>
public sealed class NativeImage : IDisposable
{
    private byte[]? _pixels;

    public int Width { get; }
    public int Height { get; }
    public Format ImageFormat { get; }

    public ReadOnlyMemory<byte> PixelBytes =>
        _pixels ?? throw new ObjectDisposedException(nameof(NativeImage));

    public NativeImage(Format format, int width, int height, ReadOnlySpan<byte> pixels)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException($"Invalid texture size: {width}x{height}");

        long expectedSize = (long)width * height * format.Components();
        if (expectedSize > int.MaxValue)
            throw new ArgumentException($"Image of size {width}x{height} is too large.");

        if (pixels.Length != expectedSize)
            throw new ArgumentException(
                $"Invalid pixel buffer size: expected {expectedSize} bytes, got {pixels.Length}.",
                nameof(pixels));

        Width = width;
        Height = height;
        ImageFormat = format;
        _pixels = pixels.ToArray();
    }

    public Transparency ComputeTransparency() =>
        ComputeTransparency(0, 0, Width, Height);

    /// <summary>
    /// Computes alpha transparency over the supplied pixel rectangle, preserving NativeImage's
    /// RGBA-only behavior and exact out-of-bounds guard from Minecraft 26.3.
    /// </summary>
    public Transparency ComputeTransparency(int x0, int y0, int x1, int y1)
    {
        byte[] pixels = _pixels ??
            throw new ObjectDisposedException(nameof(NativeImage));

        if (ImageFormat != Format.RGBA)
            return Transparency.NONE;

        if (x0 >= 0 && y0 >= 0 && x1 <= Width && y1 <= Height)
        {
            if ((long)Width * Height * 4L > int.MaxValue)
            {
                throw new ArgumentException(
                    $"Image of size {Width}x{Height} is too large to compute translucency.");
            }

            bool hasTransparentPixel = false;
            bool hasTranslucentPixel = false;

            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    int alpha = pixels[(x + y * Width) * 4 + 3];
                    if (alpha == 0)
                    {
                        hasTransparentPixel = true;
                    }
                    else if (alpha != 255)
                    {
                        hasTranslucentPixel = true;
                    }
                }
            }

            return Transparency.Of(hasTransparentPixel, hasTranslucentPixel);
        }

        throw new ArgumentException(
            $"Cannot compute translucency out of bounds: [{x0}, {y0}, {x1}, {y1}] in {Width}x{Height} image");
    }

    public void Dispose()
    {
        _pixels = null;
    }

    public enum Format
    {
        RGBA,
        RGB,
        LUMINANCE_ALPHA,
        LUMINANCE
    }
}

public static class NativeImageFormatExtensions
{
    public static int Components(this NativeImage.Format format) =>
        format switch
        {
            NativeImage.Format.RGBA => 4,
            NativeImage.Format.RGB => 3,
            NativeImage.Format.LUMINANCE_ALPHA => 2,
            NativeImage.Format.LUMINANCE => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
}
