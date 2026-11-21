using Asteria.Content.Particles;
using Asteria.Serialization;
using Asteria.World.LevelGen;

namespace Asteria.World.Attributes;

public sealed record AmbientParticle(IParticleOptions Particle, float Probability)
{
    public static Codec<AmbientParticle> CODEC { get; } =
        RecordCodecBuilder.Create<AmbientParticle>(instance =>
            instance.Group(
                ParticleTypes.CODEC.FieldOf("particle")
                    .ForGetter<AmbientParticle>(static value => value.Particle),
                Codecs.FloatRange(0.0F, 1.0F).FieldOf("probability")
                    .ForGetter<AmbientParticle>(static value => value.Probability))
            .Apply(instance, static (particle, probability) =>
                new AmbientParticle(particle, probability)));

    public bool CanSpawn(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return random.NextFloat() <= Probability;
    }

    public static IReadOnlyList<AmbientParticle> Of(
        IParticleOptions particle,
        float probability) =>
        Array.AsReadOnly(new[] { new AmbientParticle(particle, probability) });
}
