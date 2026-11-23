using Asteria.Core;
using Asteria.Util;
using Asteria.World.LevelGen;

namespace Asteria.World.LevelGen.Synth;

/// <summary>
/// Two- and three-dimensional simplex noise ported from Minecraft Java 26.3.
/// </summary>
public sealed class SimplexNoise : GradientNoise, Noise
{
    private const double Sqrt3 = 1.7320508075688772;
    private static readonly double F2 = 0.5 * (Sqrt3 - 1.0);
    private static readonly double G2 = (3.0 - Sqrt3) / 6.0;

    public static readonly Interval RANGE = Interval.OfSymmetric(2.0f);
    public const double STANDARD_DEVIATION = 0.42544;

    public SimplexNoise(WorldgenRandom random)
        : base(random)
    {
    }

    public SimplexNoise(WorldgenRandom random, bool discardNoiseOffset)
        : base(random, discardNoiseOffset ? 0.0 : 256.0)
    {
    }

    public Interval GetRange() => RANGE;

    private static double GetCornerNoise3D(
        int gradientIndex,
        double x,
        double y,
        double z,
        double baseValue)
    {
        double attenuation = baseValue - x * x - y * y - z * z;
        if (attenuation < 0.0)
            return 0.0;

        attenuation *= attenuation;
        return attenuation * attenuation * Gradients[gradientIndex].Dot(x, y, z);
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

        double n0 = GetCornerNoise3D(gradient0, x0, y0, 0.0, 0.5);
        double n1 = GetCornerNoise3D(gradient1, x1, y1, 0.0, 0.5);
        double n2 = GetCornerNoise3D(gradient2, x2, y2, 0.0, 0.5);
        return (float)(70.0 * (n0 + n1 + n2));
    }

    public float Get(double xInput, double yInput, double zInput)
    {
        double x = xInput + OffsetX;
        double y = yInput + OffsetY;
        double z = zInput + OffsetZ;

        const double f3 = 0.3333333333333333;
        double skew = (x + y + z) * f3;
        int i = Mth.Floor(x + skew);
        int j = Mth.Floor(y + skew);
        int k = Mth.Floor(z + skew);

        const double g3 = 0.16666666666666666;
        double unskew = (i + j + k) * g3;
        double x0 = x - (i - unskew);
        double y0 = y - (j - unskew);
        double z0 = z - (k - unskew);

        int i1, j1, k1;
        int i2, j2, k2;

        if (x0 >= y0)
        {
            if (y0 >= z0)
            {
                i1 = 1; j1 = 0; k1 = 0;
                i2 = 1; j2 = 1; k2 = 0;
            }
            else if (x0 >= z0)
            {
                i1 = 1; j1 = 0; k1 = 0;
                i2 = 1; j2 = 0; k2 = 1;
            }
            else
            {
                i1 = 0; j1 = 0; k1 = 1;
                i2 = 1; j2 = 0; k2 = 1;
            }
        }
        else if (y0 < z0)
        {
            i1 = 0; j1 = 0; k1 = 1;
            i2 = 0; j2 = 1; k2 = 1;
        }
        else if (x0 < z0)
        {
            i1 = 0; j1 = 1; k1 = 0;
            i2 = 0; j2 = 1; k2 = 1;
        }
        else
        {
            i1 = 0; j1 = 1; k1 = 0;
            i2 = 1; j2 = 1; k2 = 0;
        }

        double x1 = x0 - i1 + g3;
        double y1 = y0 - j1 + g3;
        double z1 = z0 - k1 + g3;
        double x2 = x0 - i2 + 2.0 * g3;
        double y2 = y0 - j2 + 2.0 * g3;
        double z2 = z0 - k2 + 2.0 * g3;
        double x3 = x0 - 1.0 + 0.5;
        double y3 = y0 - 1.0 + 0.5;
        double z3 = z0 - 1.0 + 0.5;

        int ii = i & 0xFF;
        int jj = j & 0xFF;
        int kk = k & 0xFF;

        int gradient0 = Permute(ii + Permute(jj + Permute(kk))) % 12;
        int gradient1 = Permute(ii + i1 + Permute(jj + j1 + Permute(kk + k1))) % 12;
        int gradient2 = Permute(ii + i2 + Permute(jj + j2 + Permute(kk + k2))) % 12;
        int gradient3 = Permute(ii + 1 + Permute(jj + 1 + Permute(kk + 1))) % 12;

        double n0 = GetCornerNoise3D(gradient0, x0, y0, z0, 0.6);
        double n1 = GetCornerNoise3D(gradient1, x1, y1, z1, 0.6);
        double n2 = GetCornerNoise3D(gradient2, x2, y2, z2, 0.6);
        double n3 = GetCornerNoise3D(gradient3, x3, y3, z3, 0.6);
        return (float)(32.0 * (n0 + n1 + n2 + n3));
    }
}
