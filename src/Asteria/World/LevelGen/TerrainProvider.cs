using Asteria.Core;
using Asteria.Util;

namespace Asteria.World.LevelGen;

/// <summary>
/// Vanilla Minecraft Java 26.3 overworld terrain spline construction.
/// Normal and amplified terrain share the same spline topology; amplification
/// is applied only through the source-defined value transforms.
/// </summary>
public static class TerrainProvider
{
    private const float DeepOceanContinentalness = -0.51f;
    private const float BeachContinentalness = -0.15f;

    private static readonly Func<float, float> NoTransform = static value => value;
    private static readonly Func<float, float> AmplifiedOffset =
        static offset => offset < 0.0f ? offset : offset * 2.0f;
    private static readonly Func<float, float> AmplifiedFactor =
        static factor => 1.25f - 6.25f / (factor + 5.0f);
    private static readonly Func<float, float> AmplifiedJaggedness =
        static jaggedness => jaggedness * 2.0f;

    public static CubicSpline<TContext> OverworldOffset<TContext>(
        IBoundedFloatFunction<TContext> continents,
        IBoundedFloatFunction<TContext> erosion,
        IBoundedFloatFunction<TContext> ridges,
        bool amplified)
    {
        Func<float, float> offsetTransformer = amplified ? AmplifiedOffset : NoTransform;
        CubicSpline<TContext> beachSpline = BuildErosionOffsetSpline(
            erosion, ridges, -0.15f, 0.0f, 0.0f, 0.1f, 0.0f, -0.03f,
            includeExtremeHills: false, saddle: false, offsetTransformer);
        CubicSpline<TContext> lowSpline = BuildErosionOffsetSpline(
            erosion, ridges, -0.1f, 0.03f, 0.1f, 0.1f, 0.01f, -0.03f,
            includeExtremeHills: false, saddle: false, offsetTransformer);
        CubicSpline<TContext> midSpline = BuildErosionOffsetSpline(
            erosion, ridges, -0.1f, 0.03f, 0.1f, 0.7f, 0.01f, -0.03f,
            includeExtremeHills: true, saddle: true, offsetTransformer);
        CubicSpline<TContext> highSpline = BuildErosionOffsetSpline(
            erosion, ridges, -0.05f, 0.03f, 0.1f, 1.0f, 0.01f, 0.01f,
            includeExtremeHills: true, saddle: true, offsetTransformer);

        return CubicSpline<TContext>.CreateBuilder(continents, offsetTransformer)
            .AddPoint(-1.1f, 0.044f)
            .AddPoint(-1.02f, -0.2222f)
            .AddPoint(DeepOceanContinentalness, -0.2222f)
            .AddPoint(-0.44f, -0.12f)
            .AddPoint(-0.18f, -0.12f)
            .AddPoint(-0.16f, beachSpline)
            .AddPoint(BeachContinentalness, beachSpline)
            .AddPoint(-0.1f, lowSpline)
            .AddPoint(0.25f, midSpline)
            .AddPoint(1.0f, highSpline)
            .Build();
    }

    public static CubicSpline<TContext> OverworldFactor<TContext>(
        IBoundedFloatFunction<TContext> continents,
        IBoundedFloatFunction<TContext> erosion,
        IBoundedFloatFunction<TContext> weirdness,
        IBoundedFloatFunction<TContext> ridges,
        bool amplified)
    {
        Func<float, float> factorTransformer = amplified ? AmplifiedFactor : NoTransform;
        return CubicSpline<TContext>.CreateBuilder(continents, NoTransform)
            .AddPoint(-0.19f, 3.95f)
            .AddPoint(-0.15f, GetErosionFactor(erosion, weirdness, ridges, 6.25f, true, NoTransform))
            .AddPoint(-0.1f, GetErosionFactor(erosion, weirdness, ridges, 5.47f, true, factorTransformer))
            .AddPoint(0.03f, GetErosionFactor(erosion, weirdness, ridges, 5.08f, true, factorTransformer))
            .AddPoint(0.06f, GetErosionFactor(erosion, weirdness, ridges, 4.69f, false, factorTransformer))
            .Build();
    }

    public static CubicSpline<TContext> OverworldJaggedness<TContext>(
        IBoundedFloatFunction<TContext> continents,
        IBoundedFloatFunction<TContext> erosion,
        IBoundedFloatFunction<TContext> weirdness,
        IBoundedFloatFunction<TContext> ridges,
        bool amplified)
    {
        Func<float, float> jaggednessTransformer = amplified ? AmplifiedJaggedness : NoTransform;
        const float farInlandMiddle = 0.65f;
        return CubicSpline<TContext>.CreateBuilder(continents, jaggednessTransformer)
            .AddPoint(-0.11f, 0.0f)
            .AddPoint(0.03f, BuildErosionJaggednessSpline(
                erosion, weirdness, ridges, 1.0f, 0.5f, 0.0f, 0.0f, jaggednessTransformer))
            .AddPoint(farInlandMiddle, BuildErosionJaggednessSpline(
                erosion, weirdness, ridges, 1.0f, 1.0f, 1.0f, 0.0f, jaggednessTransformer))
            .Build();
    }

    public static float PeaksAndValleys(float weirdness) =>
        -(MathF.Abs(MathF.Abs(weirdness) - 0.6666667f) - 0.33333334f) * 3.0f;

    private static CubicSpline<TContext> BuildErosionJaggednessSpline<TContext>(
        IBoundedFloatFunction<TContext> erosion,
        IBoundedFloatFunction<TContext> weirdness,
        IBoundedFloatFunction<TContext> ridges,
        float jaggednessFactorAtPeakRidgeAndErosionIndex0,
        float jaggednessFactorAtPeakRidgeAndErosionIndex1,
        float jaggednessFactorAtHighRidgeAndErosionIndex0,
        float jaggednessFactorAtHighRidgeAndErosionIndex1,
        Func<float, float> jaggednessTransformer)
    {
        const float erosionIndex1Middle = -0.5775f;
        CubicSpline<TContext> ridgeJaggednessSplineAtErosion0 = BuildRidgeJaggednessSpline(
            weirdness, ridges,
            jaggednessFactorAtPeakRidgeAndErosionIndex0,
            jaggednessFactorAtHighRidgeAndErosionIndex0,
            jaggednessTransformer);
        CubicSpline<TContext> ridgeJaggednessSplineAtErosion1 = BuildRidgeJaggednessSpline(
            weirdness, ridges,
            jaggednessFactorAtPeakRidgeAndErosionIndex1,
            jaggednessFactorAtHighRidgeAndErosionIndex1,
            jaggednessTransformer);

        return CubicSpline<TContext>.CreateBuilder(erosion, jaggednessTransformer)
            .AddPoint(-1.0f, ridgeJaggednessSplineAtErosion0)
            .AddPoint(-0.78f, ridgeJaggednessSplineAtErosion1)
            .AddPoint(erosionIndex1Middle, ridgeJaggednessSplineAtErosion1)
            .AddPoint(-0.375f, 0.0f)
            .Build();
    }

    private static CubicSpline<TContext> BuildRidgeJaggednessSpline<TContext>(
        IBoundedFloatFunction<TContext> weirdness,
        IBoundedFloatFunction<TContext> ridges,
        float jaggednessFactorAtPeakRidge,
        float jaggednessFactorAtHighRidge,
        Func<float, float> jaggednessTransformer)
    {
        float highSliceStart = PeaksAndValleys(0.4f);
        float highSliceEnd = PeaksAndValleys(0.56666666f);
        float highSliceMiddle = (highSliceStart + highSliceEnd) / 2.0f;
        CubicSpline<TContext>.Builder ridgeSpline =
            CubicSpline<TContext>.CreateBuilder(ridges, jaggednessTransformer);
        ridgeSpline.AddPoint(highSliceStart, 0.0f);
        ridgeSpline.AddPoint(
            highSliceMiddle,
            jaggednessFactorAtHighRidge > 0.0f
                ? BuildWeirdnessJaggednessSpline(weirdness, jaggednessFactorAtHighRidge, jaggednessTransformer)
                : CubicSpline<TContext>.CreateConstant(0.0f));
        ridgeSpline.AddPoint(
            1.0f,
            jaggednessFactorAtPeakRidge > 0.0f
                ? BuildWeirdnessJaggednessSpline(weirdness, jaggednessFactorAtPeakRidge, jaggednessTransformer)
                : CubicSpline<TContext>.CreateConstant(0.0f));
        return ridgeSpline.Build();
    }

    private static CubicSpline<TContext> BuildWeirdnessJaggednessSpline<TContext>(
        IBoundedFloatFunction<TContext> weirdness,
        float jaggednessFactor,
        Func<float, float> jaggednessTransformer)
    {
        float maxJaggednessAtNegativeWeirdness = 0.63f * jaggednessFactor;
        float maxJaggednessAtPositiveWeirdness = 0.3f * jaggednessFactor;
        return CubicSpline<TContext>.CreateBuilder(weirdness, jaggednessTransformer)
            .AddPoint(-0.01f, maxJaggednessAtNegativeWeirdness)
            .AddPoint(0.01f, maxJaggednessAtPositiveWeirdness)
            .Build();
    }

    private static CubicSpline<TContext> GetErosionFactor<TContext>(
        IBoundedFloatFunction<TContext> erosion,
        IBoundedFloatFunction<TContext> weirdness,
        IBoundedFloatFunction<TContext> ridges,
        float baseValue,
        bool shatteredTerrain,
        Func<float, float> factorTransformer)
    {
        CubicSpline<TContext> baseSpline = CubicSpline<TContext>.CreateBuilder(weirdness, factorTransformer)
            .AddPoint(-0.2f, 6.3f)
            .AddPoint(0.2f, baseValue)
            .Build();
        CubicSpline<TContext>.Builder erosionPoints = CubicSpline<TContext>.CreateBuilder(erosion, factorTransformer)
            .AddPoint(-0.6f, baseSpline)
            .AddPoint(-0.5f, CubicSpline<TContext>.CreateBuilder(weirdness, factorTransformer)
                .AddPoint(-0.05f, 6.3f).AddPoint(0.05f, 2.67f).Build())
            .AddPoint(-0.35f, baseSpline)
            .AddPoint(-0.25f, baseSpline)
            .AddPoint(-0.1f, CubicSpline<TContext>.CreateBuilder(weirdness, factorTransformer)
                .AddPoint(-0.05f, 2.67f).AddPoint(0.05f, 6.3f).Build())
            .AddPoint(0.03f, baseSpline);

        if (shatteredTerrain)
        {
            CubicSpline<TContext> weirdnessShattered = CubicSpline<TContext>.CreateBuilder(weirdness, factorTransformer)
                .AddPoint(0.0f, baseValue).AddPoint(0.1f, 0.625f).Build();
            CubicSpline<TContext> ridgesShattered = CubicSpline<TContext>.CreateBuilder(ridges, factorTransformer)
                .AddPoint(-0.9f, baseValue).AddPoint(-0.69f, weirdnessShattered).Build();
            erosionPoints.AddPoint(0.35f, baseValue)
                .AddPoint(0.45f, ridgesShattered)
                .AddPoint(0.55f, ridgesShattered)
                .AddPoint(0.62f, baseValue);
        }
        else
        {
            CubicSpline<TContext> extremeHillsTerrainFromMidSliceAndUp =
                CubicSpline<TContext>.CreateBuilder(ridges, factorTransformer)
                    .AddPoint(-0.7f, baseSpline).AddPoint(-0.15f, 1.37f).Build();
            CubicSpline<TContext> extra3dNoiseOnPeaksOnly =
                CubicSpline<TContext>.CreateBuilder(ridges, factorTransformer)
                    .AddPoint(0.45f, baseSpline).AddPoint(0.7f, 1.56f).Build();
            erosionPoints.AddPoint(0.05f, extra3dNoiseOnPeaksOnly)
                .AddPoint(0.4f, extra3dNoiseOnPeaksOnly)
                .AddPoint(0.45f, extremeHillsTerrainFromMidSliceAndUp)
                .AddPoint(0.55f, extremeHillsTerrainFromMidSliceAndUp)
                .AddPoint(0.58f, baseValue);
        }

        return erosionPoints.Build();
    }

    private static float CalculateSlope(float y1, float y2, float x1, float x2) =>
        (y2 - y1) / (x2 - x1);

    private static CubicSpline<TContext> BuildMountainRidgeSplineWithPoints<TContext>(
        IBoundedFloatFunction<TContext> ridges,
        float modulation,
        bool saddle,
        Func<float, float> offsetTransformer)
    {
        CubicSpline<TContext>.Builder build = CubicSpline<TContext>.CreateBuilder(ridges, offsetTransformer);
        const float allowRiversBelow = -0.7f;
        const float minPoint = -1.0f;
        float minPointContinentalness = MountainContinentalness(minPoint, modulation, allowRiversBelow);
        const float maxPoint = 1.0f;
        float maxPointContinentalness = MountainContinentalness(maxPoint, modulation, allowRiversBelow);
        float ridgeZeroPoint = CalculateMountainRidgeZeroContinentalnessPoint(modulation);
        const float afterRiverPoint = -0.65f;

        if (afterRiverPoint < ridgeZeroPoint && ridgeZeroPoint < maxPoint)
        {
            float afterRiverThresholdContinentalness = MountainContinentalness(afterRiverPoint, modulation, allowRiversBelow);
            const float beforeRiverPoint = -0.75f;
            float beforeRiverThresholdContinentalness = MountainContinentalness(beforeRiverPoint, modulation, allowRiversBelow);
            float minPointDerivative = CalculateSlope(
                minPointContinentalness, beforeRiverThresholdContinentalness, minPoint, beforeRiverPoint);
            build.AddPoint(minPoint, minPointContinentalness, minPointDerivative);
            build.AddPoint(beforeRiverPoint, beforeRiverThresholdContinentalness);
            build.AddPoint(afterRiverPoint, afterRiverThresholdContinentalness);
            float ridgeZeroPointContinentalness = MountainContinentalness(ridgeZeroPoint, modulation, allowRiversBelow);
            float maxPointDerivative = CalculateSlope(
                ridgeZeroPointContinentalness, maxPointContinentalness, ridgeZeroPoint, maxPoint);
            build.AddPoint(ridgeZeroPoint - 0.01f, ridgeZeroPointContinentalness);
            build.AddPoint(ridgeZeroPoint, ridgeZeroPointContinentalness, maxPointDerivative);
            build.AddPoint(maxPoint, maxPointContinentalness, maxPointDerivative);
        }
        else
        {
            float simpleDerivative = CalculateSlope(
                minPointContinentalness, maxPointContinentalness, minPoint, maxPoint);
            if (saddle)
            {
                build.AddPoint(minPoint, Math.Max(0.2f, minPointContinentalness));
                build.AddPoint(0.0f, Mth.Lerp(0.5f, minPointContinentalness, maxPointContinentalness), simpleDerivative);
            }
            else
            {
                build.AddPoint(minPoint, minPointContinentalness, simpleDerivative);
            }

            build.AddPoint(maxPoint, maxPointContinentalness, simpleDerivative);
        }

        return build.Build();
    }

    private static float MountainContinentalness(float ridge, float modulation, float allowRiversBelow)
    {
        float ridgeSlope = 1.0f - (1.0f - modulation) * 0.5f;
        float ridgeIntersect = 0.5f * (1.0f - modulation);
        float adjustedRidgeHeight = (ridge + 1.17f) * 0.46082947f;
        float continentalness = adjustedRidgeHeight * ridgeSlope - ridgeIntersect;
        return ridge < allowRiversBelow
            ? Math.Max(continentalness, -0.2222f)
            : Math.Max(continentalness, 0.0f);
    }

    private static float CalculateMountainRidgeZeroContinentalnessPoint(float modulation)
    {
        float ridgeSlope = 1.0f - (1.0f - modulation) * 0.5f;
        float ridgeIntersect = 0.5f * (1.0f - modulation);
        return ridgeIntersect / (0.46082947f * ridgeSlope) - 1.17f;
    }

    public static CubicSpline<TContext> BuildErosionOffsetSpline<TContext>(
        IBoundedFloatFunction<TContext> erosion,
        IBoundedFloatFunction<TContext> ridges,
        float lowValley,
        float hill,
        float tallHill,
        float mountainFactor,
        float plain,
        float swamp,
        bool includeExtremeHills,
        bool saddle,
        Func<float, float> offsetTransformer)
    {
        CubicSpline<TContext> veryLowErosionMountains = BuildMountainRidgeSplineWithPoints(
            ridges, Mth.Lerp(mountainFactor, 0.6f, 1.5f), saddle, offsetTransformer);
        CubicSpline<TContext> lowErosionMountains = BuildMountainRidgeSplineWithPoints(
            ridges, Mth.Lerp(mountainFactor, 0.6f, 1.0f), saddle, offsetTransformer);
        CubicSpline<TContext> mountains = BuildMountainRidgeSplineWithPoints(
            ridges, mountainFactor, saddle, offsetTransformer);
        CubicSpline<TContext> widePlateau = RidgeSpline(
            ridges, lowValley - 0.15f, 0.5f * mountainFactor,
            Mth.Lerp(0.5f, 0.5f, 0.5f) * mountainFactor,
            0.5f * mountainFactor, 0.6f * mountainFactor, 0.5f, offsetTransformer);
        CubicSpline<TContext> narrowPlateau = RidgeSpline(
            ridges, lowValley, plain * mountainFactor, hill * mountainFactor,
            0.5f * mountainFactor, 0.6f * mountainFactor, 0.5f, offsetTransformer);
        CubicSpline<TContext> plains = RidgeSpline(
            ridges, lowValley, plain, plain, hill, tallHill, 0.5f, offsetTransformer);
        CubicSpline<TContext> plainsFarInland = RidgeSpline(
            ridges, lowValley, plain, plain, hill, tallHill, 0.5f, offsetTransformer);
        CubicSpline<TContext> extremeHills = CubicSpline<TContext>.CreateBuilder(ridges, offsetTransformer)
            .AddPoint(-1.0f, lowValley)
            .AddPoint(-0.4f, plains)
            .AddPoint(0.0f, tallHill + 0.07f)
            .Build();
        CubicSpline<TContext> swamps = RidgeSpline(
            ridges, -0.02f, swamp, swamp, hill, tallHill, 0.0f, offsetTransformer);

        CubicSpline<TContext>.Builder builder = CubicSpline<TContext>.CreateBuilder(erosion, offsetTransformer)
            .AddPoint(-0.85f, veryLowErosionMountains)
            .AddPoint(-0.7f, lowErosionMountains)
            .AddPoint(-0.4f, mountains)
            .AddPoint(-0.35f, widePlateau)
            .AddPoint(-0.1f, narrowPlateau)
            .AddPoint(0.2f, plains);

        if (includeExtremeHills)
        {
            builder.AddPoint(0.4f, plainsFarInland)
                .AddPoint(0.45f, extremeHills)
                .AddPoint(0.55f, extremeHills)
                .AddPoint(0.58f, plainsFarInland);
        }

        builder.AddPoint(0.7f, swamps);
        return builder.Build();
    }

    private static CubicSpline<TContext> RidgeSpline<TContext>(
        IBoundedFloatFunction<TContext> ridges,
        float valley,
        float low,
        float mid,
        float high,
        float peaks,
        float minValleySteepness,
        Func<float, float> offsetTransformer)
    {
        float d1 = Math.Max(0.5f * (low - valley), minValleySteepness);
        float d2 = 5.0f * (mid - low);
        return CubicSpline<TContext>.CreateBuilder(ridges, offsetTransformer)
            .AddPoint(-1.0f, valley, d1)
            .AddPoint(-0.4f, low, Math.Min(d1, d2))
            .AddPoint(0.0f, mid, d2)
            .AddPoint(0.4f, high, 2.0f * (high - mid))
            .AddPoint(1.0f, peaks, 0.7f * (peaks - high))
            .Build();
    }
}
