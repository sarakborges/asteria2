using Asteria.Serialization;
using Asteria.World.GameEvents;

namespace Asteria.Content.Particles;

/// <summary>
/// Particle options carrying a destination position source and arrival delay.
/// Only local block-position destinations are currently available in Asteria.
/// </summary>
public sealed record VibrationParticleOption(
    IPositionSource Destination,
    int ArrivalInTicks) : IParticleOptions
{
    private static readonly Codec<IPositionSource> SAFE_POSITION_SOURCE_CODEC =
        PositionSourceCodecs.CODEC.Validate(
            static source => source is BlockPositionSource
                ? DataResult<IPositionSource>.Success(source)
                : DataResult<IPositionSource>.Error(
                    () => "Entity position sources are not allowed"));

    public static MapCodec<VibrationParticleOption> CODEC { get; } =
        RecordCodecBuilder.MapCodec<VibrationParticleOption>(instance =>
            instance.Group(
                SAFE_POSITION_SOURCE_CODEC.FieldOf("destination")
                    .ForGetter<VibrationParticleOption>(static option => option.Destination),
                Codecs.INT.FieldOf("arrival_in_ticks")
                    .ForGetter<VibrationParticleOption>(static option => option.ArrivalInTicks))
            .Apply(instance, static (destination, arrivalInTicks) =>
                new VibrationParticleOption(destination, arrivalInTicks)));

    public ParticleType GetParticleType() => ParticleTypes.VIBRATION;
}
