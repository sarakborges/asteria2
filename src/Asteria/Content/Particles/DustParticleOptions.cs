using System.Numerics;
using Asteria.Serialization;
using Asteria.Util;

namespace Asteria.Content.Particles;

/// <summary>RGB dust particle options with a clamped scale.</summary>
public sealed class DustParticleOptions : ScalableParticleOptionsBase
{
    public static MapCodec<DustParticleOptions> CODEC { get; } =
        RecordCodecBuilder.MapCodec<DustParticleOptions>(instance =>
            instance.Group(
                ExtraCodecs.RGB_COLOR_CODEC.FieldOf("color")
                    .ForGetter<DustParticleOptions>(static option => option.Color),
                SCALE.FieldOf("scale")
                    .ForGetter<DustParticleOptions>(static option => option.GetScale()))
            .Apply(instance, static (color, scale) =>
                new DustParticleOptions(color, scale)));

    public int Color { get; }

    public DustParticleOptions(int color, float scale) : base(scale) =>
        Color = color;

    public Vector3 GetColor() => Argb.Vector3FromRgb24(Color);

    public override ParticleType GetParticleType() => ParticleTypes.DUST;
}
