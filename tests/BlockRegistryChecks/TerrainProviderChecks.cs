using Asteria.Util;
using Asteria.World.LevelGen;

internal static class TerrainProviderChecks
{
    private readonly record struct Sample(float Continents, float Erosion, float Weirdness, float Ridges);

    private sealed class Coordinate(Func<Sample, float> selector) : IBoundedFloatFunction<Sample>
    {
        public float Apply(Sample context) => selector(context);
        public Interval Range() => Interval.Of(-2.0f, 2.0f);
    }

    private static readonly IBoundedFloatFunction<Sample> Continents = new Coordinate(static value => value.Continents);
    private static readonly IBoundedFloatFunction<Sample> Erosion = new Coordinate(static value => value.Erosion);
    private static readonly IBoundedFloatFunction<Sample> Weirdness = new Coordinate(static value => value.Weirdness);
    private static readonly IBoundedFloatFunction<Sample> Ridges = new Coordinate(static value => value.Ridges);

    public static void Run()
    {
        CheckSharedSplineTopologyAndAmplification();
        CheckPeaksAndValleys();
        CheckErosionOffsetSpline();
    }

    private static void CheckSharedSplineTopologyAndAmplification()
    {
        CubicSpline<Sample> normalOffset = TerrainProvider.OverworldOffset(Continents, Erosion, Ridges, amplified: false);
        CubicSpline<Sample> amplifiedOffset = TerrainProvider.OverworldOffset(Continents, Erosion, Ridges, amplified: true);
        Sample positiveOffset = new(-1.1f, 0.0f, 0.0f, 0.0f);
        Sample negativeOffset = new(-1.02f, 0.0f, 0.0f, 0.0f);

        Require(
            MathF.Abs(CubicSpline<Sample>.Sample(normalOffset, positiveOffset) - 0.044f) < 0.00001f &&
            MathF.Abs(CubicSpline<Sample>.Sample(amplifiedOffset, positiveOffset) - 0.088f) < 0.00001f,
            "Normal and amplified offset splines must share topology while amplified mode doubles only non-negative offsets.");
        Require(
            MathF.Abs(CubicSpline<Sample>.Sample(normalOffset, negativeOffset) -
                CubicSpline<Sample>.Sample(amplifiedOffset, negativeOffset)) < 0.00001f,
            "Amplified offset transformation must leave negative offsets unchanged.");

        CubicSpline<Sample> normalFactor = TerrainProvider.OverworldFactor(
            Continents, Erosion, Weirdness, Ridges, amplified: false);
        CubicSpline<Sample> amplifiedFactor = TerrainProvider.OverworldFactor(
            Continents, Erosion, Weirdness, Ridges, amplified: true);
        Sample factorSample = new(-0.1f, -0.8f, -0.2f, 0.0f);
        float normalFactorValue = CubicSpline<Sample>.Sample(normalFactor, factorSample);
        float amplifiedFactorValue = CubicSpline<Sample>.Sample(amplifiedFactor, factorSample);
        Require(
            MathF.Abs(normalFactorValue - 6.3f) < 0.0001f &&
            MathF.Abs(amplifiedFactorValue - (1.25f - 6.25f / 11.3f)) < 0.0001f,
            "Amplified factor must use the vanilla nonlinear transform without duplicating the spline definition.");

        CubicSpline<Sample> normalJaggedness = TerrainProvider.OverworldJaggedness(
            Continents, Erosion, Weirdness, Ridges, amplified: false);
        CubicSpline<Sample> amplifiedJaggedness = TerrainProvider.OverworldJaggedness(
            Continents, Erosion, Weirdness, Ridges, amplified: true);
        Sample jaggednessSample = new(0.65f, -1.0f, -0.01f, 1.0f);
        float normalJaggednessValue = CubicSpline<Sample>.Sample(normalJaggedness, jaggednessSample);
        float amplifiedJaggednessValue = CubicSpline<Sample>.Sample(amplifiedJaggedness, jaggednessSample);
        Require(
            MathF.Abs(normalJaggednessValue - 0.63f) < 0.0001f &&
            MathF.Abs(amplifiedJaggednessValue - 1.26f) < 0.0001f,
            "Amplified jaggedness must double the vanilla jaggedness values through the shared spline topology.");
    }

    private static void CheckPeaksAndValleys()
    {
        Require(
            MathF.Abs(TerrainProvider.PeaksAndValleys(0.0f) + 1.0f) < 0.00001f &&
            MathF.Abs(TerrainProvider.PeaksAndValleys(0.33333334f)) < 0.00001f &&
            MathF.Abs(TerrainProvider.PeaksAndValleys(-0.33333334f)) < 0.00001f,
            "Peaks-and-valleys must preserve the vanilla function's center and ridge zero crossings.");
    }

    private static void CheckErosionOffsetSpline()
    {
        CubicSpline<Sample> spline = TerrainProvider.BuildErosionOffsetSpline(
            Erosion, Ridges,
            lowValley: -0.1f,
            hill: 0.03f,
            tallHill: 0.1f,
            mountainFactor: 0.7f,
            plain: 0.01f,
            swamp: -0.03f,
            includeExtremeHills: true,
            saddle: true,
            offsetTransformer: static value => value);
        Require(
            spline is CubicSpline<Sample>.Multipoint multipoint &&
            multipoint.Locations.SequenceEqual(new[] { -0.85f, -0.7f, -0.4f, -0.35f, -0.1f, 0.2f, 0.4f, 0.45f, 0.55f, 0.58f, 0.7f }),
            "The erosion offset spline must preserve the exact vanilla erosion control points when extreme hills are enabled.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
