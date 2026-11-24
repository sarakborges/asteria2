using System.Globalization;
using Asteria.Content.Biomes;

namespace Asteria.World.LevelGen;

/// <summary>
/// Shared vanilla Overworld climate ranges and diagnostic classification from Minecraft Java 26.3.
/// Biome parameter emission is kept as the next layer so this class never invents registry entries.
/// </summary>
public sealed class OverworldBiomeBuilder
{
    public const float ValleySize = 0.05F;
    public const float LowStart = 0.26666668F;
    public const float HighStart = 0.4F;
    public const float HighEnd = 0.93333334F;
    public const float PeakSize = 0.1F;
    public const float PeakStart = 0.56666666F;
    public const float PeakEnd = 0.7666667F;
    public const float NearInlandStart = -0.11F;
    public const float MidInlandStart = 0.03F;
    public const float FarInlandStart = 0.3F;
    public const float ErosionIndex1Start = -0.78F;
    public const float ErosionIndex2Start = -0.375F;

    private static readonly Climate.Parameter FullRange = Climate.Parameter.Span(-1.0F, 1.0F);

    private readonly Climate.Parameter[] _temperatures =
    [
        Climate.Parameter.Span(-1.0F, -0.45F),
        Climate.Parameter.Span(-0.45F, -0.15F),
        Climate.Parameter.Span(-0.15F, 0.2F),
        Climate.Parameter.Span(0.2F, 0.55F),
        Climate.Parameter.Span(0.55F, 1.0F)
    ];

    private readonly Climate.Parameter[] _humidities =
    [
        Climate.Parameter.Span(-1.0F, -0.35F),
        Climate.Parameter.Span(-0.35F, -0.1F),
        Climate.Parameter.Span(-0.1F, 0.1F),
        Climate.Parameter.Span(0.1F, 0.3F),
        Climate.Parameter.Span(0.3F, 1.0F)
    ];

    private readonly Climate.Parameter[] _erosions =
    [
        Climate.Parameter.Span(-1.0F, -0.78F),
        Climate.Parameter.Span(-0.78F, -0.375F),
        Climate.Parameter.Span(-0.375F, -0.2225F),
        Climate.Parameter.Span(-0.2225F, 0.05F),
        Climate.Parameter.Span(0.05F, 0.45F),
        Climate.Parameter.Span(0.45F, 0.55F),
        Climate.Parameter.Span(0.55F, 1.0F)
    ];

    private readonly Climate.Parameter _mushroomFieldsContinentalness =
        Climate.Parameter.Span(-1.2F, -1.05F);
    private readonly Climate.Parameter _deepOceanContinentalness =
        Climate.Parameter.Span(-1.05F, -0.455F);
    private readonly Climate.Parameter _oceanContinentalness =
        Climate.Parameter.Span(-0.455F, -0.19F);
    private readonly Climate.Parameter _coastContinentalness =
        Climate.Parameter.Span(-0.19F, -0.11F);
    private readonly Climate.Parameter _inlandContinentalness =
        Climate.Parameter.Span(-0.11F, 0.55F);
    private readonly Climate.Parameter _nearInlandContinentalness =
        Climate.Parameter.Span(-0.11F, 0.03F);
    private readonly Climate.Parameter _midInlandContinentalness =
        Climate.Parameter.Span(0.03F, 0.3F);
    private readonly Climate.Parameter _farInlandContinentalness =
        Climate.Parameter.Span(0.3F, 1.0F);

    public Climate.Parameter FullClimateRange => FullRange;
    public Climate.Parameter FrozenRange => _temperatures[0];
    public Climate.Parameter UnfrozenRange =>
        Climate.Parameter.Span(_temperatures[1], _temperatures[4]);

    public Climate.Parameter MushroomFieldsContinentalness => _mushroomFieldsContinentalness;
    public Climate.Parameter DeepOceanContinentalness => _deepOceanContinentalness;
    public Climate.Parameter OceanContinentalness => _oceanContinentalness;
    public Climate.Parameter CoastContinentalness => _coastContinentalness;
    public Climate.Parameter InlandContinentalness => _inlandContinentalness;
    public Climate.Parameter NearInlandContinentalness => _nearInlandContinentalness;
    public Climate.Parameter MidInlandContinentalness => _midInlandContinentalness;
    public Climate.Parameter FarInlandContinentalness => _farInlandContinentalness;

    public Climate.Parameter[] GetTemperatureThresholds() => _temperatures;
    public Climate.Parameter[] GetHumidityThresholds() => _humidities;
    public Climate.Parameter[] GetErosionThresholds() => _erosions;

    public Climate.Parameter[] GetContinentalnessThresholds() =>
    [
        _mushroomFieldsContinentalness,
        _deepOceanContinentalness,
        _oceanContinentalness,
        _coastContinentalness,
        _nearInlandContinentalness,
        _midInlandContinentalness,
        _farInlandContinentalness
    ];

    public Climate.Parameter[] GetPeaksAndValleysThresholds()
    {
        float valley = TerrainProvider.PeaksAndValleys(ValleySize);
        float low = TerrainProvider.PeaksAndValleys(LowStart);
        float high = TerrainProvider.PeaksAndValleys(HighStart);
        float peak = TerrainProvider.PeaksAndValleys(PeakStart);

        return
        [
            Climate.Parameter.Span(-2.0F, valley),
            Climate.Parameter.Span(valley, low),
            Climate.Parameter.Span(low, high),
            Climate.Parameter.Span(high, peak),
            Climate.Parameter.Span(peak, 2.0F)
        ];
    }

    public Climate.Parameter[] GetWeirdnessThresholds() =>
    [
        Climate.Parameter.Span(-2.0F, 0.0F),
        Climate.Parameter.Span(0.0F, 2.0F)
    ];

    public string GetDebugStringForPeaksAndValleys(double peaksAndValleys)
    {
        if (peaksAndValleys < TerrainProvider.PeaksAndValleys(ValleySize))
            return "Valley";
        if (peaksAndValleys < TerrainProvider.PeaksAndValleys(LowStart))
            return "Low";
        if (peaksAndValleys < TerrainProvider.PeaksAndValleys(HighStart))
            return "Mid";
        return peaksAndValleys < TerrainProvider.PeaksAndValleys(PeakStart) ? "High" : "Peak";
    }

    public string GetDebugStringForContinentalness(double continentalness)
    {
        long value = Climate.QuantizeCoord((float)continentalness);
        if (value < _mushroomFieldsContinentalness.Max)
            return "Mushroom fields";
        if (value < _deepOceanContinentalness.Max)
            return "Deep ocean";
        if (value < _oceanContinentalness.Max)
            return "Ocean";
        if (value < _coastContinentalness.Max)
            return "Coast";
        if (value < _nearInlandContinentalness.Max)
            return "Near inland";
        return value < _midInlandContinentalness.Max ? "Mid inland" : "Far inland";
    }

    public string GetDebugStringForErosion(double erosion) =>
        GetDebugStringForNoiseValue(erosion, _erosions);

    public string GetDebugStringForTemperature(double temperature) =>
        GetDebugStringForNoiseValue(temperature, _temperatures);

    public string GetDebugStringForHumidity(double humidity) =>
        GetDebugStringForNoiseValue(humidity, _humidities);

    private static string GetDebugStringForNoiseValue(
        double noiseValue,
        IReadOnlyList<Climate.Parameter> thresholds)
    {
        long quantized = Climate.QuantizeCoord((float)noiseValue);
        for (int index = 0; index < thresholds.Count; index++)
        {
            if (quantized < thresholds[index].Max)
                return index.ToString(CultureInfo.InvariantCulture);
        }

        return "?";
    }
}
