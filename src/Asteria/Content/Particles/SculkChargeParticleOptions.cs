using Asteria.Serialization;

namespace Asteria.Content.Particles;

/// <summary>
/// Options for the sculk charge particle. Its roll value is a required float field.
/// </summary>
public sealed record SculkChargeParticleOptions(float Roll) : IParticleOptions
{
    public static MapCodec<SculkChargeParticleOptions> CODEC { get; } =
        Codecs.FLOAT.FieldOf("roll")
            .XMap(
                static roll => new SculkChargeParticleOptions(roll),
                static option => option.Roll);

    public ParticleType GetParticleType() => ParticleTypes.SCULK_CHARGE;
}
