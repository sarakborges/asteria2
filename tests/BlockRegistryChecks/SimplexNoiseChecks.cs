using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Synth;

internal static class SimplexNoiseChecks
{
    private sealed class ExposedGradientNoise : GradientNoise
    {
        public ExposedGradientNoise()
            : base(new WorldgenRandom(new LegacyRandomSource(1234L)), 0.0)
        {
        }

        public static double WrapCoordinate(double value) => Wrap(value);

        public (int X, int Y, int Z) GetGradient(int value)
        {
            Gradient gradient = PermuteToGrad(value);
            return (gradient.X, gradient.Y, gradient.Z);
        }
    }

    public static void Run()
    {
        var noise = new SimplexNoise(
            new WorldgenRandom(new LegacyRandomSource(1234L)),
            discardNoiseOffset: false);

        Require(noise.GetRange().Equals(Asteria.Util.Interval.OfSymmetric(2.0f)),
            "SimplexNoise.range must return the symmetric [-2,2] interval.");
        Require(SimplexNoise.STANDARD_DEVIATION == 0.42544,
            "SimplexNoise standard deviation must preserve the Minecraft 26.3 constant.");

        Require(Math.Abs(noise.Get(0.0, 0.0, 0.0) - 0.070777265f) < 0.000001f,
            "Three-dimensional simplex noise must match the 26.3 corner ordering and attenuation at the origin.");
        Require(Math.Abs(noise.Get(1.0, 2.0, 3.0) - -0.27196983f) < 0.000001f,
            "Three-dimensional simplex noise must match the 26.3 rank ordering in the first tetrahedral branch.");
        Require(Math.Abs(noise.Get(-17.25, 82.5, 35.75) - 0.7247156f) < 0.000001f,
            "Three-dimensional simplex noise must remain stable for negative and elevated world coordinates.");
        Require(Math.Abs(noise.Get(0.25, -0.75, 1.5) - -0.5176961f) < 0.000001f,
            "Three-dimensional simplex noise must preserve mixed-sign coordinate behavior.");
        Require(Math.Abs(noise.Get(2.1, 3.7, -4.2) - 0.22836201f) < 0.000001f,
            "Three-dimensional simplex noise must cover the remaining tetrahedral rank branch.");

        double halfRoundOff = Math.BitDecrement(16777216.0);
        Require(ExposedGradientNoise.WrapCoordinate(halfRoundOff - 1.0) == halfRoundOff - 1.0 &&
            ExposedGradientNoise.WrapCoordinate(-halfRoundOff) == -halfRoundOff,
            "GradientNoise.wrap must preserve coordinates within the reference half-range.");
        Require(ExposedGradientNoise.WrapCoordinate(16777216.0) == -16777216.0 &&
            ExposedGradientNoise.WrapCoordinate(-16777216.0 - 1.0) == 16777215.0,
            "GradientNoise.wrap must fold coordinates at the 2^24 threshold into the 2^25 period.");

        Require(ExposedGradientNoise.WrapCoordinate(0.0) == 0.0,
            "GradientNoise.wrap must leave zero unchanged.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
