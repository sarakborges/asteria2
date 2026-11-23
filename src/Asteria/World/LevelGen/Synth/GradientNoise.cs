using Asteria.World.LevelGen;

namespace Asteria.World.LevelGen.Synth;

/// <summary>
/// Shared permutation and gradient implementation used by Minecraft 26.3 simplex and Perlin noise.
/// </summary>
public abstract class GradientNoise
{
    private const int RoundOff = 33554432;
    private static readonly double HalfRoundOff = Math.BitDecrement(16777216.0);

    protected static readonly Gradient[] Gradients =
    [
        new(1, 1, 0),
        new(-1, 1, 0),
        new(1, -1, 0),
        new(-1, -1, 0),
        new(1, 0, 1),
        new(-1, 0, 1),
        new(1, 0, -1),
        new(-1, 0, -1),
        new(0, 1, 1),
        new(0, -1, 1),
        new(0, 1, -1),
        new(0, -1, -1),
        new(1, 1, 0),
        new(0, -1, 1),
        new(-1, 1, 0),
        new(0, -1, -1)
    ];

    protected readonly byte[] Permutations = new byte[256];
    protected readonly double OffsetX;
    protected readonly double OffsetY;
    protected readonly double OffsetZ;

    protected GradientNoise(WorldgenRandom random)
        : this(random, 256.0)
    {
    }

    protected GradientNoise(WorldgenRandom random, double noiseOffsetScale)
    {
        ArgumentNullException.ThrowIfNull(random);

        OffsetX = random.NextDouble() * noiseOffsetScale;
        OffsetY = random.NextDouble() * noiseOffsetScale;
        OffsetZ = random.NextDouble() * noiseOffsetScale;

        for (int i = 0; i < 256; i++)
            Permutations[i] = (byte)i;

        for (int i = 0; i < 256; i++)
        {
            int offset = random.NextInt(256 - i);
            (Permutations[i], Permutations[offset + i]) =
                (Permutations[offset + i], Permutations[i]);
        }
    }

    protected int Permute(int value) =>
        Permutations[value & 0xFF];

    protected Gradient PermuteToGrad(int value) =>
        Gradients[Permute(value) & 15];

    protected static float GradDot(int hash, float x, float y, float z) =>
        Gradients[hash & 15].Dot(x, y, z);

    /// <summary>
    /// Applies Minecraft's 2^25 farlands coordinate wrap. The SharedConstants debug
    /// override is intentionally absent because Asteria has no farlands-debug runtime flag.
    /// </summary>
    protected static double Wrap(double value) =>
        value >= -HalfRoundOff && value < HalfRoundOff
            ? value
            : value - Math.Floor(value / RoundOff + 0.5) * RoundOff;

    protected readonly record struct Gradient(int X, int Y, int Z)
    {
        public double Dot(double x, double y, double z) =>
            X * x + Y * y + Z * z;

        public float Dot(float x, float y, float z) =>
            X * x + Y * y + Z * z;

        public float DotXz(float x, float z) =>
            X * x + Z * z;
    }
}
