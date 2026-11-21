using System.Text.Json;
using Asteria.Content.Particles;
using Asteria.Serialization;
using Asteria.World.Attributes;
using Asteria.World.LevelGen;

internal static class AmbientParticleChecks
{
    public static void Run()
    {
        CheckCodecRoundTrip();
        CheckProbabilityValidation();
        CheckSpawnBoundary();
        CheckFactoryAndCrossFade();
        CheckRandomSourceFloat();
    }

    private static void CheckCodecRoundTrip()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var source = new AmbientParticle(ParticleTypes.FIREFLY, 0.375F);
        JsonElement encoded = AmbientParticle.CODEC.EncodeStart(ops, source).GetOrThrow();

        Require(ops.Get(encoded, "probability").GetOrThrow().GetSingle() == 0.375F,
            "AmbientParticle codec must write its probability field.");
        IParticleOptions decodedParticle = ParticleTypes.CODEC.Parse(
            ops,
            ops.Get(encoded, "particle").GetOrThrow()).GetOrThrow();
        Require(ReferenceEquals(decodedParticle, ParticleTypes.FIREFLY),
            "AmbientParticle codec must delegate its particle field to ParticleTypes.CODEC.");

        AmbientParticle decoded = AmbientParticle.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(decoded == source,
            "AmbientParticle codec must round-trip both particle options and probability.");
    }

    private static void CheckProbabilityValidation()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        foreach (float probability in new[] { 0.0F, 1.0F })
        {
            var valid = new AmbientParticle(ParticleTypes.ASH, probability);
            JsonElement encoded = AmbientParticle.CODEC.EncodeStart(ops, valid).GetOrThrow();
            Require(AmbientParticle.CODEC.Parse(ops, encoded).IsSuccess,
                "Ambient particle probability range endpoints must be inclusive.");
        }

        foreach (float probability in new[] { -0.001F, 1.001F })
        {
            JsonElement input = ops.CreateMap(new[]
            {
                new KeyValuePair<JsonElement, JsonElement>(
                    ops.CreateString("particle"),
                    ParticleTypes.CODEC.EncodeStart(ops, ParticleTypes.ASH).GetOrThrow()),
                new KeyValuePair<JsonElement, JsonElement>(
                    ops.CreateString("probability"),
                    ops.CreateFloat(probability))
            });
            Require(AmbientParticle.CODEC.Parse(ops, input).IsError,
                "Ambient particle codec must reject probabilities outside [0, 1].");
        }
    }

    private static void CheckSpawnBoundary()
    {
        var particle = new AmbientParticle(ParticleTypes.ASH, 0.5F);
        Require(particle.CanSpawn(new FixedRandomSource(0.5F)),
            "Ambient particle spawning must use <= at the probability boundary.");
        Require(!particle.CanSpawn(new FixedRandomSource(0.5001F)),
            "Ambient particle spawning must reject random values above the probability.");
        Require(new AmbientParticle(ParticleTypes.ASH, 0.0F).CanSpawn(new FixedRandomSource(0.0F)),
            "A zero-probability particle spawns when the random draw is exactly zero.");
        Require(new AmbientParticle(ParticleTypes.ASH, 1.0F).CanSpawn(new FixedRandomSource(0.999999F)),
            "A probability of one accepts every valid NextFloat result.");
    }

    private static void CheckFactoryAndCrossFade()
    {
        IReadOnlyList<AmbientParticle> singleton = AmbientParticle.Of(ParticleTypes.FIREFLY, 0.25F);
        Require(singleton.Count == 1 &&
                ReferenceEquals(singleton[0].Particle, ParticleTypes.FIREFLY) &&
                singleton[0].Probability == 0.25F,
            "AmbientParticle.Of must return a single entry with the supplied particle and probability.");

        IReadOnlyList<AmbientParticle> from =
            new[] { new AmbientParticle(ParticleTypes.ASH, 0.8F) };
        IReadOnlyList<AmbientParticle> to =
            new[] { new AmbientParticle(ParticleTypes.FIREFLY, 0.4F) };
        IReadOnlyList<AmbientParticle> middle =
            AttributeTypes.AMBIENT_PARTICLES.KeyframeLerp.Apply(0.25F, from, to);

        Require(middle.Count == 2 &&
                ReferenceEquals(middle[0].Particle, ParticleTypes.ASH) &&
                Math.Abs(middle[0].Probability - 0.6F) < 0.000001F &&
                ReferenceEquals(middle[1].Particle, ParticleTypes.FIREFLY) &&
                Math.Abs(middle[1].Probability - 0.1F) < 0.000001F,
            "Ambient particle cross-fade must preserve order and scale only probability.");
        Require(ReferenceEquals(AttributeTypes.AMBIENT_PARTICLES.KeyframeLerp.Apply(0.0F, from, to), from) &&
                ReferenceEquals(AttributeTypes.AMBIENT_PARTICLES.KeyframeLerp.Apply(1.0F, from, to), to),
            "Ambient particle cross-fade endpoints must return the original list references.");
    }

    private static void CheckRandomSourceFloat()
    {
        var random = new LegacyRandomSource(1234L);
        var reference = new LegacyRandomSource(1234L);
        float expected = reference.NextBits(24) * (1.0F / 16777216.0F);
        Require(random.NextFloat() == expected,
            "LegacyRandomSource.NextFloat must consume exactly 24 random bits.");

        var worldgen = new WorldgenRandom(new LegacyRandomSource(1234L));
        _ = worldgen.NextFloat();
        Require(worldgen.Count == 1,
            "WorldgenRandom.NextFloat must count as exactly one random-source call.");
    }

    private sealed class FixedRandomSource(float value) : IRandomSource
    {
        public float NextFloat() => value;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
