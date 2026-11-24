using System.Globalization;
using Asteria.Content.Biomes;
using Asteria.Content.Identifiers;
using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>
/// Minecraft Java 26.3 Overworld climate partition and biome parameter distribution.
/// The builder emits registry keys, leaving holder resolution to the selected biome registry.
/// </summary>
public sealed class OverworldBiomeBuilder
{
    public readonly record struct BiomeParameter(
        ResourceLocation Biome,
        Climate.ParameterPoint Parameters);

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

    private const float ErosionDeepDarkDrynessThreshold = -0.225F;
    private const float DepthDeepDarkDrynessThreshold = 0.9F;

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

    private readonly Climate.Parameter _frozenRange;
    private readonly Climate.Parameter _unfrozenRange;
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

    private static readonly ResourceLocation?[,] Oceans =
    {
        {
            Key("deep_frozen_ocean"), Key("deep_cold_ocean"), Key("deep_ocean"),
            Key("deep_lukewarm_ocean"), Key("warm_ocean")
        },
        {
            Key("frozen_ocean"), Key("cold_ocean"), Key("ocean"),
            Key("lukewarm_ocean"), Key("warm_ocean")
        }
    };

    private static readonly ResourceLocation?[,] MiddleBiomes =
    {
        { Key("snowy_plains"), Key("snowy_plains"), Key("snowy_plains"), Key("snowy_taiga"), Key("taiga") },
        { Key("plains"), Key("plains"), Key("forest"), Key("taiga"), Key("old_growth_spruce_taiga") },
        { Key("flower_forest"), Key("plains"), Key("forest"), Key("birch_forest"), Key("dark_forest") },
        { Key("savanna"), Key("savanna"), Key("forest"), Key("jungle"), Key("jungle") },
        { Key("desert"), Key("desert"), Key("desert"), Key("desert"), Key("desert") }
    };

    private static readonly ResourceLocation?[,] MiddleBiomeVariants =
    {
        { Key("ice_spikes"), null, Key("snowy_taiga"), null, null },
        { Key("dappled_forest"), null, null, null, Key("old_growth_pine_taiga") },
        { Key("sunflower_plains"), null, null, Key("old_growth_birch_forest"), null },
        { null, null, Key("plains"), Key("sparse_jungle"), Key("bamboo_jungle") },
        { null, null, null, null, null }
    };

    private static readonly ResourceLocation?[,] PlateauBiomes =
    {
        { Key("snowy_plains"), Key("snowy_plains"), Key("snowy_plains"), Key("snowy_taiga"), Key("snowy_taiga") },
        { Key("meadow"), Key("meadow"), Key("forest"), Key("taiga"), Key("old_growth_spruce_taiga") },
        { Key("meadow"), Key("meadow"), Key("meadow"), Key("meadow"), Key("pale_garden") },
        { Key("savanna_plateau"), Key("savanna_plateau"), Key("forest"), Key("forest"), Key("jungle") },
        { Key("badlands"), Key("badlands"), Key("badlands"), Key("wooded_badlands"), Key("wooded_badlands") }
    };

    private static readonly ResourceLocation?[,] PlateauBiomeVariants =
    {
        { Key("ice_spikes"), null, null, null, null },
        { Key("cherry_grove"), null, Key("meadow"), Key("meadow"), Key("old_growth_pine_taiga") },
        { Key("cherry_grove"), Key("cherry_grove"), Key("forest"), Key("birch_forest"), null },
        { null, null, null, null, null },
        { Key("eroded_badlands"), Key("eroded_badlands"), null, null, null }
    };

    private static readonly ResourceLocation?[,] ShatteredBiomes =
    {
        {
            Key("windswept_gravelly_hills"), Key("windswept_gravelly_hills"),
            Key("windswept_hills"), Key("windswept_forest"), Key("windswept_forest")
        },
        {
            Key("windswept_gravelly_hills"), Key("windswept_gravelly_hills"),
            Key("windswept_hills"), Key("windswept_forest"), Key("windswept_forest")
        },
        {
            Key("windswept_hills"), Key("windswept_hills"),
            Key("windswept_hills"), Key("windswept_forest"), Key("windswept_forest")
        },
        { null, null, null, null, null },
        { null, null, null, null, null }
    };

    public OverworldBiomeBuilder()
    {
        _frozenRange = _temperatures[0];
        _unfrozenRange = Climate.Parameter.Span(_temperatures[1], _temperatures[4]);
    }

    public Climate.Parameter FullClimateRange => FullRange;
    public Climate.Parameter FrozenRange => _frozenRange;
    public Climate.Parameter UnfrozenRange => _unfrozenRange;

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

    /// <summary>
    /// Emits the complete 26.3 Overworld climate table as biome resource keys and parameter points.
    /// Missing biome definitions remain keys; resolving them against a registry is a separate step.
    /// </summary>
    public IReadOnlyList<BiomeParameter> BuildBiomeParameters()
    {
        var entries = new List<BiomeParameter>(7594);
        AddOffCoastBiomes(entries);
        AddInlandBiomes(entries);
        AddUndergroundBiomes(entries);
        return entries;
    }

    /// <summary>Builds the R-tree-backed climate list after resolving every emitted biome key.</summary>
    public Climate.ParameterList<TBiome> BuildParameterList<TBiome>(
        Func<ResourceLocation, TBiome> resolveBiome)
    {
        ArgumentNullException.ThrowIfNull(resolveBiome);

        IReadOnlyList<BiomeParameter> entries = BuildBiomeParameters();
        var values = new List<Pair<Climate.ParameterPoint, TBiome>>(entries.Count);
        foreach (BiomeParameter entry in entries)
        {
            values.Add(new Pair<Climate.ParameterPoint, TBiome>(
                entry.Parameters,
                resolveBiome(entry.Biome)));
        }

        return new Climate.ParameterList<TBiome>(values);
    }

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

    private void AddOffCoastBiomes(List<BiomeParameter> entries)
    {
        AddSurfaceBiome(entries, FullRange, FullRange, _mushroomFieldsContinentalness,
            FullRange, FullRange, 0.0F, Key("mushroom_fields"));

        for (int temperatureIndex = 0; temperatureIndex < _temperatures.Length; temperatureIndex++)
        {
            Climate.Parameter temperature = _temperatures[temperatureIndex];
            AddSurfaceBiome(entries, temperature, FullRange, _deepOceanContinentalness,
                FullRange, FullRange, 0.0F, RequiredBiome(Oceans[0, temperatureIndex]));
            AddSurfaceBiome(entries, temperature, FullRange, _oceanContinentalness,
                FullRange, FullRange, 0.0F, RequiredBiome(Oceans[1, temperatureIndex]));
        }
    }

    private void AddInlandBiomes(List<BiomeParameter> entries)
    {
        AddMidSlice(entries, Climate.Parameter.Span(-1.0F, -HighEnd));
        AddHighSlice(entries, Climate.Parameter.Span(-HighEnd, -PeakEnd));
        AddPeaks(entries, Climate.Parameter.Span(-PeakEnd, -PeakStart));
        AddHighSlice(entries, Climate.Parameter.Span(-PeakStart, -HighStart));
        AddMidSlice(entries, Climate.Parameter.Span(-HighStart, -LowStart));
        AddLowSlice(entries, Climate.Parameter.Span(-LowStart, -ValleySize));
        AddValleys(entries, Climate.Parameter.Span(-ValleySize, ValleySize));
        AddLowSlice(entries, Climate.Parameter.Span(ValleySize, LowStart));
        AddMidSlice(entries, Climate.Parameter.Span(LowStart, HighStart));
        AddHighSlice(entries, Climate.Parameter.Span(HighStart, PeakStart));
        AddPeaks(entries, Climate.Parameter.Span(PeakStart, PeakEnd));
        AddHighSlice(entries, Climate.Parameter.Span(PeakEnd, HighEnd));
        AddMidSlice(entries, Climate.Parameter.Span(HighEnd, 1.0F));
    }

    private void AddPeaks(List<BiomeParameter> entries, Climate.Parameter weirdness)
    {
        for (int temperatureIndex = 0; temperatureIndex < _temperatures.Length; temperatureIndex++)
        {
            Climate.Parameter temperature = _temperatures[temperatureIndex];
            for (int humidityIndex = 0; humidityIndex < _humidities.Length; humidityIndex++)
            {
                Climate.Parameter humidity = _humidities[humidityIndex];
                ResourceLocation middleBiome = PickMiddleBiome(temperatureIndex, humidityIndex, weirdness);
                ResourceLocation middleOrBadlands = PickMiddleBiomeOrBadlandsIfHot(temperatureIndex, humidityIndex, weirdness);
                ResourceLocation middleOrBadlandsOrSlope = PickMiddleBiomeOrBadlandsIfHotOrSlopeIfCold(temperatureIndex, humidityIndex, weirdness);
                ResourceLocation plateauBiome = PickPlateauBiome(temperatureIndex, humidityIndex, weirdness);
                ResourceLocation shatteredBiome = PickShatteredBiome(temperatureIndex, humidityIndex, weirdness);
                ResourceLocation shatteredOrSavanna = MaybePickWindsweptSavannaBiome(
                    temperatureIndex, humidityIndex, weirdness, shatteredBiome);
                ResourceLocation peakBiome = PickPeakBiome(temperatureIndex, humidityIndex, weirdness);

                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_coastContinentalness, _farInlandContinentalness),
                    _erosions[0], weirdness, 0.0F, peakBiome);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_coastContinentalness, _nearInlandContinentalness),
                    _erosions[1], weirdness, 0.0F, middleOrBadlandsOrSlope);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_midInlandContinentalness, _farInlandContinentalness),
                    _erosions[1], weirdness, 0.0F, peakBiome);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_coastContinentalness, _nearInlandContinentalness),
                    Climate.Parameter.Span(_erosions[2], _erosions[3]),
                    weirdness, 0.0F, middleBiome);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_midInlandContinentalness, _farInlandContinentalness),
                    _erosions[2], weirdness, 0.0F, plateauBiome);
                AddSurfaceBiome(entries, temperature, humidity, _midInlandContinentalness,
                    _erosions[3], weirdness, 0.0F, middleOrBadlands);
                AddSurfaceBiome(entries, temperature, humidity, _farInlandContinentalness,
                    _erosions[3], weirdness, 0.0F, plateauBiome);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_coastContinentalness, _farInlandContinentalness),
                    _erosions[4], weirdness, 0.0F, middleBiome);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_coastContinentalness, _nearInlandContinentalness),
                    _erosions[5], weirdness, 0.0F, shatteredOrSavanna);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_midInlandContinentalness, _farInlandContinentalness),
                    _erosions[5], weirdness, 0.0F, shatteredBiome);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_coastContinentalness, _farInlandContinentalness),
                    _erosions[6], weirdness, 0.0F, middleBiome);
            }
        }
    }

    private void AddHighSlice(List<BiomeParameter> entries, Climate.Parameter weirdness)
    {
        for (int temperatureIndex = 0; temperatureIndex < _temperatures.Length; temperatureIndex++)
        {
            Climate.Parameter temperature = _temperatures[temperatureIndex];
            for (int humidityIndex = 0; humidityIndex < _humidities.Length; humidityIndex++)
            {
                Climate.Parameter humidity = _humidities[humidityIndex];
                ResourceLocation middleBiome = PickMiddleBiome(temperatureIndex, humidityIndex, weirdness);
                ResourceLocation middleOrBadlands = PickMiddleBiomeOrBadlandsIfHot(temperatureIndex, humidityIndex, weirdness);
                ResourceLocation middleOrBadlandsOrSlope = PickMiddleBiomeOrBadlandsIfHotOrSlopeIfCold(temperatureIndex, humidityIndex, weirdness);
                ResourceLocation plateauBiome = PickPlateauBiome(temperatureIndex, humidityIndex, weirdness);
                ResourceLocation shatteredBiome = PickShatteredBiome(temperatureIndex, humidityIndex, weirdness);
                ResourceLocation middleOrSavanna = MaybePickWindsweptSavannaBiome(
                    temperatureIndex, humidityIndex, weirdness, middleBiome);
                ResourceLocation slopeBiome = PickSlopeBiome(temperatureIndex, humidityIndex, weirdness);
                ResourceLocation peakBiome = PickPeakBiome(temperatureIndex, humidityIndex, weirdness);

                AddSurfaceBiome(entries, temperature, humidity, _coastContinentalness,
                    Climate.Parameter.Span(_erosions[0], _erosions[1]), weirdness, 0.0F, middleBiome);
                AddSurfaceBiome(entries, temperature, humidity, _nearInlandContinentalness,
                    _erosions[0], weirdness, 0.0F, slopeBiome);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_midInlandContinentalness, _farInlandContinentalness),
                    _erosions[0], weirdness, 0.0F, peakBiome);
                AddSurfaceBiome(entries, temperature, humidity, _nearInlandContinentalness,
                    _erosions[1], weirdness, 0.0F, middleOrBadlandsOrSlope);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_midInlandContinentalness, _farInlandContinentalness),
                    _erosions[1], weirdness, 0.0F, slopeBiome);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_coastContinentalness, _nearInlandContinentalness),
                    Climate.Parameter.Span(_erosions[2], _erosions[3]),
                    weirdness, 0.0F, middleBiome);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_midInlandContinentalness, _farInlandContinentalness),
                    _erosions[2], weirdness, 0.0F, plateauBiome);
                AddSurfaceBiome(entries, temperature, humidity, _midInlandContinentalness,
                    _erosions[3], weirdness, 0.0F, middleOrBadlands);
                AddSurfaceBiome(entries, temperature, humidity, _farInlandContinentalness,
                    _erosions[3], weirdness, 0.0F, plateauBiome);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_coastContinentalness, _farInlandContinentalness),
                    _erosions[4], weirdness, 0.0F, middleBiome);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_coastContinentalness, _nearInlandContinentalness),
                    _erosions[5], weirdness, 0.0F, middleOrSavanna);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_midInlandContinentalness, _farInlandContinentalness),
                    _erosions[5], weirdness, 0.0F, shatteredBiome);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_coastContinentalness, _farInlandContinentalness),
                    _erosions[6], weirdness, 0.0F, middleBiome);
            }
        }
    }

    private void AddMidSlice(List<BiomeParameter> entries, Climate.Parameter weirdness)
    {
        AddSurfaceBiome(entries, FullRange, FullRange, _coastContinentalness,
            Climate.Parameter.Span(_erosions[0], _erosions[2]), weirdness, 0.0F, Key("stony_shore"));
        AddSurfaceBiome(entries, Climate.Parameter.Span(_temperatures[1], _temperatures[2]),
            FullRange, Climate.Parameter.Span(_nearInlandContinentalness, _farInlandContinentalness),
            _erosions[6], weirdness, 0.0F, Key("swamp"));
        AddSurfaceBiome(entries, Climate.Parameter.Span(_temperatures[3], _temperatures[4]),
            FullRange, Climate.Parameter.Span(_nearInlandContinentalness, _farInlandContinentalness),
            _erosions[6], weirdness, 0.0F, Key("mangrove_swamp"));

        for (int temperatureIndex = 0; temperatureIndex < _temperatures.Length; temperatureIndex++)
        {
            Climate.Parameter temperature = _temperatures[temperatureIndex];
            for (int humidityIndex = 0; humidityIndex < _humidities.Length; humidityIndex++)
            {
                Climate.Parameter humidity = _humidities[humidityIndex];
                ResourceLocation middleBiome = PickMiddleBiome(temperatureIndex, humidityIndex, weirdness);
                ResourceLocation middleOrBadlands = PickMiddleBiomeOrBadlandsIfHot(temperatureIndex, humidityIndex, weirdness);
                ResourceLocation middleOrBadlandsOrSlope = PickMiddleBiomeOrBadlandsIfHotOrSlopeIfCold(temperatureIndex, humidityIndex, weirdness);
                ResourceLocation shatteredBiome = PickShatteredBiome(temperatureIndex, humidityIndex, weirdness);
                ResourceLocation plateauBiome = PickPlateauBiome(temperatureIndex, humidityIndex, weirdness);
                ResourceLocation beachBiome = PickBeachBiome(temperatureIndex);
                ResourceLocation middleOrSavanna = MaybePickWindsweptSavannaBiome(
                    temperatureIndex, humidityIndex, weirdness, middleBiome);
                ResourceLocation shatteredCoastBiome = PickShatteredCoastBiome(
                    temperatureIndex, humidityIndex, weirdness);
                ResourceLocation slopeBiome = PickSlopeBiome(temperatureIndex, humidityIndex, weirdness);

                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_nearInlandContinentalness, _farInlandContinentalness),
                    _erosions[0], weirdness, 0.0F, slopeBiome);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_nearInlandContinentalness, _midInlandContinentalness),
                    _erosions[1], weirdness, 0.0F, middleOrBadlandsOrSlope);
                AddSurfaceBiome(entries, temperature, humidity, _farInlandContinentalness,
                    _erosions[1], weirdness, 0.0F,
                    temperatureIndex == 0 ? slopeBiome : plateauBiome);
                AddSurfaceBiome(entries, temperature, humidity, _nearInlandContinentalness,
                    _erosions[2], weirdness, 0.0F, middleBiome);
                AddSurfaceBiome(entries, temperature, humidity, _midInlandContinentalness,
                    _erosions[2], weirdness, 0.0F, middleOrBadlands);
                AddSurfaceBiome(entries, temperature, humidity, _farInlandContinentalness,
                    _erosions[2], weirdness, 0.0F, plateauBiome);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_coastContinentalness, _nearInlandContinentalness),
                    _erosions[3], weirdness, 0.0F, middleBiome);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_midInlandContinentalness, _farInlandContinentalness),
                    _erosions[3], weirdness, 0.0F, middleOrBadlands);

                if (weirdness.Max < 0L)
                {
                    AddSurfaceBiome(entries, temperature, humidity, _coastContinentalness,
                        _erosions[4], weirdness, 0.0F, beachBiome);
                    AddSurfaceBiome(entries, temperature, humidity,
                        Climate.Parameter.Span(_nearInlandContinentalness, _farInlandContinentalness),
                        _erosions[4], weirdness, 0.0F, middleBiome);
                }
                else
                {
                    AddSurfaceBiome(entries, temperature, humidity,
                        Climate.Parameter.Span(_coastContinentalness, _farInlandContinentalness),
                        _erosions[4], weirdness, 0.0F, middleBiome);
                }

                AddSurfaceBiome(entries, temperature, humidity, _coastContinentalness,
                    _erosions[5], weirdness, 0.0F, shatteredCoastBiome);
                AddSurfaceBiome(entries, temperature, humidity, _nearInlandContinentalness,
                    _erosions[5], weirdness, 0.0F, middleOrSavanna);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_midInlandContinentalness, _farInlandContinentalness),
                    _erosions[5], weirdness, 0.0F, shatteredBiome);

                AddSurfaceBiome(entries, temperature, humidity, _coastContinentalness,
                    _erosions[6], weirdness, 0.0F,
                    weirdness.Max < 0L ? beachBiome : middleBiome);

                if (temperatureIndex == 0)
                {
                    AddSurfaceBiome(entries, temperature, humidity,
                        Climate.Parameter.Span(_nearInlandContinentalness, _farInlandContinentalness),
                        _erosions[6], weirdness, 0.0F, middleBiome);
                }
            }
        }
    }

    private void AddLowSlice(List<BiomeParameter> entries, Climate.Parameter weirdness)
    {
        AddSurfaceBiome(entries, FullRange, FullRange, _coastContinentalness,
            Climate.Parameter.Span(_erosions[0], _erosions[2]), weirdness, 0.0F, Key("stony_shore"));
        AddSurfaceBiome(entries, Climate.Parameter.Span(_temperatures[1], _temperatures[2]),
            FullRange, Climate.Parameter.Span(_nearInlandContinentalness, _farInlandContinentalness),
            _erosions[6], weirdness, 0.0F, Key("swamp"));
        AddSurfaceBiome(entries, Climate.Parameter.Span(_temperatures[3], _temperatures[4]),
            FullRange, Climate.Parameter.Span(_nearInlandContinentalness, _farInlandContinentalness),
            _erosions[6], weirdness, 0.0F, Key("mangrove_swamp"));

        for (int temperatureIndex = 0; temperatureIndex < _temperatures.Length; temperatureIndex++)
        {
            Climate.Parameter temperature = _temperatures[temperatureIndex];
            for (int humidityIndex = 0; humidityIndex < _humidities.Length; humidityIndex++)
            {
                Climate.Parameter humidity = _humidities[humidityIndex];
                ResourceLocation middleBiome = PickMiddleBiome(temperatureIndex, humidityIndex, weirdness);
                ResourceLocation middleOrBadlands = PickMiddleBiomeOrBadlandsIfHot(temperatureIndex, humidityIndex, weirdness);
                ResourceLocation middleOrBadlandsOrSlope = PickMiddleBiomeOrBadlandsIfHotOrSlopeIfCold(temperatureIndex, humidityIndex, weirdness);
                ResourceLocation beachBiome = PickBeachBiome(temperatureIndex);
                ResourceLocation middleOrSavanna = MaybePickWindsweptSavannaBiome(
                    temperatureIndex, humidityIndex, weirdness, middleBiome);
                ResourceLocation shatteredCoastBiome = PickShatteredCoastBiome(
                    temperatureIndex, humidityIndex, weirdness);

                AddSurfaceBiome(entries, temperature, humidity, _nearInlandContinentalness,
                    Climate.Parameter.Span(_erosions[0], _erosions[1]),
                    weirdness, 0.0F, middleOrBadlands);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_midInlandContinentalness, _farInlandContinentalness),
                    Climate.Parameter.Span(_erosions[0], _erosions[1]),
                    weirdness, 0.0F, middleOrBadlandsOrSlope);
                AddSurfaceBiome(entries, temperature, humidity, _nearInlandContinentalness,
                    Climate.Parameter.Span(_erosions[2], _erosions[3]),
                    weirdness, 0.0F, middleBiome);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_midInlandContinentalness, _farInlandContinentalness),
                    Climate.Parameter.Span(_erosions[2], _erosions[3]),
                    weirdness, 0.0F, middleOrBadlands);
                AddSurfaceBiome(entries, temperature, humidity, _coastContinentalness,
                    Climate.Parameter.Span(_erosions[3], _erosions[4]),
                    weirdness, 0.0F, beachBiome);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_nearInlandContinentalness, _farInlandContinentalness),
                    _erosions[4], weirdness, 0.0F, middleBiome);
                AddSurfaceBiome(entries, temperature, humidity, _coastContinentalness,
                    _erosions[5], weirdness, 0.0F, shatteredCoastBiome);
                AddSurfaceBiome(entries, temperature, humidity, _nearInlandContinentalness,
                    _erosions[5], weirdness, 0.0F, middleOrSavanna);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_midInlandContinentalness, _farInlandContinentalness),
                    _erosions[5], weirdness, 0.0F, middleBiome);
                AddSurfaceBiome(entries, temperature, humidity, _coastContinentalness,
                    _erosions[6], weirdness, 0.0F, beachBiome);

                if (temperatureIndex == 0)
                {
                    AddSurfaceBiome(entries, temperature, humidity,
                        Climate.Parameter.Span(_nearInlandContinentalness, _farInlandContinentalness),
                        _erosions[6], weirdness, 0.0F, middleBiome);
                }
            }
        }
    }

    private void AddValleys(List<BiomeParameter> entries, Climate.Parameter weirdness)
    {
        AddSurfaceBiome(entries, _frozenRange, FullRange, _coastContinentalness,
            Climate.Parameter.Span(_erosions[0], _erosions[1]), weirdness, 0.0F,
            weirdness.Max < 0L ? Key("stony_shore") : Key("frozen_river"));
        AddSurfaceBiome(entries, _unfrozenRange, FullRange, _coastContinentalness,
            Climate.Parameter.Span(_erosions[0], _erosions[1]), weirdness, 0.0F,
            weirdness.Max < 0L ? Key("stony_shore") : Key("river"));
        AddSurfaceBiome(entries, _frozenRange, FullRange, _nearInlandContinentalness,
            Climate.Parameter.Span(_erosions[0], _erosions[1]), weirdness, 0.0F, Key("frozen_river"));
        AddSurfaceBiome(entries, _unfrozenRange, FullRange, _nearInlandContinentalness,
            Climate.Parameter.Span(_erosions[0], _erosions[1]), weirdness, 0.0F, Key("river"));
        AddSurfaceBiome(entries, _frozenRange, FullRange,
            Climate.Parameter.Span(_coastContinentalness, _farInlandContinentalness),
            Climate.Parameter.Span(_erosions[2], _erosions[5]), weirdness, 0.0F, Key("frozen_river"));
        AddSurfaceBiome(entries, _unfrozenRange, FullRange,
            Climate.Parameter.Span(_coastContinentalness, _farInlandContinentalness),
            Climate.Parameter.Span(_erosions[2], _erosions[5]), weirdness, 0.0F, Key("river"));
        AddSurfaceBiome(entries, _frozenRange, FullRange, _coastContinentalness,
            _erosions[6], weirdness, 0.0F, Key("frozen_river"));
        AddSurfaceBiome(entries, _unfrozenRange, FullRange, _coastContinentalness,
            _erosions[6], weirdness, 0.0F, Key("river"));
        AddSurfaceBiome(entries, Climate.Parameter.Span(_temperatures[1], _temperatures[2]),
            FullRange, Climate.Parameter.Span(_inlandContinentalness, _farInlandContinentalness),
            _erosions[6], weirdness, 0.0F, Key("swamp"));
        AddSurfaceBiome(entries, Climate.Parameter.Span(_temperatures[3], _temperatures[4]),
            FullRange, Climate.Parameter.Span(_inlandContinentalness, _farInlandContinentalness),
            _erosions[6], weirdness, 0.0F, Key("mangrove_swamp"));
        AddSurfaceBiome(entries, _frozenRange, FullRange,
            Climate.Parameter.Span(_inlandContinentalness, _farInlandContinentalness),
            _erosions[6], weirdness, 0.0F, Key("frozen_river"));

        for (int temperatureIndex = 0; temperatureIndex < _temperatures.Length; temperatureIndex++)
        {
            Climate.Parameter temperature = _temperatures[temperatureIndex];
            for (int humidityIndex = 0; humidityIndex < _humidities.Length; humidityIndex++)
            {
                Climate.Parameter humidity = _humidities[humidityIndex];
                ResourceLocation middleOrBadlands = PickMiddleBiomeOrBadlandsIfHot(
                    temperatureIndex, humidityIndex, weirdness);
                AddSurfaceBiome(entries, temperature, humidity,
                    Climate.Parameter.Span(_midInlandContinentalness, _farInlandContinentalness),
                    Climate.Parameter.Span(_erosions[0], _erosions[1]),
                    weirdness, 0.0F, middleOrBadlands);
            }
        }
    }

    private void AddUndergroundBiomes(List<BiomeParameter> entries)
    {
        AddUndergroundBiome(entries, FullRange, FullRange,
            Climate.Parameter.Span(0.8F, 1.0F), FullRange, FullRange, 0.0F, Key("dripstone_caves"));
        AddUndergroundBiome(entries, FullRange,
            Climate.Parameter.Span(0.7F, 1.0F), FullRange, FullRange, FullRange, 0.0F, Key("lush_caves"));
        AddUndergroundBiome(entries, FullRange, FullRange,
            Climate.Parameter.Span(_coastContinentalness, _inlandContinentalness),
            Climate.Parameter.Span(_erosions[5], _erosions[6]),
            Climate.Parameter.Span(-1.1F, -0.85F), 0.0F, Key("sulfur_caves"));
        AddBottomBiome(entries, FullRange, FullRange, FullRange,
            Climate.Parameter.Span(_erosions[0], _erosions[1]),
            FullRange, 0.0F, Key("deep_dark"));
    }

    private static void AddSurfaceBiome(
        List<BiomeParameter> entries,
        Climate.Parameter temperature,
        Climate.Parameter humidity,
        Climate.Parameter continentalness,
        Climate.Parameter erosion,
        Climate.Parameter weirdness,
        float offset,
        ResourceLocation biome)
    {
        entries.Add(new BiomeParameter(
            biome,
            Climate.Parameters(temperature, humidity, continentalness, erosion,
                Climate.Parameter.Point(0.0F), weirdness, offset)));
        entries.Add(new BiomeParameter(
            biome,
            Climate.Parameters(temperature, humidity, continentalness, erosion,
                Climate.Parameter.Point(1.0F), weirdness, offset)));
    }

    private static void AddUndergroundBiome(
        List<BiomeParameter> entries,
        Climate.Parameter temperature,
        Climate.Parameter humidity,
        Climate.Parameter continentalness,
        Climate.Parameter erosion,
        Climate.Parameter weirdness,
        float offset,
        ResourceLocation biome) =>
        entries.Add(new BiomeParameter(
            biome,
            Climate.Parameters(temperature, humidity, continentalness, erosion,
                Climate.Parameter.Span(0.2F, 0.9F), weirdness, offset)));

    private static void AddBottomBiome(
        List<BiomeParameter> entries,
        Climate.Parameter temperature,
        Climate.Parameter humidity,
        Climate.Parameter continentalness,
        Climate.Parameter erosion,
        Climate.Parameter weirdness,
        float offset,
        ResourceLocation biome) =>
        entries.Add(new BiomeParameter(
            biome,
            Climate.Parameters(temperature, humidity, continentalness, erosion,
                Climate.Parameter.Point(1.1F), weirdness, offset)));

    private ResourceLocation PickMiddleBiome(int temperatureIndex, int humidityIndex, Climate.Parameter weirdness)
    {
        if (weirdness.Max < 0L)
            return RequiredBiome(MiddleBiomes[temperatureIndex, humidityIndex]);

        ResourceLocation? variant = MiddleBiomeVariants[temperatureIndex, humidityIndex];
        return variant ?? RequiredBiome(MiddleBiomes[temperatureIndex, humidityIndex]);
    }

    private ResourceLocation PickMiddleBiomeOrBadlandsIfHot(
        int temperatureIndex,
        int humidityIndex,
        Climate.Parameter weirdness) =>
        temperatureIndex == 4
            ? PickBadlandsBiome(humidityIndex, weirdness)
            : PickMiddleBiome(temperatureIndex, humidityIndex, weirdness);

    private ResourceLocation PickMiddleBiomeOrBadlandsIfHotOrSlopeIfCold(
        int temperatureIndex,
        int humidityIndex,
        Climate.Parameter weirdness) =>
        temperatureIndex == 0
            ? PickSlopeBiome(temperatureIndex, humidityIndex, weirdness)
            : PickMiddleBiomeOrBadlandsIfHot(temperatureIndex, humidityIndex, weirdness);

    private static ResourceLocation MaybePickWindsweptSavannaBiome(
        int temperatureIndex,
        int humidityIndex,
        Climate.Parameter weirdness,
        ResourceLocation underlyingBiome) =>
        temperatureIndex > 1 && humidityIndex < 4 && weirdness.Max >= 0L
            ? Key("windswept_savanna")
            : underlyingBiome;

    private ResourceLocation PickShatteredCoastBiome(
        int temperatureIndex,
        int humidityIndex,
        Climate.Parameter weirdness)
    {
        ResourceLocation beachOrMiddle = weirdness.Max >= 0L
            ? PickMiddleBiome(temperatureIndex, humidityIndex, weirdness)
            : PickBeachBiome(temperatureIndex);
        return MaybePickWindsweptSavannaBiome(
            temperatureIndex, humidityIndex, weirdness, beachOrMiddle);
    }

    private static ResourceLocation PickBeachBiome(int temperatureIndex) =>
        temperatureIndex switch
        {
            0 => Key("snowy_beach"),
            4 => Key("desert"),
            _ => Key("beach")
        };

    private static ResourceLocation PickBadlandsBiome(int humidityIndex, Climate.Parameter weirdness)
    {
        if (humidityIndex < 2)
            return weirdness.Max < 0L ? Key("badlands") : Key("eroded_badlands");
        if (humidityIndex < 3)
            return Key("badlands");
        return Key("wooded_badlands");
    }

    private ResourceLocation PickPlateauBiome(
        int temperatureIndex,
        int humidityIndex,
        Climate.Parameter weirdness)
    {
        ResourceLocation? variant = PlateauBiomeVariants[temperatureIndex, humidityIndex];
        if (weirdness.Max >= 0L && variant.HasValue)
            return variant.Value;
        return RequiredBiome(PlateauBiomes[temperatureIndex, humidityIndex]);
    }

    private ResourceLocation PickPeakBiome(
        int temperatureIndex,
        int humidityIndex,
        Climate.Parameter weirdness)
    {
        if (temperatureIndex <= 2)
            return weirdness.Max < 0L ? Key("jagged_peaks") : Key("frozen_peaks");
        if (temperatureIndex == 3)
            return Key("stony_peaks");
        return PickBadlandsBiome(humidityIndex, weirdness);
    }

    private ResourceLocation PickSlopeBiome(
        int temperatureIndex,
        int humidityIndex,
        Climate.Parameter weirdness)
    {
        if (temperatureIndex >= 3)
            return PickPlateauBiome(temperatureIndex, humidityIndex, weirdness);
        return humidityIndex <= 1 ? Key("snowy_slopes") : Key("grove");
    }

    private ResourceLocation PickShatteredBiome(
        int temperatureIndex,
        int humidityIndex,
        Climate.Parameter weirdness) =>
        ShatteredBiomes[temperatureIndex, humidityIndex] ??
        PickMiddleBiome(temperatureIndex, humidityIndex, weirdness);

    private static ResourceLocation Key(string path) =>
        ResourceLocation.FromNamespaceAndPath("minecraft", path);

    private static ResourceLocation RequiredBiome(ResourceLocation? biome) =>
        biome ?? throw new InvalidOperationException("A required Overworld biome matrix entry is null.");
}
