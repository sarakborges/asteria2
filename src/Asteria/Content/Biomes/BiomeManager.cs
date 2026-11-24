using System.Buffers.Binary;
using System.Security.Cryptography;
using Asteria.Content.Registries;
using Asteria.Core;

namespace Asteria.Content.Biomes;

/// <summary>
/// Applies Minecraft Java 26.3 fuzzy biome zoom when converting block positions to biomes.
/// Noise samplers remain quart-based; this manager selects the nearest jittered quart corner.
/// </summary>
public sealed class BiomeManager
{
    public static int ChunkCenterQuart => QuartPos.FromBlock(8);

    private readonly BiomeResolver _noiseBiomeSource;
    private readonly long _biomeZoomSeed;

    public BiomeManager(BiomeResolver noiseBiomeSource, long biomeZoomSeed)
    {
        _noiseBiomeSource = noiseBiomeSource ??
            throw new ArgumentNullException(nameof(noiseBiomeSource));
        _biomeZoomSeed = biomeZoomSeed;
    }

    public static long ObfuscateSeed(long seed)
    {
        Span<byte> input = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(input, seed);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        return BinaryPrimitives.ReadInt64LittleEndian(hash);
    }

    public BiomeManager WithDifferentSource(BiomeResolver biomeSource) =>
        new(biomeSource, _biomeZoomSeed);

    public Holder<Biome> GetBiome(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return GetBiome(position.GetX(), position.GetY(), position.GetZ());
    }

    public Holder<Biome> GetBiome(int x, int y, int z)
    {
        int absX = unchecked(x - 2);
        int absY = unchecked(y - 2);
        int absZ = unchecked(z - 2);
        int parentX = absX >> 2;
        int parentY = absY >> 2;
        int parentZ = absZ >> 2;
        double fractX = (absX & 3) / 4.0;
        double fractY = (absY & 3) / 4.0;
        double fractZ = (absZ & 3) / 4.0;

        int minCorner = 0;
        double minFiddledDistance = double.PositiveInfinity;
        for (int corner = 0; corner < 8; corner++)
        {
            bool xEven = (corner & 4) == 0;
            bool yEven = (corner & 2) == 0;
            bool zEven = (corner & 1) == 0;

            int cornerX = xEven ? parentX : unchecked(parentX + 1);
            int cornerY = yEven ? parentY : unchecked(parentY + 1);
            int cornerZ = zEven ? parentZ : unchecked(parentZ + 1);

            double distanceX = xEven ? fractX : fractX - 1.0;
            double distanceY = yEven ? fractY : fractY - 1.0;
            double distanceZ = zEven ? fractZ : fractZ - 1.0;

            double distance = GetFiddledDistance(
                _biomeZoomSeed,
                cornerX,
                cornerY,
                cornerZ,
                distanceX,
                distanceY,
                distanceZ);

            if (minFiddledDistance > distance)
            {
                minCorner = corner;
                minFiddledDistance = distance;
            }
        }

        int biomeX = (minCorner & 4) == 0 ? parentX : unchecked(parentX + 1);
        int biomeY = (minCorner & 2) == 0 ? parentY : unchecked(parentY + 1);
        int biomeZ = (minCorner & 1) == 0 ? parentZ : unchecked(parentZ + 1);
        return _noiseBiomeSource.GetNoiseBiome(biomeX, biomeY, biomeZ);
    }

    public Holder<Biome> GetNoiseBiomeAtPosition(double x, double y, double z) =>
        GetNoiseBiomeAtQuart(
            QuartPos.FromBlock(Mth.Floor(x)),
            QuartPos.FromBlock(Mth.Floor(y)),
            QuartPos.FromBlock(Mth.Floor(z)));

    public Holder<Biome> GetNoiseBiomeAtPosition(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return GetNoiseBiomeAtQuart(
            QuartPos.FromBlock(position.GetX()),
            QuartPos.FromBlock(position.GetY()),
            QuartPos.FromBlock(position.GetZ()));
    }

    public Holder<Biome> GetNoiseBiomeAtQuart(int quartX, int quartY, int quartZ) =>
        _noiseBiomeSource.GetNoiseBiome(quartX, quartY, quartZ);

    private static double GetFiddledDistance(
        long seed,
        int xRandom,
        int yRandom,
        int zRandom,
        double distanceX,
        double distanceY,
        double distanceZ)
    {
        long value = seed;
        value = LinearCongruentialGenerator.Next(value, xRandom);
        value = LinearCongruentialGenerator.Next(value, yRandom);
        value = LinearCongruentialGenerator.Next(value, zRandom);
        value = LinearCongruentialGenerator.Next(value, xRandom);
        value = LinearCongruentialGenerator.Next(value, yRandom);
        value = LinearCongruentialGenerator.Next(value, zRandom);
        double fiddleX = GetFiddle(value);
        value = LinearCongruentialGenerator.Next(value, seed);
        double fiddleY = GetFiddle(value);
        value = LinearCongruentialGenerator.Next(value, seed);
        double fiddleZ = GetFiddle(value);

        double offsetX = distanceX + fiddleX;
        double offsetY = distanceY + fiddleY;
        double offsetZ = distanceZ + fiddleZ;
        return offsetZ * offsetZ + offsetY * offsetY + offsetX * offsetX;
    }

    private static double GetFiddle(long value)
    {
        long shifted = value >> 24;
        long uniform = shifted % 1024;
        if (uniform < 0)
            uniform += 1024;
        return (uniform / 1024.0 - 0.5) * 0.9;
    }
}
