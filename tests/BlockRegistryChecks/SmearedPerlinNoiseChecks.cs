using Asteria.Util;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.Synth;

internal static class SmearedPerlinNoiseChecks
{
    public static void Run()
    {
        CheckRange();
        CheckVerticalSmear();
        CheckVolumeParity();
    }

    private static void CheckRange()
    {
        Require(
            SmearedPerlinNoise.Range(0.5).Equals(Interval.OfSymmetric(2.5f)) &&
            SmearedPerlinNoise.Range(-1.25).Equals(Interval.OfSymmetric(3.25f)),
            "SmearedPerlinNoise.range must use abs(fudgeYScale)+2.");

        var noise = new SmearedPerlinNoise(new LegacyRandomSource(1234L), 0.75);
        Require(
            noise.GetRange().Equals(Interval.OfSymmetric(2.75f)),
            "SmearedPerlinNoise instance range must preserve its vertical smear scale.");
    }

    private static void CheckVerticalSmear()
    {
        const long seed = 0x4A17_22B3L;
        var normal = new PerlinNoise(new LegacyRandomSource(seed));
        var smeared = new SmearedPerlinNoise(new LegacyRandomSource(seed), 0.5);

        float normalValue = normal.Get(12.25, 4.75, -9.5);
        float smearedValue = smeared.Get(12.25, 4.75, -9.5);
        Require(
            normalValue != smearedValue,
            "SmearedPerlinNoise must alter the Y gradient coordinate using the vertical fudge scale.");

        float negativeYValue = smeared.Get(-12.25, -4.75, 9.5);
        Require(
            float.IsFinite(negativeYValue),
            "SmearedPerlinNoise must retain finite samples for negative coordinates.");

        Require(
            smeared.Get(12.25, 4.75, -9.5) == smeared.Get(12.25, 4.75, -9.5),
            "SmearedPerlinNoise must be deterministic for a fixed permutation table and coordinates.");
    }

    private static void CheckVolumeParity()
    {
        const double xzScale = 0.375;
        const double yScale = 0.625;
        const float amplitude = 1.25f;
        var noise = new SmearedPerlinNoise(
            new XoroshiroRandomSource(0x1234_5678_9ABC_DEF0L),
            0.75);

        var volume = new DensityVolume(
            3,
            4,
            2,
            -7,
            -9,
            13,
            2,
            3,
            5);
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
        output.Fill(0.0f);
        noise.AddToVolume(output, volume, xzScale, yScale, amplitude);

        for (int z = 0; z < volume.SizeZ; z++)
        {
            for (int x = 0; x < volume.SizeX; x++)
            {
                for (int y = 0; y < volume.SizeY; y++)
                {
                    float expected = amplitude * noise.Get(
                        volume.BlockX(x) * xzScale,
                        volume.BlockY(y) * yScale,
                        volume.BlockZ(z) * xzScale);
                    float actual = output.Get(volume.IndexUnchecked(x, y, z));
                    Require(
                        MathF.Abs(actual - expected) <= 0.00002f,
                        "SmearedPerlinNoise.addToVolume must match point sampling with the same scaled block coordinates.");
                }
            }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
