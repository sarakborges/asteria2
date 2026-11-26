using Asteria.Client.Rendering;
using Asteria.Content.Identifiers;

internal static class TextureAtlasSpriteChecks
{
    public static void Run()
    {
        CheckAtlasCoordinates();
        CheckUvInterpolation();
        CheckContentDelegation();
        CheckOwnershipAndLifetime();
    }

    private static void CheckAtlasCoordinates()
    {
        using var contents = CreateContents(16, 32, Enumerable.Repeat((byte)255, 16 * 32).ToArray());
        using var sprite = new TextureAtlasSprite(
            ResourceLocation.Parse("minecraft:textures/atlas/blocks"),
            contents,
            atlasWidth: 256,
            atlasHeight: 128,
            x: 32,
            y: 64,
            padding: 2);

        Require(sprite.GetX() == 32 && sprite.GetY() == 64, "TextureAtlasSprite must retain the unpadded atlas origin.");
        Require(
            sprite.GetU0() == 34.0f / 256.0f &&
            sprite.GetU1() == 50.0f / 256.0f &&
            sprite.GetV0() == 66.0f / 128.0f &&
            sprite.GetV1() == 98.0f / 128.0f,
            "TextureAtlasSprite UV bounds must include padding and the exact SpriteContents frame dimensions.");
        Require(
            sprite.AtlasLocation == ResourceLocation.Parse("minecraft:textures/atlas/blocks") &&
            sprite.Padding == 2,
            "TextureAtlasSprite must retain its atlas identifier and padding metadata.");
    }

    private static void CheckUvInterpolation()
    {
        using var contents = CreateContents(16, 32, Enumerable.Repeat((byte)255, 16 * 32).ToArray());
        using var sprite = new TextureAtlasSprite(
            ResourceLocation.Parse("minecraft:atlas"),
            contents,
            atlasWidth: 256,
            atlasHeight: 128,
            x: 32,
            y: 64,
            padding: 2);

        Require(sprite.GetU(0.0f) == sprite.GetU0(), "U at zero must equal the minimum atlas coordinate.");
        Require(sprite.GetU(1.0f) == sprite.GetU1(), "U at one must equal the maximum atlas coordinate.");
        Require(sprite.GetV(0.0f) == sprite.GetV0(), "V at zero must equal the minimum atlas coordinate.");
        Require(sprite.GetV(1.0f) == sprite.GetV1(), "V at one must equal the maximum atlas coordinate.");
        Require(
            sprite.GetU(0.5f) == sprite.GetU0() + (sprite.GetU1() - sprite.GetU0()) * 0.5f &&
            sprite.GetV(0.5f) == sprite.GetV0() + (sprite.GetV1() - sprite.GetV0()) * 0.5f,
            "UV interpolation must use the reference linear mapping without clamping the offset.");
        Require(
            sprite.GetU(-0.5f) == sprite.GetU0() + (sprite.GetU1() - sprite.GetU0()) * -0.5f,
            "UV mapping must preserve extrapolation semantics for offsets outside zero to one.");
    }

    private static void CheckContentDelegation()
    {
        byte[] transparentPixels = Enumerable.Repeat((byte)255, 16 * 32).ToArray();
        transparentPixels[^1] = 0;
        using var transparentContents = CreateContents(16, 32, transparentPixels);
        using var transparentSprite = new TextureAtlasSprite(
            ResourceLocation.Parse("minecraft:atlas"),
            transparentContents,
            256,
            128,
            0,
            0,
            0);

        Require(
            transparentSprite.GetTransparency() == Transparency.TRANSPARENT,
            "TextureAtlasSprite transparency must be delegated to SpriteContents.");
        Require(
            !transparentSprite.IsAnimated(),
            "TextureAtlasSprite animation state must be delegated to SpriteContents.");

        byte[] animatedPixels = Enumerable.Repeat((byte)255, 4 * 2).ToArray();
        using var animatedContents = new SpriteContents(
            ResourceLocation.Parse("minecraft:block/animated"),
            new FrameSize(1, 1),
            new NativeImage(NativeImage.Format.RGBA, 2, 1, ToRgba(animatedPixels)),
            new[]
            {
                new SpriteContents.FrameInfo(0, 2),
                new SpriteContents.FrameInfo(1, 2)
            },
            defaultFrameTime: 1);
        using var animatedSprite = new TextureAtlasSprite(
            ResourceLocation.Parse("minecraft:atlas"),
            animatedContents,
            16,
            16,
            0,
            0,
            0);

        Require(
            animatedSprite.IsAnimated(),
            "An atlas sprite must reflect the animation state of its contents.");
    }

    private static void CheckOwnershipAndLifetime()
    {
        var contents = CreateContents(2, 2, Enumerable.Repeat((byte)255, 4).ToArray());
        var sprite = new TextureAtlasSprite(
            ResourceLocation.Parse("minecraft:atlas"),
            contents,
            16,
            16,
            0,
            0,
            0);

        sprite.Dispose();
        Require(
            Expect<ObjectDisposedException>(() => _ = contents.OriginalImage),
            "Closing a texture atlas sprite must close its owned SpriteContents image.");
    }

    private static SpriteContents CreateContents(int width, int height, byte[] alpha)
    {
        byte[] rgba = ToRgba(alpha);
        return new SpriteContents(
            ResourceLocation.Parse("minecraft:block/test"),
            new FrameSize(width, height),
            new NativeImage(NativeImage.Format.RGBA, width, height, rgba));
    }

    private static byte[] ToRgba(byte[] alpha)
    {
        byte[] rgba = new byte[alpha.Length * 4];
        for (int i = 0; i < alpha.Length; i++)
        {
            rgba[i * 4] = 64;
            rgba[i * 4 + 1] = 128;
            rgba[i * 4 + 2] = 192;
            rgba[i * 4 + 3] = alpha[i];
        }

        return rgba;
    }

    private static bool Expect<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return true;
        }

        return false;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
