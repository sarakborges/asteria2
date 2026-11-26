using Asteria.Client.Rendering;
using Asteria.Content.Identifiers;

internal static class SpriteContentsChecks
{
    public static void Run()
    {
        CheckFrameLayout();
        CheckStaticTransparency();
        CheckAnimationTransparencyAcrossFrames();
        CheckRepeatedAnimationFrames();
        CheckFramePixelAccess();
        CheckInvalidContent();
        CheckDispose();
    }

    private static void CheckFrameLayout()
    {
        using var image = new NativeImage(
            NativeImage.Format.RGBA,
            4,
            4,
            Pixels(Enumerable.Repeat((byte)255, 16).ToArray()));
        using var sprite = new SpriteContents(
            ResourceLocation.Parse("minecraft:block/test"),
            new FrameSize(2, 2),
            image);

        Require(sprite.Width == 2 && sprite.Height == 2, "Sprite dimensions must describe one frame.");
        Require(sprite.IsAnimated, "A sprite sheet with multiple implicit frames must be animated.");
        Require(
            sprite.Frames.Select(frame => frame.Index).SequenceEqual(new[] { 0, 1, 2, 3 }),
            "An implicit animation sequence must enumerate sprite sheet frames in row-major order.");
        Require(
            sprite.GetFrameX(0) == 0 && sprite.GetFrameY(0) == 0 &&
            sprite.GetFrameX(1) == 2 && sprite.GetFrameY(1) == 0 &&
            sprite.GetFrameX(2) == 0 && sprite.GetFrameY(2) == 2 &&
            sprite.GetFrameX(3) == 2 && sprite.GetFrameY(3) == 2,
            "Frame coordinates must follow the reference row-major frame layout.");
        Require(
            sprite.UniqueFrames.SequenceEqual(new[] { 0, 1, 2, 3 }),
            "Unique frame indices must retain first-occurrence animation order.");
    }

    private static void CheckStaticTransparency()
    {
        using var image = new NativeImage(
            NativeImage.Format.RGBA,
            2,
            2,
            Pixels(255, 255, 255, 0));
        using var sprite = new SpriteContents(
            ResourceLocation.Parse("minecraft:block/opaque_test"),
            new FrameSize(2, 2),
            image);

        Require(
            sprite.SpriteTransparency == Transparency.TRANSPARENT,
            "Sprite-wide transparency must be computed from its original image.");
        Require(
            sprite.ComputeTransparency(0.0f, 0.0f, 0.5f, 1.0f) == Transparency.NONE,
            "An opaque face UV region must classify as solid even when another region in the sprite is transparent.");
        Require(
            sprite.ComputeTransparency(0.5f, 0.0f, 1.0f, 1.0f) == Transparency.TRANSPARENT,
            "A face UV region containing an alpha-zero texel must classify as transparent.");
        Require(
            sprite.ComputeTransparency(0.0f, 0.0f, 1.0f, 1.0f) == Transparency.TRANSPARENT,
            "The full sprite UV region must return cached sprite transparency.");
    }

    private static void CheckAnimationTransparencyAcrossFrames()
    {
        // Two 2x2 frames placed side by side. First frame is opaque; second contains partial alpha.
        using var image = new NativeImage(
            NativeImage.Format.RGBA,
            4,
            2,
            Pixels(255, 255, 255, 255, 128, 255, 255, 255));
        using var sprite = new SpriteContents(
            ResourceLocation.Parse("minecraft:block/animated_test"),
            new FrameSize(2, 2),
            image,
            new[]
            {
                new SpriteContents.FrameInfo(0, 2),
                new SpriteContents.FrameInfo(1, 3)
            },
            defaultFrameTime: 1);

        Require(sprite.IsAnimated, "Multiple animation frames must mark sprite contents as animated.");
        Require(
            sprite.ComputeTransparency(0.0f, 0.0f, 1.0f, 1.0f) == Transparency.TRANSLUCENT,
            "Face transparency must combine the same UV region across all unique animation frames.");
        Require(
            sprite.ComputeTransparency(0.0f, 0.0f, 0.5f, 1.0f) == Transparency.NONE,
            "An opaque UV region in every animation frame must remain opaque.");
        Require(
            sprite.ComputeTransparency(0.0f, 0.0f, 0.25f, 1.0f) == Transparency.NONE,
            "UV bounds must use floor/ceiling pixel conversion within every frame.");
    }

    private static void CheckRepeatedAnimationFrames()
    {
        using var image = new NativeImage(
            NativeImage.Format.RGBA,
            4,
            2,
            Pixels(255, 0, 255, 255));
        using var sprite = new SpriteContents(
            ResourceLocation.Parse("minecraft:block/repeated_frames"),
            new FrameSize(2, 2),
            image,
            new[]
            {
                new SpriteContents.FrameInfo(1, 2),
                new SpriteContents.FrameInfo(0, 4),
                new SpriteContents.FrameInfo(1, 1)
            },
            defaultFrameTime: 7);

        Require(
            sprite.UniqueFrames.SequenceEqual(new[] { 1, 0 }),
            "Transparency analysis must visit unique frames once, preserving their first-seen order.");
        Require(
            sprite.Frames.Select(frame => frame.Time).SequenceEqual(new[] { 2, 4, 1 }),
            "Animation frame durations must be retained independently for repeated frame indices.");
        Require(
            sprite.ComputeTransparency(0.0f, 0.0f, 1.0f, 1.0f) == Transparency.TRANSPARENT,
            "Repeated animation frames must not hide transparency from a later unique frame.");
    }

    private static void CheckFramePixelAccess()
    {
        using var image = new NativeImage(
            NativeImage.Format.RGBA,
            4,
            2,
            Pixels(255, 0, 255, 128));
        using var sprite = new SpriteContents(
            ResourceLocation.Parse("minecraft:block/frame_pixel"),
            new FrameSize(2, 2),
            image,
            new[]
            {
                new SpriteContents.FrameInfo(1, 1),
                new SpriteContents.FrameInfo(0, 1)
            },
            defaultFrameTime: 1);

        Require(sprite.IsTransparent(0, 1, 0), "IsTransparent must use the frame index selected by the animation sequence.");
        Require(!sprite.IsTransparent(0, 0, 0), "IsTransparent must inspect the requested local frame pixel.");
        Require(sprite.IsTransparent(1, 1, 0), "IsTransparent must read alpha-zero pixels from subsequent frames.");
        Require(!sprite.IsTransparent(1, 0, 0), "Opaque pixels must not be reported as transparent.");
    }

    private static void CheckInvalidContent()
    {
        using var image = new NativeImage(
            NativeImage.Format.RGBA,
            4,
            2,
            Pixels(255, 255, 255, 255));

        Expect<ArgumentOutOfRangeException>(
            () => _ = new FrameSize(0, 2),
            "FrameSize width must be positive.");
        Expect<ArgumentOutOfRangeException>(
            () => _ = new FrameSize(2, 0),
            "FrameSize height must be positive.");
        Expect<ArgumentException>(
            () => _ = new SpriteContents(ResourceLocation.Parse("minecraft:block/invalid"), new FrameSize(8, 8), image),
            "Frame dimensions larger than the source image must be rejected.");
        Expect<ArgumentException>(
            () => _ = new SpriteContents(
                ResourceLocation.Parse("minecraft:block/invalid_empty_frames"),
                new FrameSize(2, 2),
                image,
                Array.Empty<SpriteContents.FrameInfo>(),
                defaultFrameTime: 1),
            "An explicitly empty frame sequence must be rejected.");
        Expect<ArgumentOutOfRangeException>(
            () => _ = new SpriteContents(
                ResourceLocation.Parse("minecraft:block/invalid_index"),
                new FrameSize(2, 2),
                image,
                new[] { new SpriteContents.FrameInfo(2, 1) },
                defaultFrameTime: 1),
            "Animation frame indices outside the sprite sheet must be rejected.");
        Expect<ArgumentOutOfRangeException>(
            () => _ = new SpriteContents(
                ResourceLocation.Parse("minecraft:block/invalid_duration"),
                new FrameSize(2, 2),
                image,
                new[] { new SpriteContents.FrameInfo(0, 0) },
                defaultFrameTime: 1),
            "Animation frame durations must be positive.");
    }

    private static void CheckDispose()
    {
        var image = new NativeImage(
            NativeImage.Format.RGBA,
            2,
            2,
            Pixels(255, 255, 255, 255));
        var sprite = new SpriteContents(
            ResourceLocation.Parse("minecraft:block/dispose"),
            new FrameSize(2, 2),
            image);
        sprite.Dispose();
        sprite.Dispose();

        Expect<ObjectDisposedException>(
            () => sprite.ComputeTransparency(0, 0, 1, 1),
            "Disposed sprite contents cannot compute transparency.");
        Expect<ObjectDisposedException>(
            () => _ = sprite.OriginalImage,
            "Disposed sprite contents cannot expose the original image.");
    }

    private static byte[] Pixels(params byte[] alphaValues)
    {
        byte[] result = new byte[alphaValues.Length * 4];
        for (int i = 0; i < alphaValues.Length; i++)
        {
            result[i * 4] = 32;
            result[i * 4 + 1] = 96;
            result[i * 4 + 2] = 160;
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
