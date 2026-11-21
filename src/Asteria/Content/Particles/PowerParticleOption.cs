using Asteria.Serialization;

namespace Asteria.Content.Particles;

/// <summary>
/// Particle options carrying an optional power value. The absent/default power is 1.0
/// and is omitted from encoded map data.
/// </summary>
public sealed class PowerParticleOption : IParticleOptions
{
    public ParticleType<PowerParticleOption> Type { get; }
    public float Power { get; }

    private PowerParticleOption(ParticleType<PowerParticleOption> type, float power)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Power = power;
    }

    public static MapCodec<PowerParticleOption> CreateCodec(
        ParticleType<PowerParticleOption> type) =>
        Codecs.FLOAT.OptionalFieldOf("power", 1.0F)
            .XMap(
                power => new PowerParticleOption(type, power),
                static option => option.Power);

    public ParticleType GetParticleType() => Type;

    public float GetPower() => Power;

    public static PowerParticleOption Create(
        ParticleType<PowerParticleOption> type,
        float power) =>
        new(type, power);
}
