using Asteria.Client.Rendering;

internal static class NativeImageChecks
{
    public static void Run()
    {
        CheckOpaquePixels();
        CheckTransparentAndTranslucentPixels();
        CheckRegionScanning();
        CheckNonRgbaFormats();
        CheckBoundsAndEmptyRegions();
        CheckConstructionAndLifetime();
    }

    private static void CheckOpaquePixels()
    {
        using var image = new NativeImage(
            NativeImage.Format.RGBA,
            2,
            2,
            Pixels(255, 255, 255, 255));

        Require(
            image.ComputeTransparency() == Transparency.NONE,
            "An RGBA image with fully opaque pixels must have no transparency.");
    }

    private static void CheckTransparentAndTranslucentPixels()
    {
        using var transparent = new NativeImage(
            NativeImage.Format.RGBA,
            1,
            1,
            Pixels(0));

        using var translucent = new NativeImage(
            NativeImage.Format.RGBA,
            1,
            1,
            Pixels(127));

        using var both = new NativeImage(
            NativeImage.Format.RGBA,
            2,
            1,
            Pixels(0, 128));

        Require(
            transparent.ComputeTransparency() == Transparency.TRANSPARENT,
            "Alpha zero must set hasTransparent.");
        Require(
            translucent.ComputeTransparency() == Transparency.TRANSLUCENT,
            "Alpha values from one through 254 must set hasTranslucent.");
        Require(
            both.ComputeTransparency() == Transparency.TRANSPARENT_AND_TRANSLUCENT,
            "An image containing both zero and partial alpha must set both transparency flags.");
    }

    private static void CheckRegionScanning()
    {
        // Row-major RGBA image. Only the top-right pixel is transparent and only the
        // bottom-left pixel is translucent.
        byte[] pixels =
        [
            10, 20, 30, 255,
            10, 20, 30, 0,
            10, 20, 30, 128,
            10, 20, 30, 255
        ];

        using var image = new NativeImage(NativeImage.Format.RGBA, 2, 2, pixels);

        Require(
            image.ComputeTransparency(0, 0, 1, 1) == Transparency.NONE,
            "A region containing only opaque pixels must remain opaque.");
        Require(
            image.ComputeTransparency(1, 0, 2, 1) == Transparency.TRANSPARENT,
            "Region transparency must inspect only pixels inside the requested rectangle.");
        Require(
            image.ComputeTransparency(0, 1, 1, 2) == Transparency.TRANSLUCENT,
            "Region transparency must recognize partial alpha in the requested rectangle.");
        Require(
            image.ComputeTransparency(1, 1, 2, 2) == Transparency.NONE,
            "Pixels outside a face UV region must not affect its render layer.");
    }

    private static void CheckNonRgbaFormats()
    {
        using var rgb = new NativeImage(
            NativeImage.Format.RGB,
            1,
            1,
            new byte[] { 255, 0, 0 });
        using var luminanceAlpha = new NativeImage(
            NativeImage.Format.LUMINANCE_ALPHA,
            1,
            1,
            new byte[] { 255, 0 });
        using var luminance = new NativeImage(
            NativeImage.Format.LUMINANCE,
            1,
            1,
            new byte[] { 0 });

        Require(
            rgb.ComputeTransparency() == Transparency.NONE &&
            luminanceAlpha.ComputeTransparency() == Transparency.NONE &&
            luminance.ComputeTransparency() == Transparency.NONE,
            "NativeImage.computeTransparency must return NONE for every non-RGBA format.");
    }

    private static void CheckBoundsAndEmptyRegions()
    {
        using var image = new NativeImage(
            NativeImage.Format.RGBA,
            2,
            2,
            Pixels(255, 0, 128, 255));

        Require(
            image.ComputeTransparency(1, 1, 1, 2) == Transparency.NONE,
            "An empty in-bounds region contains no transparent pixels.");

        Expect<ArgumentException>(
            () => image.ComputeTransparency(-1, 0, 1, 1),
            "Negative UV pixel bounds must throw.");
        Expect<ArgumentException>(
            () => image.ComputeTransparency(0, 0, 3, 1),
            "UV pixel bounds extending past image width must throw.");
        Expect<ArgumentException>(
            () => image.ComputeTransparency(0, 0, 1, 3),
            "UV pixel bounds extending past image height must throw.");
    }

    private static void CheckConstructionAndLifetime()
    {
        Expect<ArgumentException>(
            () => new NativeImage(NativeImage.Format.RGBA, 0, 1, Array.Empty<byte>()),
            "NativeImage dimensions must be positive.");
        Expect<ArgumentException>(
            () => new NativeImage(NativeImage.Format.RGBA, 1, 1, new byte[3]),
            "NativeImage must reject a buffer whose length does not match format and dimensions.");

        var disposed = new NativeImage(
            NativeImage.Format.RGBA,
            1,
            1,
            Pixels(255));
        disposed.Dispose();

        Expect<ObjectDisposedException>(
            () => disposed.ComputeTransparency(),
            "Disposed NativeImage pixel storage must not be accessed.");
        Expect<ObjectDisposedException>(
            () => _ = disposed.PixelBytes,
            "Disposed NativeImage must not expose pixel bytes.");
    }

    private static byte[] Pixels(params byte[] alphaValues)
    {
        byte[] result = new byte[alphaValues.Length * 4];
        for (int i = 0; i < alphaValues.Length; i++)
        {
            result[i * 4] = 64;
            result[i * 4 + 1] = 128;
            result[i * 4 + 2] = 192;
            result[i * 4 + 3] = alphaValues[i];
        }

        return result;
    }

    private static void Expect<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
