using System.Numerics;
using Asteria.Serialization;
using Asteria.Util;

namespace Asteria.Content.Particles;

/// <summary>RGB dust particle options transitioning between two colors.</summary>
public sealed class DustColorTransitionOptions : ScalableParticleOptionsBase
{
    public static MapCodec<DustColorTransitionOptions> CODEC { get; } =
        RecordCodecBuilder.MapCodec<DustColorTransitionOptions>(instance =>
            instance.Group(
                ExtraCodecs.RGB_COLOR_CODEC.FieldOf("from_color")
                    .ForGetter<DustColorTransitionOptions>(static option => option.FromColor),
                ExtraCodecs.RGB_COLOR_CODEC.FieldOf("to_color")
                    .ForGetter<DustColorTransitionOptions>(static option => option.ToColor),
                SCALE.FieldOf("scale")
                    .ForGetter<DustColorTransitionOptions>(static option => option.GetScale()))
            .Apply(instance, static (fromColor, toColor, scale) =>
                new DustColorTransitionOptions(fromColor, toColor, scale)));

    public int FromColor { get; }
    public int ToColor { get; }

    public DustColorTransitionOptions(int fromColor, int toColor, float scale)
        : base(scale)
    {
        FromColor = fromColor;
        ToColor = toColor;
    }

    public Vector3 GetFromColor() => Argb.Vector3FromRgb24(FromColor);

    public Vector3 GetToColor() => Argb.Vector3FromRgb24(ToColor);

    public override ParticleType GetParticleType() => ParticleTypes.DUST_COLOR_TRANSITION;
}
