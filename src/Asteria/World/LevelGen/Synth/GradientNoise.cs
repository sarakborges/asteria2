using Asteria.World.LevelGen;

namespace Asteria.World.LevelGen.Synth;

/// <summary>Permutation table and gradient vectors shared by simplex noise.</summary>
public abstract class GradientNoise
{
    protected static readonly (int X, int Y, int Z)[] Gradients =
    [
        (1, 1, 0), (-1, 1, 0), (1, -1, 0), (-1, -1, 0),
        (1, 0, 1), (-1, 0, 1), (1, 0, -1), (-1, 0, -1),
        (0, 1, 1), (0, -1, 1), (0, 1, -1), (0, -1, -1),
        (1, 1, 0), (0, -1, 1), (-1, 1, 0), (0, -1, -1)
    ];

    protected readonly byte[] Permutations = new byte[256];
    protected readonly double OffsetX;
    protected readonly double OffsetY;
    protected readonly double OffsetZ;

    protected GradientNoise(WorldgenRandom random, double noiseOffsetScale)
    {
        ArgumentNullException.ThrowIfNull(random);

        // These draws are required even when the scale is zero.
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

    protected int Permute(int value) => Permutations[value & 0xFF];

}
