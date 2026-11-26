namespace Asteria.Client.Rendering;

/// <summary>
/// Pixel transparency discovered in a sprite or in the UV region of a baked face.
/// Port of Minecraft Java 26.3 com.mojang.blaze3d.platform.Transparency.
/// </summary>
public readonly record struct Transparency(bool HasTransparent, bool HasTranslucent)
{
    public static readonly Transparency NONE = new(false, false);
    public static readonly Transparency TRANSPARENT = new(true, false);
    public static readonly Transparency TRANSLUCENT = new(false, true);
    public static readonly Transparency TRANSPARENT_AND_TRANSLUCENT = new(true, true);

    public static Transparency Of(bool hasTransparent, bool hasTranslucent)
    {
        if (hasTransparent && hasTranslucent)
            return TRANSPARENT_AND_TRANSLUCENT;

        if (hasTransparent)
            return TRANSPARENT;

        return hasTranslucent ? TRANSLUCENT : NONE;
    }

    public Transparency Or(Transparency other) =>
        Of(
            HasTransparent || other.HasTransparent,
            HasTranslucent || other.HasTranslucent);

    public bool IsOpaque() =>
        !HasTransparent && !HasTranslucent;
}
