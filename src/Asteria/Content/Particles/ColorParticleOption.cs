using Asteria.Serialization;
using Asteria.Util;

namespace Asteria.Content.Particles;

/// <summary>
/// Particle options carrying a packed ARGB color. The map codec writes an integer and
/// accepts a four-component RGBA vector as the source-compatible alternative.
/// </summary>
public sealed class ColorParticleOption : IParticleOptions
{
    public ParticleType<ColorParticleOption> Type { get; }
    public int Color { get; }

    private ColorParticleOption(ParticleType<ColorParticleOption> type, int color)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Color = color;
    }

    public static MapCodec<ColorParticleOption> CreateCodec(
        ParticleType<ColorParticleOption> type) =>
        ExtraCodecs.ARGB_COLOR_CODEC
            .FieldOf("color")
            .XMap(
                color => new ColorParticleOption(type, color),
                option => option.Color);

    public ParticleType GetParticleType() => Type;

    public float GetRed() => Argb.RedFloat(Color);
    public float GetGreen() => Argb.GreenFloat(Color);
    public float GetBlue() => Argb.BlueFloat(Color);
    public float GetAlpha() => Argb.AlphaFloat(Color);

    public static ColorParticleOption Create(
        ParticleType<ColorParticleOption> type,
        int color) =>
        new(type, color);

    public static ColorParticleOption Create(
        ParticleType<ColorParticleOption> type,
        float red,
        float green,
        float blue) =>
        Create(type, Argb.ColorFromFloat(1.0F, red, green, blue));
}
