using Asteria.Core;
using Asteria.Util;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;

namespace Asteria.World.LevelGen.Synth;

/// <summary>
/// Perlin noise with vertically smeared gradients from Minecraft Java 26.3.
/// Used by the legacy blended-noise density function.
/// </summary>
public sealed class SmearedPerlinNoise : PerlinNoise
{
    private const float SHIFT_UP_EPSILON = 1.0E-7f;
    private readonly double _fudgeYScale;

    public SmearedPerlinNoise(IRandomSource random, double fudgeYScale)
        : base(random)
    {
        _fudgeYScale = fudgeYScale;
    }

    public static Interval Range(double fudgeYScale) =>
        Interval.OfSymmetric((float)(Math.Abs(fudgeYScale) + 2.0));

    public override Interval GetRange() =>
        Range(_fudgeYScale);

    public override float Get(double xInput, double yInput, double zInput)
    {
        double x = Wrap(xInput) + OffsetX;
        double y = Wrap(yInput) + OffsetY;
        double z = Wrap(zInput) + OffsetZ;

        int floorX = Mth.Floor(x);
        int floorY = Mth.Floor(y);
        int floorZ = Mth.Floor(z);

        float relativeX = (float)(x - floorX);
        double relativeY = y - floorY;
        float relativeZ = (float)(z - floorZ);
        float fudgedRelativeY = (float)(
            relativeY - ComputeFudgeY(yInput, relativeY));

        return SampleAndLerp(
            floorX,
            floorY,
            floorZ,
            relativeX,
            fudgedRelativeY,
            relativeZ,
            (float)relativeY);
    }

    private double ComputeFudgeY(double originalY, double relativeY)
    {
        double fudgeLimit =
            originalY >= 0.0 && originalY < relativeY
                ? originalY
                : relativeY;

        return Math.Floor(
            fudgeLimit / _fudgeYScale + SHIFT_UP_EPSILON) * _fudgeYScale;
    }

    public override void AddToVolume(
        DensityBuffer buffer,
        DensityVolume volume,
        double xzScale,
        double yScale,
        float amplitude)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(volume);

        float d000xz = 0.0f;
        float d100xz = 0.0f;
        float d010xz = 0.0f;
        float d110xz = 0.0f;
        float d001xz = 0.0f;
        float d101xz = 0.0f;
        float d011xz = 0.0f;
        float d111xz = 0.0f;
        float g000y = 0.0f;
        float g100y = 0.0f;
        float g010y = 0.0f;
        float g110y = 0.0f;
        float g001y = 0.0f;
        float g101y = 0.0f;
        float g011y = 0.0f;
        float g111y = 0.0f;
        int index = 0;

        for (int indexZ = 0; indexZ < volume.SizeZ; indexZ++)
        {
            double z = Wrap(volume.BlockZ(indexZ) * xzScale) + OffsetZ;
            int floorZ = Mth.Floor(z);
            float relativeZ = (float)(z - floorZ);
            float alphaZ = Mth.Smoothstep(relativeZ);

            for (int indexX = 0; indexX < volume.SizeX; indexX++)
            {
                double x = Wrap(volume.BlockX(indexX) * xzScale) + OffsetX;
                int floorX = Mth.Floor(x);
                float relativeX = (float)(x - floorX);
                int x0 = Permute(floorX);
                int x1 = Permute(floorX + 1);
                float alphaX = Mth.Smoothstep(relativeX);
                int lastFloorY = int.MinValue;

                for (int indexY = 0; indexY < volume.SizeY; indexY++)
                {
                    double originalY = volume.BlockY(indexY) * yScale;
                    double y = Wrap(originalY) + OffsetY;
                    int floorY = Mth.Floor(y);
                    double relativeY = y - floorY;
                    float alphaY = Mth.Smoothstep((float)relativeY);

                    if (lastFloorY != floorY)
                    {
                        int xy00 = Permute(x0 + floorY);
                        int xy01 = Permute(x0 + floorY + 1);
                        int xy10 = Permute(x1 + floorY);
                        int xy11 = Permute(x1 + floorY + 1);

                        Gradient g000 = PermuteToGrad(xy00 + floorZ);
                        d000xz = g000.DotXz(relativeX, relativeZ);
                        g000y = g000.Y;

                        Gradient g100 = PermuteToGrad(xy10 + floorZ);
                        d100xz = g100.DotXz(relativeX - 1.0f, relativeZ);
                        g100y = g100.Y;

                        Gradient g010 = PermuteToGrad(xy01 + floorZ);
                        d010xz = g010.DotXz(relativeX, relativeZ);
                        g010y = g010.Y;

                        Gradient g110 = PermuteToGrad(xy11 + floorZ);
                        d110xz = g110.DotXz(relativeX - 1.0f, relativeZ);
                        g110y = g110.Y;

                        Gradient g001 = PermuteToGrad(xy00 + floorZ + 1);
                        d001xz = g001.DotXz(relativeX, relativeZ - 1.0f);
                        g001y = g001.Y;

                        Gradient g101 = PermuteToGrad(xy10 + floorZ + 1);
                        d101xz = g101.DotXz(relativeX - 1.0f, relativeZ - 1.0f);
                        g101y = g101.Y;

                        Gradient g011 = PermuteToGrad(xy01 + floorZ + 1);
                        d011xz = g011.DotXz(relativeX, relativeZ - 1.0f);
                        g011y = g011.Y;

                        Gradient g111 = PermuteToGrad(xy11 + floorZ + 1);
                        d111xz = g111.DotXz(relativeX - 1.0f, relativeZ - 1.0f);
                        g111y = g111.Y;

                        lastFloorY = floorY;
                    }

                    float fudgedRelativeY = (float)(
                        relativeY - ComputeFudgeY(originalY, relativeY));

                    buffer.AddTo(
                        index,
                        amplitude * Mth.Lerp3(
                            alphaX,
                            alphaY,
                            alphaZ,
                            d000xz + g000y * fudgedRelativeY,
                            d100xz + g100y * fudgedRelativeY,
                            d010xz + g010y * (fudgedRelativeY - 1.0f),
                            d110xz + g110y * (fudgedRelativeY - 1.0f),
                            d001xz + g001y * fudgedRelativeY,
                            d101xz + g101y * fudgedRelativeY,
                            d011xz + g011y * (fudgedRelativeY - 1.0f),
                            d111xz + g111y * (fudgedRelativeY - 1.0f)));
                    index++;
                }
            }
        }
    }
}
