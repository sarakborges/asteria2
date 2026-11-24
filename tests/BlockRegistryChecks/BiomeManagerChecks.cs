using Asteria.Content.Biomes;
using Asteria.Content.Registries;
using Asteria.Core;

internal static class BiomeManagerChecks
{
    private sealed class CapturingBiomeResolver : BiomeResolver
    {
        public (int X, int Y, int Z)? LastPosition { get; private set; }

        public Holder<Biome> GetNoiseBiome(int quartX, int quartY, int quartZ)
        {
            LastPosition = (quartX, quartY, quartZ);
            return Holder<Biome>.Direct(Biomes.PLAINS);
        }
    }

    public static void Run()
    {
        CheckSeedObfuscation();
        CheckFuzzyBiomeZoom();
        CheckQuartLookup();
    }

    private static void CheckSeedObfuscation()
    {
        const long seed = 0x504C4143454D4F44L;
        Require(
            BiomeManager.ObfuscateSeed(seed) == -4664597923171126727L,
            "BiomeManager.obfuscateSeed must use SHA-256 hashLong with little-endian seed bytes.");
    }

    private static void CheckFuzzyBiomeZoom()
    {
        const long seed = 0x504C4143454D4F44L;
        var resolver = new CapturingBiomeResolver();
        var manager = new BiomeManager(
            resolver,
            BiomeManager.ObfuscateSeed(seed));

        Holder<Biome> biome = manager.GetBiome(new BlockPos(5, 40, 7));
        Require(
            ReferenceEquals(biome.Value, Biomes.PLAINS) &&
            resolver.LastPosition == (0, 9, 2),
            "BiomeManager.getBiome must select the same jittered quart corner as Minecraft Java 26.3.");

        _ = manager.GetBiome(new BlockPos(0, 0, 0));
        Require(
            resolver.LastPosition == (0, -1, -1),
            "BiomeManager.getBiome must preserve floor-shift behavior for negative quart coordinates.");

        _ = manager.GetBiome(new BlockPos(100, 70, -100));
        Require(
            resolver.LastPosition == (25, 17, -25),
            "BiomeManager.getBiome must preserve deterministic corner selection across negative X/Z coordinates.");
    }

    private static void CheckQuartLookup()
    {
        var resolver = new CapturingBiomeResolver();
        var manager = new BiomeManager(resolver, biomeZoomSeed: 123L);
        _ = manager.GetNoiseBiomeAtPosition(new BlockPos(-1, 63, -5));

        Require(
            resolver.LastPosition == (-1, 15, -2),
            "BiomeManager.getNoiseBiomeAtPosition(BlockPos) must use direct quart conversion without fuzzy zoom.");

        var replacementResolver = new CapturingBiomeResolver();
        BiomeManager replacement = manager.WithDifferentSource(replacementResolver);
        _ = replacement.GetBiome(new BlockPos(5, 40, 7));

        Require(
            replacementResolver.LastPosition == (0, 9, 2),
            "BiomeManager.withDifferentSource must retain the obfuscated biome zoom seed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
