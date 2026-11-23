using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Synth;

internal static class LegacyFbmInitializerChecks
{
    public static void Run()
    {
        CheckLegacyOctavePlacementAndSkipping();
        CheckDisabledZeroOctaveConsumption();
        CheckPositiveOctaveFailure();
        CheckNormalNoiseLegacyPipeline();
    }

    private static void CheckLegacyOctavePlacementAndSkipping()
    {
        var random = new LegacyRandomSource(778899L);
        NoiseStack actual = LegacyFbmInitializer.CreateForLegacyNetherBiome(
            random,
            firstOctave: -2,
            amplitudes: new[] { 1.0, 0.0, 1.0 });

        var expectedRandom = new LegacyRandomSource(778899L);
        PerlinNoise zeroOctave = new(expectedRandom);
        expectedRandom.ConsumeCount(262);
        PerlinNoise lowestOctave = new(expectedRandom);

        NoiseStack expected = NoiseStack.CreateBuilder()
            .Add(lowestOctave, 0.25, (float)(4.0 / 7.0))
            .Add(zeroOctave, 1.0, (float)(1.0 / 7.0))
            .Build();

        foreach ((double x, double y, double z) in new[]
        {
            (0.0, 0.0, 0.0),
            (4.25, 72.0, -9.5),
            (-128.75, 34.5, 256.125),
            (8192.0, -12.0, -4096.0)
        })
        {
            Require(actual.Get(x, y, z) == expected.Get(x, y, z),
                "LegacyFbmInitializer must place octaves in ascending frequency order and consume exactly 262 draws for a muted octave.");
        }
    }

    private static void CheckDisabledZeroOctaveConsumption()
    {
        var random = new LegacyRandomSource(112233L);
        _ = LegacyFbmInitializer.CreateForLegacyNetherBiome(
            random,
            firstOctave: -2,
            amplitudes: new[] { 1.0, 1.0, 0.0 });

        var expectedRandom = new LegacyRandomSource(112233L);
        _ = new PerlinNoise(expectedRandom);
        _ = new PerlinNoise(expectedRandom);
        expectedRandom.ConsumeCount(262);
        Require(random.NextLong() == expectedRandom.NextLong(),
            "LegacyFbmInitializer must construct the zero octave even when its modifier is zero, then skip muted lower octave draws.");
    }

    private static void CheckPositiveOctaveFailure()
    {
        ArgumentException exception = Capture<ArgumentException>(
            () => LegacyFbmInitializer.CreateForLegacyNetherBiome(
                new LegacyRandomSource(9L),
                firstOctave: 1,
                amplitudes: new[] { 0.0, 0.0 }),
            "LegacyFbmInitializer must reject positive octave indices.");
        Require(exception.Message == "Positive octaves are temporarily disabled",
            "LegacyFbmInitializer must preserve the vanilla positive-octave failure.");
    }

    private static void CheckNormalNoiseLegacyPipeline()
    {
        NormalNoise normal = NormalNoise.CreateBuilder()
            .SetBaseOctave(-2)
            .SetOctaveCount(3)
            .SetAmplitudeModifier(0, 1.0)
            .SetAmplitudeModifier(1, 0.0)
            .SetAmplitudeModifier(2, 1.0)
            .Build();

#pragma warning disable CS0618
        NoiseStack actual = normal.CreateForLegacyNetherBiome(new LegacyRandomSource(445566L));
#pragma warning restore CS0618

        IReadOnlyList<double> amplitudes = new[] { 1.0, 0.0, 1.0 };
        var root = new LegacyRandomSource(445566L);
        NoiseStack first = LegacyFbmInitializer.CreateForLegacyNetherBiome(root, -2, amplitudes);
        NoiseStack second = LegacyFbmInitializer.CreateForLegacyNetherBiome(root, -2, amplitudes);
        NoiseStack expected = NoiseStack.CreateBuilder()
            .AddStack(first, 1.0, 1.0f)
            .AddStack(second, 1.0181268882175227, 1.0f)
            .Build();

        foreach ((double x, double y, double z) in new[]
        {
            (0.0, 0.0, 0.0),
            (11.5, 60.0, -18.25),
            (-256.0, 90.0, 2048.5)
        })
        {
            Require(float.IsFinite(actual.Get(x, y, z)) && actual.Get(x, y, z) == expected.Get(x, y, z),
                "NormalNoise.createForLegacyNetherBiome must compose two sequential legacy FBM initializers with the vanilla second-stack frequency factor.");
        }
    }

    private static TException Capture<TException>(Action action, string message)
        where TException : Exception
    {
        try { action(); }
        catch (TException exception) { return exception; }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
