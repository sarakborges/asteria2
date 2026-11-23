using System.Text;
using System.Globalization;
using Asteria.Core;
using Asteria.Util;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.Synth;

internal static class PerlinNoiseChecks
{
    public static void Run()
    {
        var noise = new PerlinNoise(new WorldgenRandom(new LegacyRandomSource(1234L)));
        Require(noise.GetRange().Equals(Interval.OfSymmetric(2.0f)),
            "PerlinNoise.range must return the symmetric [-2,2] interval.");
        Require(PerlinNoise.STANDARD_DEVIATION == 0.2702247831245211,
            "PerlinNoise standard deviation must preserve the Minecraft 26.3 constant.");

        Require(Math.Abs(noise.Get(0.0, 0.0, 0.0) - 0.2845192849636078f) < 0.000001f,
            "PerlinNoise.get must match the fixed-seed 26.3 origin sample.");
        Require(Math.Abs(noise.Get(1.0, 2.0, 3.0) - -0.4103383421897888f) < 0.000001f,
            "PerlinNoise.get must match a fixed-seed positive-coordinate sample.");
        Require(Math.Abs(noise.Get(-17.25, 82.5, 35.75) - 0.26501667499542236f) < 0.000001f,
            "PerlinNoise.get must preserve negative, high-altitude and positive coordinates.");
        Require(Math.Abs(noise.Get(0.25, -0.75, 1.5) - -0.07437247037887573f) < 0.000001f,
            "PerlinNoise.get must preserve mixed-sign coordinate sampling.");
        Require(Math.Abs(noise.Get(2.1, 3.7, -4.2) - -0.015059009194374084f) < 0.000001f,
            "PerlinNoise.get must match a further fixed-seed lattice cell.");

        Require(Math.Abs(noise.Get(0.0, 0.0) - 0.2845192849636078f) < 0.000001f &&
            Math.Abs(noise.Get(1.0, 2.0) - 0.10460613667964935f) < 0.000001f &&
            Math.Abs(noise.Get(-17.25, 35.75) - 0.3956707715988159f) < 0.000001f,
            "The two-coordinate overload must map to the XZ plane.");

        float[] derivative = { 1.0f, 2.0f, 3.0f };
        float derivativeValue = noise.NoiseWithDerivative(0.0, 0.0, 0.0, derivative);
        Require(Math.Abs(derivativeValue - 0.2845192849636078f) < 0.000001f &&
            Math.Abs(derivative[0] - 0.5280973315238953f) < 0.000001f &&
            Math.Abs(derivative[1] - 2.7191601991653442f) < 0.000001f &&
            Math.Abs(derivative[2] - 3.2207374572753906f) < 0.000001f,
            "PerlinNoise.noiseWithDerivative must add the interpolated gradient to the supplied derivative accumulator.");

        var volume = new DensityVolume(2, 3, 2, -2, 1, 5);
        DensityBuffer buffer = DensityBuffer.CreateUnpooled(volume.Size());
        noise.AddToVolume(buffer, volume, xzScale: 0.125, yScale: 0.25, amplitude: 0.75f);
        float[] expectedVolume =
        {
            0.11675719916820526f,
            0.055512428283691406f,
            0.07993258535861969f,
            0.2020893394947052f,
            0.11878383159637451f,
            0.1025090366601944f,
            0.01059279777109623f,
            -0.07575005292892456f,
            -0.07599866390228271f,
            0.11092711240053177f,
            0.009321615099906921f,
            -0.026245739310979843f
        };
        for (int index = 0; index < expectedVolume.Length; index++)
        {
            Require(Math.Abs(buffer.Get(index) - expectedVolume[index]) < 0.000001f,
                $"PerlinNoise.addToVolume must preserve the optimized 26.3 volume result at index {index}.");
        }

        var accumulated = DensityBuffer.CreateUnpooled(volume.Size());
        accumulated.Fill(0.25f);
        noise.AddToVolume(accumulated, volume, xzScale: 0.125, yScale: 0.25, amplitude: 0.75f);
        for (int index = 0; index < expectedVolume.Length; index++)
        {
            Require(Math.Abs(accumulated.Get(index) - (expectedVolume[index] + 0.25f)) < 0.000001f,
                "PerlinNoise.addToVolume must accumulate into an existing density buffer.");
        }

        var parity = new StringBuilder();
        noise.ParityConfigString(parity);
        Require(parity.ToString() == "xo=165.525, yo=243.548, zo=219.543, p0=117, p255=118",
            "PerlinNoise.parityConfigString must expose the vanilla rounded offsets and permutation endpoints.");

        Require(Math.Abs(Mth.Smoothstep(0.5f) - 0.5f) < 0.000001f &&
            Math.Abs(Mth.SmoothstepDerivative(0.5f) - 1.875f) < 0.000001f,
            "Mth float smoothstep helpers must use the 26.3 quintic interpolation curve.");
        Require(Mth.Lerp3(
                0.5f, 0.5f, 0.5f,
                0.0f, 1.0f, 2.0f, 3.0f,
                4.0f, 5.0f, 6.0f, 7.0f) == 3.5f,
            "Mth float lerp3 must interpolate in X, then Y, then Z order.");

        RequireThrows<ArgumentNullException>(
            () => noise.NoiseWithDerivative(0, 0, 0, null!),
            "PerlinNoise.noiseWithDerivative must reject a null derivative destination.");
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
