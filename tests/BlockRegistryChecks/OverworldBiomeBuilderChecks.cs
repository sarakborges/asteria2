using Asteria.Content.Biomes;
using Asteria.Content.Identifiers;
using Asteria.World.LevelGen;

internal static class OverworldBiomeBuilderChecks
{
    public static void Run()
    {
        var builder = new OverworldBiomeBuilder();
        CheckTemperatureAndHumidity(builder);
        CheckErosionAndContinentalness(builder);
        CheckPeaksAndValleysAndWeirdness(builder);
        CheckDiagnosticBoundaries(builder);
        CheckCompleteBiomeParameterTable(builder);
    }

    private static void CheckTemperatureAndHumidity(OverworldBiomeBuilder builder)
    {
        Climate.Parameter[] temperatures = builder.GetTemperatureThresholds();
        Climate.Parameter[] humidities = builder.GetHumidityThresholds();

        Require(temperatures.Length == 5, "Overworld temperature must have five vanilla bands.");
        Require(humidities.Length == 5, "Overworld humidity must have five vanilla bands.");
        AssertRanges(temperatures,
        [
            (-1.0F, -0.45F), (-0.45F, -0.15F), (-0.15F, 0.2F),
            (0.2F, 0.55F), (0.55F, 1.0F)
        ], "temperature");
        AssertRanges(humidities,
        [
            (-1.0F, -0.35F), (-0.35F, -0.1F), (-0.1F, 0.1F),
            (0.1F, 0.3F), (0.3F, 1.0F)
        ], "humidity");

        Require(builder.FrozenRange == temperatures[0] &&
                builder.UnfrozenRange == Climate.Parameter.Span(temperatures[1], temperatures[4]),
            "Frozen and unfrozen ranges must be derived from the temperature bands.");
        Require(builder.FullClimateRange == Climate.Parameter.Span(-1.0F, 1.0F),
            "The shared climate full range must be [-1, 1].");
    }

    private static void CheckErosionAndContinentalness(OverworldBiomeBuilder builder)
    {
        AssertRanges(builder.GetErosionThresholds(),
        [
            (-1.0F, -0.78F), (-0.78F, -0.375F), (-0.375F, -0.2225F),
            (-0.2225F, 0.05F), (0.05F, 0.45F), (0.45F, 0.55F), (0.55F, 1.0F)
        ], "erosion");

        AssertRanges(builder.GetContinentalnessThresholds(),
        [
            (-1.2F, -1.05F), (-1.05F, -0.455F), (-0.455F, -0.19F),
            (-0.19F, -0.11F), (-0.11F, 0.03F), (0.03F, 0.3F), (0.3F, 1.0F)
        ], "continentalness");

        Require(builder.InlandContinentalness == Climate.Parameter.Span(-0.11F, 0.55F),
            "The full inland band must remain separate from near/mid/far diagnostic bands.");
        Require(
            builder.GetContinentalnessThresholds().Length == 7 &&
            builder.GetContinentalnessThresholds()[4] == builder.NearInlandContinentalness &&
            builder.GetContinentalnessThresholds()[5] == builder.MidInlandContinentalness &&
            builder.GetContinentalnessThresholds()[6] == builder.FarInlandContinentalness,
            "Continentalness diagnostics must return the seven reference bands, including separate inland subdivisions.");
    }

    private static void CheckPeaksAndValleysAndWeirdness(OverworldBiomeBuilder builder)
    {
        Climate.Parameter[] thresholds = builder.GetPeaksAndValleysThresholds();
        Require(thresholds.Length == 5, "Peaks-and-valleys must have five diagnostic bands.");
        AssertRanges(thresholds,
        [
            (-2.0F, TerrainProvider.PeaksAndValleys(0.05F)),
            (TerrainProvider.PeaksAndValleys(0.05F), TerrainProvider.PeaksAndValleys(0.26666668F)),
            (TerrainProvider.PeaksAndValleys(0.26666668F), TerrainProvider.PeaksAndValleys(0.4F)),
            (TerrainProvider.PeaksAndValleys(0.4F), TerrainProvider.PeaksAndValleys(0.56666666F)),
            (TerrainProvider.PeaksAndValleys(0.56666666F), 2.0F)
        ], "peaks-and-valleys");

        AssertRanges(builder.GetWeirdnessThresholds(),
        [
            (-2.0F, 0.0F),
            (0.0F, 2.0F)
        ], "weirdness");
    }

    private static void CheckDiagnosticBoundaries(OverworldBiomeBuilder builder)
    {
        float valley = TerrainProvider.PeaksAndValleys(OverworldBiomeBuilder.ValleySize);
        float low = TerrainProvider.PeaksAndValleys(OverworldBiomeBuilder.LowStart);
        float high = TerrainProvider.PeaksAndValleys(OverworldBiomeBuilder.HighStart);
        float peak = TerrainProvider.PeaksAndValleys(OverworldBiomeBuilder.PeakStart);
        Require(
            builder.GetDebugStringForPeaksAndValleys(valley - 0.00001F) == "Valley" &&
            builder.GetDebugStringForPeaksAndValleys(valley) == "Low" &&
            builder.GetDebugStringForPeaksAndValleys(low) == "Mid" &&
            builder.GetDebugStringForPeaksAndValleys(high) == "High" &&
            builder.GetDebugStringForPeaksAndValleys(peak) == "Peak",
            "Peaks-and-valleys debug classification must compare raw noise values with strict vanilla boundaries.");

        Require(builder.GetDebugStringForTemperature(-1.0) == "0" &&
                builder.GetDebugStringForTemperature(-0.45) == "1" &&
                builder.GetDebugStringForTemperature(0.55) == "4" &&
                builder.GetDebugStringForTemperature(1.0) == "?",
            "Temperature debug labels must quantize and use strict upper bounds.");
        Require(builder.GetDebugStringForHumidity(-0.35) == "1" &&
                builder.GetDebugStringForHumidity(0.1) == "3" &&
                builder.GetDebugStringForHumidity(1.0) == "?",
            "Humidity debug labels must use the exact upper-exclusive threshold semantics.");
        Require(builder.GetDebugStringForErosion(-0.78) == "1" &&
                builder.GetDebugStringForErosion(-0.375) == "2" &&
                builder.GetDebugStringForErosion(1.0) == "?",
            "Erosion debug labels must use the seven quantized vanilla bands.");

        Require(
            builder.GetDebugStringForContinentalness(-1.2) == "Mushroom fields" &&
            builder.GetDebugStringForContinentalness(-1.05) == "Deep ocean" &&
            builder.GetDebugStringForContinentalness(-0.455) == "Ocean" &&
            builder.GetDebugStringForContinentalness(-0.19) == "Coast" &&
            builder.GetDebugStringForContinentalness(-0.11) == "Near inland" &&
            builder.GetDebugStringForContinentalness(0.03) == "Mid inland" &&
            builder.GetDebugStringForContinentalness(0.3) == "Far inland",
            "Continentalness labels must follow the quantized upper-exclusive boundaries.");
    }

    private static void CheckCompleteBiomeParameterTable(OverworldBiomeBuilder builder)
    {
        IReadOnlyList<OverworldBiomeBuilder.BiomeParameter> entries =
            builder.BuildBiomeParameters();

        Require(entries.Count == 7594,
            "The Minecraft 26.3 Overworld builder must emit 7,594 surface and underground parameter points.");

        ResourceLocation Key(string name) =>
            ResourceLocation.FromNamespaceAndPath("minecraft", name);

        var distinctBiomes = entries.Select(entry => entry.Biome).ToHashSet();
        Require(
            new[]
            {
                "mushroom_fields", "deep_frozen_ocean", "deep_cold_ocean", "warm_ocean",
                "snowy_plains", "badlands", "eroded_badlands", "windswept_savanna",
                "stony_shore", "river", "frozen_river", "swamp", "mangrove_swamp",
                "dripstone_caves", "lush_caves", "sulfur_caves", "deep_dark"
            }.All(name => distinctBiomes.Contains(Key(name))),
            "The parameter table must include representative off-coast, inland, valley and underground biomes.");

        int depthSurface = entries.Count(entry => entry.Parameters.Depth == Climate.Parameter.Point(0.0F));
        int depthCaves = entries.Count(entry => entry.Parameters.Depth == Climate.Parameter.Span(0.2F, 0.9F));
        int depthBottom = entries.Count(entry => entry.Parameters.Depth == Climate.Parameter.Point(1.1F));
        int depthOne = entries.Count(entry => entry.Parameters.Depth == Climate.Parameter.Point(1.0F));

        Require(depthSurface == 3795 && depthOne == 3795,
            "Every surface biome mapping must be emitted at both depth 0 and depth 1.");
        Require(depthCaves == 3 && depthBottom == 1,
            "The three cave biomes and the bottom-only Deep Dark mapping must retain their exact depth intervals.");
        Require(entries.All(entry => entry.Parameters.Offset == 0L),
            "Vanilla Overworld biome parameter entries must use zero offset.");

        Climate.ParameterList<ResourceLocation> parameterList =
            builder.BuildParameterList(static key => key);
        Require(parameterList.Values().Count == 7594,
            "The builder's registry-key resolver must preserve the entire ordered climate table.");

        Require(
            parameterList.FindValue(Climate.Target(0.0F, 0.0F, -1.1F, 0.0F, 0.0F, 0.0F)) == Key("mushroom_fields"),
            "Mushroom fields must own the full-temperature/full-humidity off-coast climate region.");
        Require(
            parameterList.FindValue(Climate.Target(-0.9F, 0.0F, -0.7F, 0.0F, 0.0F, 0.0F)) == Key("deep_frozen_ocean"),
            "The coldest temperature band over deep-ocean continentalness must select deep frozen ocean.");
        Require(
            parameterList.FindValue(Climate.Target(0.0F, 0.0F, 0.0F, -0.9F, 1.1F, 0.0F)) == Key("deep_dark"),
            "The bottom-only parameter point must select Deep Dark at its target climate.");
    }

    private static void AssertRanges(
        IReadOnlyList<Climate.Parameter> actual,
        IReadOnlyList<(float Min, float Max)> expected,
        string axis)
    {
        Require(actual.Count == expected.Count, $"The {axis} range count must match Minecraft 26.3.");
        for (int index = 0; index < expected.Count; index++)
        {
            Climate.Parameter range = actual[index];
            long min = Climate.QuantizeCoord(expected[index].Min);
            long max = Climate.QuantizeCoord(expected[index].Max);
            Require(
                range.Min == min && range.Max == max,
                $"The {axis} range {index} must be [{expected[index].Min}, {expected[index].Max}].");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
