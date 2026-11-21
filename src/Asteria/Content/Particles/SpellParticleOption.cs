using System.Numerics;
using Asteria.Serialization;
using Asteria.Util;

namespace Asteria.Content.Particles;

/// <summary>
/// Particle options carrying an RGB color and power. Both fields use their 26.3 defaults
/// when absent, and default values are omitted when encoded.
/// </summary>
public sealed class SpellParticleOption : IParticleOptions
{
    public ParticleType<SpellParticleOption> Type { get; }
    public int Color { get; }
    public float Power { get; }

    private SpellParticleOption(
        ParticleType<SpellParticleOption> type,
        int color,
        float power)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Color = color;
        Power = power;
    }

    public static MapCodec<SpellParticleOption> CreateCodec(
        ParticleType<SpellParticleOption> type) =>
        RecordCodecBuilder.MapCodec<SpellParticleOption>(instance =>
            instance.Group(
                ExtraCodecs.RGB_COLOR_CODEC.OptionalFieldOf("color", -1)
                    .ForGetter<SpellParticleOption>(static option => option.Color),
                Codecs.FLOAT.OptionalFieldOf("power", 1.0F)
                    .ForGetter<SpellParticleOption>(static option => option.Power))
            .Apply(instance, (color, power) =>
                new SpellParticleOption(type, color, power)));

    public ParticleType GetParticleType() => Type;

    public float GetRed() => Argb.RedFloat(Color);
    public float GetGreen() => Argb.GreenFloat(Color);
    public float GetBlue() => Argb.BlueFloat(Color);

    public float GetPower() => Power;

    public static SpellParticleOption Create(
        ParticleType<SpellParticleOption> type,
        int color,
        float power) =>
        new(type, color, power);

    public static SpellParticleOption Create(
        ParticleType<SpellParticleOption> type,
        float red,
        float green,
        float blue,
        float power) =>
        Create(
            type,
            Argb.ColorFromVector3(new Vector3(red, green, blue)),
            power);
}
