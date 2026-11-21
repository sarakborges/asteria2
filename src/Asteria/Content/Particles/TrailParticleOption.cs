using Asteria.Core;
using Asteria.Serialization;

namespace Asteria.Content.Particles;

/// <summary>
/// Particle options that describe a target point, an RGB color and a strictly positive
/// display duration. This type has local data serialization only; packet streams are out of scope.
/// </summary>
public sealed record TrailParticleOption(Vec3 Target, int Color, int Duration) : IParticleOptions
{
    public static MapCodec<TrailParticleOption> CODEC { get; } =
        RecordCodecBuilder.MapCodec<TrailParticleOption>(instance =>
            instance.Group(
                Vec3.CODEC.FieldOf("target")
                    .ForGetter<TrailParticleOption>(static option => option.Target),
                ExtraCodecs.RGB_COLOR_CODEC.FieldOf("color")
                    .ForGetter<TrailParticleOption>(static option => option.Color),
                ExtraCodecs.POSITIVE_INT.FieldOf("duration")
                    .ForGetter<TrailParticleOption>(static option => option.Duration))
            .Apply(instance, static (target, color, duration) =>
                new TrailParticleOption(target, color, duration)));

    public ParticleType GetParticleType() => ParticleTypes.TRAIL;
}
