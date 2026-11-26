using Asteria.Content.Identifiers;

namespace Asteria.Client.Rendering;

/// <summary>
/// A sprite's region within a texture atlas. Port of Minecraft Java 26.3
/// TextureAtlasSprite's atlas coordinates, UV mapping, transparency and ownership contract.
/// GPU texture upload remains the responsibility of the rendering backend.
/// </summary>
public sealed class TextureAtlasSprite : IDisposable
{
    public ResourceLocation AtlasLocation { get; }
    public SpriteContents Contents { get; }
    public int X { get; }
    public int Y { get; }
    public int Padding { get; }

    private readonly float _u0;
    private readonly float _u1;
    private readonly float _v0;
    private readonly float _v1;

    public TextureAtlasSprite(
        ResourceLocation atlasLocation,
        SpriteContents contents,
        int atlasWidth,
        int atlasHeight,
        int x,
        int y,
        int padding)
    {
        AtlasLocation = atlasLocation;
        Contents = contents ?? throw new ArgumentNullException(nameof(contents));
        Padding = padding;
        X = x;
        Y = y;

        _u0 = (float)(x + padding) / atlasWidth;
        _u1 = (float)(x + padding + contents.Width) / atlasWidth;
        _v0 = (float)(y + padding) / atlasHeight;
        _v1 = (float)(y + padding + contents.Height) / atlasHeight;
    }

    public int GetX() => X;
    public int GetY() => Y;

    public float GetU0() => _u0;
    public float GetU1() => _u1;
    public float GetV0() => _v0;
    public float GetV1() => _v1;

    public float GetU(float offset)
    {
        float difference = _u1 - _u0;
        return _u0 + difference * offset;
    }

    public float GetV(float offset)
    {
        float difference = _v1 - _v0;
        return _v0 + difference * offset;
    }

    public Transparency GetTransparency() =>
        Contents.SpriteTransparency;

    public bool IsAnimated() =>
        Contents.IsAnimated;

    public void Dispose() =>
        Contents.Dispose();

    public override string ToString() =>
        $"TextureAtlasSprite{{contents='{Contents}', u0={_u0}, u1={_u1}, v0={_v0}, v1={_v1}}}";
}
