using Asteria.Core;
using Asteria.World.LevelGen;

namespace Asteria.World.LevelGen.Synth;

/// <summary>
/// Two-dimensional simplex noise using the 26.3 gradient table, permutation setup,
/// skewing constants, corner attenuation and output scale.
/// </summary>
public sealed class SimplexNoise : GradientNoise
{
    private static readonly double F2 = 0.5 * (Math.Sqrt(3.0) - 1.0);
    private static readonly double G2 = (3.0 - Math.Sqrt(3.0)) / 6.0;

    public SimplexNoise(WorldgenRandom random, bool discardNoiseOffset)
        : base(random, discardNoiseOffset ? 0.0 : 256.0)
    {
    }

    private static double GetCornerNoise(int gradientIndex, double x, double y)
    {
        double attenuation = 0.5 - x * x - y * y;
        if (attenuation < 0.0)
            return 0.0;

        attenuation *= attenuation;
        var gradient = Gradients[gradientIndex];
        return attenuation * attenuation * (gradient.X * x + gradient.Y * y);
    }

    public float Get(double xInput, double yInput)
    {
        double x = xInput + OffsetX;
        double y = yInput + OffsetY;
        double skew = (x + y) * F2;
        int i = Mth.Floor(x + skew);
        int j = Mth.Floor(y + skew);
        double unskew = (i + j) * G2;
        double x0 = x - (i - unskew);
        double y0 = y - (j - unskew);

        int i1;
        int j1;
        if (x0 > y0)
        {
            i1 = 1;
            j1 = 0;
        }
        else
        {
            i1 = 0;
            j1 = 1;
        }

        double x1 = x0 - i1 + G2;
        double y1 = y0 - j1 + G2;
        double x2 = x0 - 1.0 + 2.0 * G2;
        double y2 = y0 - 1.0 + 2.0 * G2;

        int ii = i & 0xFF;
        int jj = j & 0xFF;
        int gradient0 = Permute(ii + Permute(jj)) % 12;
        int gradient1 = Permute(ii + i1 + Permute(jj + j1)) % 12;
        int gradient2 = Permute(ii + 1 + Permute(jj + 1)) % 12;

        double n0 = GetCornerNoise(gradient0, x0, y0);
        double n1 = GetCornerNoise(gradient1, x1, y1);
        double n2 = GetCornerNoise(gradient2, x2, y2);
        return (float)(70.0 * (n0 + n1 + n2));
    }
}
