using Asteria.Serialization;

namespace Asteria.Content.Particles;

/// <summary>A shriek particle carrying its required integer delay.</summary>
public sealed class ShriekParticleOption : IParticleOptions
{
    public static MapCodec<ShriekParticleOption> CODEC { get; } =
        Codecs.INT.FieldOf("delay")
            .XMap(
                static delay => new ShriekParticleOption(delay),
                static option => option.Delay);

    public int Delay { get; }

    public ShriekParticleOption(int delay) =>
        Delay = delay;

    public ParticleType GetParticleType() => ParticleTypes.SHRIEK;

    public int GetDelay() => Delay;
}
