namespace Asteria.World.LevelGen.Synth;

/// <summary>
/// Legacy Nether biome fBm octave initializer from Minecraft Java 26.3.
/// </summary>
public static class LegacyFbmInitializer
{
    public static NoiseStack CreateForLegacyNetherBiome(
        IRandomSource random,
        int firstOctave,
        IReadOnlyList<double> amplitudes)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(amplitudes);

        int octaveCount = amplitudes.Count;
        int zeroOctaveIndex = unchecked(-firstOctave);
        var noiseLevels = new PerlinNoise?[octaveCount];
        PerlinNoise zeroOctave = new(random);

        if (zeroOctaveIndex >= 0 && zeroOctaveIndex < octaveCount)
        {
            double zeroOctaveAmplitude = amplitudes[zeroOctaveIndex];
            if (zeroOctaveAmplitude != 0.0)
                noiseLevels[zeroOctaveIndex] = zeroOctave;
        }

        for (int index = zeroOctaveIndex - 1; index >= 0; index--)
        {
            if (index < octaveCount)
            {
                double amplitude = amplitudes[index];
                if (amplitude != 0.0)
                    noiseLevels[index] = new PerlinNoise(random);
                else
                    SkipOctave(random);
            }
            else
            {
                SkipOctave(random);
            }
        }

        int generatedCount = noiseLevels.Count(static noise => noise is not null);
        int expectedCount = amplitudes.Count(static amplitude => amplitude != 0.0);
        if (generatedCount != expectedCount)
        {
            throw new InvalidOperationException(
                "Failed to create correct number of noise levels for given non-zero amplitudes");
        }

        if (zeroOctaveIndex < octaveCount - 1)
            throw new ArgumentException("Positive octaves are temporarily disabled");

        double frequency = Math.Pow(2.0, -zeroOctaveIndex);
        double amplitudeFactor = Math.Pow(2.0, octaveCount - 1) /
            (Math.Pow(2.0, octaveCount) - 1.0);
        NoiseStack.Builder stack = NoiseStack.CreateBuilder();

        for (int index = 0; index < noiseLevels.Length; index++)
        {
            PerlinNoise? noise = noiseLevels[index];
            if (noise is not null)
                stack.Add(noise, frequency, (float)(amplitudeFactor * amplitudes[index]));

            frequency *= 2.0;
            amplitudeFactor *= 0.5;
        }

        return stack.Build();
    }

    private static void SkipOctave(IRandomSource random) =>
        random.ConsumeCount(262);
}
