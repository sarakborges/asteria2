using System.Text;
using Asteria.Core;
using Asteria.Util;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;

namespace Asteria.World.LevelGen.Synth;

/// <summary>
/// Three-dimensional gradient Perlin noise ported from Minecraft Java 26.3.
/// </summary>
public class PerlinNoise : GradientNoise, Noise
{
    public static readonly Interval RANGE = Interval.OfSymmetric(2.0f);
    public const double STANDARD_DEVIATION = 0.2702247831245211;

    public PerlinNoise(WorldgenRandom random)
        : base(random)
    {
    }

    public virtual Interval GetRange() => RANGE;

    public virtual float Get(double x, double y) =>
        Get(Wrap(x), 0.0, Wrap(y));

    public virtual float Get(double xInput, double yInput, double zInput)
    {
        double x = Wrap(xInput) + OffsetX;
        double y = Wrap(yInput) + OffsetY;
        double z = Wrap(zInput) + OffsetZ;
        int floorX = Mth.Floor(x);
        int floorY = Mth.Floor(y);
        int floorZ = Mth.Floor(z);
        float relativeX = (float)(x - floorX);
        float relativeY = (float)(y - floorY);
        float relativeZ = (float)(z - floorZ);
        return SampleAndLerp(floorX, floorY, floorZ, relativeX, relativeY, relativeZ, relativeY);
    }

    public float NoiseWithDerivative(
        double xInput,
        double yInput,
        double zInput,
        float[] derivativeOut)
    {
        ArgumentNullException.ThrowIfNull(derivativeOut);

        double x = Wrap(xInput) + OffsetX;
        double y = Wrap(yInput) + OffsetY;
        double z = Wrap(zInput) + OffsetZ;
        int floorX = Mth.Floor(x);
        int floorY = Mth.Floor(y);
        int floorZ = Mth.Floor(z);
        float relativeX = (float)(x - floorX);
        float relativeY = (float)(y - floorY);
        float relativeZ = (float)(z - floorZ);
        return SampleWithDerivative(
            floorX, floorY, floorZ,
            relativeX, relativeY, relativeZ,
            derivativeOut);
    }

    protected virtual float SampleAndLerp(
        int x,
        int y,
        int z,
        float relativeX,
        float relativeY,
        float relativeZ,
        float originalRelativeY)
    {
        int x0 = Permute(x);
        int x1 = Permute(x + 1);
        int xy00 = Permute(x0 + y);
        int xy01 = Permute(x0 + y + 1);
        int xy10 = Permute(x1 + y);
        int xy11 = Permute(x1 + y + 1);

        float d000 = GradDot(Permute(xy00 + z), relativeX, relativeY, relativeZ);
        float d100 = GradDot(Permute(xy10 + z), relativeX - 1.0f, relativeY, relativeZ);
        float d010 = GradDot(Permute(xy01 + z), relativeX, relativeY - 1.0f, relativeZ);
        float d110 = GradDot(Permute(xy11 + z), relativeX - 1.0f, relativeY - 1.0f, relativeZ);
        float d001 = GradDot(Permute(xy00 + z + 1), relativeX, relativeY, relativeZ - 1.0f);
        float d101 = GradDot(Permute(xy10 + z + 1), relativeX - 1.0f, relativeY, relativeZ - 1.0f);
        float d011 = GradDot(Permute(xy01 + z + 1), relativeX, relativeY - 1.0f, relativeZ - 1.0f);
        float d111 = GradDot(Permute(xy11 + z + 1), relativeX - 1.0f, relativeY - 1.0f, relativeZ - 1.0f);

        float xAlpha = Mth.Smoothstep(relativeX);
        float yAlpha = Mth.Smoothstep(originalRelativeY);
        float zAlpha = Mth.Smoothstep(relativeZ);
        return Mth.Lerp3(
            xAlpha, yAlpha, zAlpha,
            d000, d100, d010, d110,
            d001, d101, d011, d111);
    }

    public virtual void AddToVolume(
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
                    double y = Wrap(volume.BlockY(indexY) * yScale) + OffsetY;
                    int floorY = Mth.Floor(y);
                    float relativeY = (float)(y - floorY);
                    float alphaY = Mth.Smoothstep(relativeY);

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

                    buffer.AddTo(
                        index,
                        amplitude * Mth.Lerp3(
                            alphaX, alphaY, alphaZ,
                            d000xz + g000y * relativeY,
                            d100xz + g100y * relativeY,
                            d010xz + g010y * (relativeY - 1.0f),
                            d110xz + g110y * (relativeY - 1.0f),
                            d001xz + g001y * relativeY,
                            d101xz + g101y * relativeY,
                            d011xz + g011y * (relativeY - 1.0f),
                            d111xz + g111y * (relativeY - 1.0f)));
                    index++;
                }
            }
        }
    }

    private float SampleWithDerivative(
        int x,
        int y,
        int z,
        float xr,
        float yr,
        float zr,
        float[] derivativeOut)
    {
        int x0 = Permute(x);
        int x1 = Permute(x + 1);
        int xy00 = Permute(x0 + y);
        int xy01 = Permute(x0 + y + 1);
        int xy10 = Permute(x1 + y);
        int xy11 = Permute(x1 + y + 1);

        Gradient g000 = PermuteToGrad(xy00 + z);
        Gradient g100 = PermuteToGrad(xy10 + z);
        Gradient g010 = PermuteToGrad(xy01 + z);
        Gradient g110 = PermuteToGrad(xy11 + z);
        Gradient g001 = PermuteToGrad(xy00 + z + 1);
        Gradient g101 = PermuteToGrad(xy10 + z + 1);
        Gradient g011 = PermuteToGrad(xy01 + z + 1);
        Gradient g111 = PermuteToGrad(xy11 + z + 1);

        float d000 = g000.Dot(xr, yr, zr);
        float d100 = g100.Dot(xr - 1.0f, yr, zr);
        float d010 = g010.Dot(xr, yr - 1.0f, zr);
        float d110 = g110.Dot(xr - 1.0f, yr - 1.0f, zr);
        float d001 = g001.Dot(xr, yr, zr - 1.0f);
        float d101 = g101.Dot(xr - 1.0f, yr, zr - 1.0f);
        float d011 = g011.Dot(xr, yr - 1.0f, zr - 1.0f);
        float d111 = g111.Dot(xr - 1.0f, yr - 1.0f, zr - 1.0f);

        float xAlpha = Mth.Smoothstep(xr);
        float yAlpha = Mth.Smoothstep(yr);
        float zAlpha = Mth.Smoothstep(zr);
        float d1x = Mth.Lerp3(
            xAlpha, yAlpha, zAlpha,
            g000.X, g100.X, g010.X, g110.X,
            g001.X, g101.X, g011.X, g111.X);
        float d1y = Mth.Lerp3(
            xAlpha, yAlpha, zAlpha,
            g000.Y, g100.Y, g010.Y, g110.Y,
            g001.Y, g101.Y, g011.Y, g111.Y);
        float d1z = Mth.Lerp3(
            xAlpha, yAlpha, zAlpha,
            g000.Z, g100.Z, g010.Z, g110.Z,
            g001.Z, g101.Z, g011.Z, g111.Z);

        float d2x = Mth.Lerp2(
            yAlpha, zAlpha,
            d100 - d000, d110 - d010,
            d101 - d001, d111 - d011);
        float d2y = Mth.Lerp2(
            zAlpha, xAlpha,
            d010 - d000, d011 - d001,
            d110 - d100, d111 - d101);
        float d2z = Mth.Lerp2(
            xAlpha, yAlpha,
            d001 - d000, d101 - d100,
            d011 - d010, d111 - d110);

        float xSmoothstepDerivative = Mth.SmoothstepDerivative(xr);
        float ySmoothstepDerivative = Mth.SmoothstepDerivative(yr);
        float zSmoothstepDerivative = Mth.SmoothstepDerivative(zr);
        float dX = d1x + xSmoothstepDerivative * d2x;
        float dY = d1y + ySmoothstepDerivative * d2y;
        float dZ = d1z + zSmoothstepDerivative * d2z;

        derivativeOut[0] += dX;
        derivativeOut[1] += dY;
        derivativeOut[2] += dZ;

        return Mth.Lerp3(
            xAlpha, yAlpha, zAlpha,
            d000, d100, d010, d110,
            d001, d101, d011, d111);
    }

    public void ParityConfigString(StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        NoiseUtils.ParityNoiseOctaveConfigString(builder, OffsetX, OffsetY, OffsetZ, Permutations);
    }
}
