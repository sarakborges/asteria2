using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Synth;

namespace Asteria.Content.Biomes;

/// <summary>The fixed-seed two-dimensional noise shared by biome visual and world-generation rules.</summary>
internal static class BiomeInfoNoise
{
    private static readonly SimplexNoise Noise = new(
        new WorldgenRandom(new LegacyRandomSource(2345L)),
        discardNoiseOffset: true);

    public static float Get(double x, double z) => Noise.Get(x, z);
}
