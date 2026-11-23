using System.Text.Json;
using Asteria.Serialization;
using Asteria.World;
using Asteria.World.LevelGen;

internal static class NoiseSettingsChecks
{
    public static void Run()
    {
        CheckVanillaPresets();
        CheckCodecRoundTrip();
        CheckCodecGuards();
        CheckCreateGuards();
        CheckHeightAccessorIntersection();
    }

    private static void CheckVanillaPresets()
    {
        Require(
            NoiseSettings.OVERWORLD_NOISE_SETTINGS == new NoiseSettings(-64, 384) &&
            NoiseSettings.NETHER_NOISE_SETTINGS == new NoiseSettings(0, 128) &&
            NoiseSettings.END_NOISE_SETTINGS == new NoiseSettings(0, 128) &&
            NoiseSettings.CAVES_NOISE_SETTINGS == new NoiseSettings(-64, 192) &&
            NoiseSettings.FLOATING_ISLANDS_NOISE_SETTINGS == new NoiseSettings(0, 256),
            "NoiseSettings presets must match the five vanilla 26.3 vertical configurations.");

        Require(
            NoiseSettings.DimensionMinY == -2032 &&
            NoiseSettings.DimensionMaxY == 2031 &&
            NoiseSettings.DimensionYSize == 4064,
            "NoiseSettings dimension bounds must derive from the 12-bit packed block-position Y layout.");
    }

    private static void CheckCodecRoundTrip()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        NoiseSettings expected = new(-64, 384);
        JsonElement encoded = NoiseSettings.CODEC.EncodeStart(ops, expected).GetOrThrow();

        Require(
            ops.Get(encoded, "min_y").GetOrThrow().GetInt32() == -64 &&
            ops.Get(encoded, "height").GetOrThrow().GetInt32() == 384,
            "NoiseSettings codec must use the vanilla min_y and height field names.");

        NoiseSettings decoded = NoiseSettings.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(
            decoded == expected,
            "NoiseSettings codec must round-trip both vertical bounds.");
    }

    private static void CheckCodecGuards()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        Require(
            NoiseSettings.CODEC.Parse(
                ops,
                ParseJson("""{"min_y":2016,"height":16}""")).IsSuccess,
            "NoiseSettings codec must allow a top bound exactly at DimensionType.MAX_Y + 1.");

        Require(
            NoiseSettings.CODEC.Parse(
                ops,
                ParseJson("""{"min_y":2016,"height":32}""")).IsError,
            "NoiseSettings codec must reject a range extending above DimensionType.MAX_Y.");

        Require(
            NoiseSettings.CODEC.Parse(
                ops,
                ParseJson("""{"min_y":-63,"height":384}""")).IsError,
            "NoiseSettings codec must reject min_y values that are not section-aligned.");

        Require(
            NoiseSettings.CODEC.Parse(
                ops,
                ParseJson("""{"min_y":-64,"height":10}""")).IsError,
            "NoiseSettings codec must reject heights that are not multiples of 16.");

        Require(
            NoiseSettings.CODEC.Parse(
                ops,
                ParseJson("""{"min_y":2032,"height":16}""")).IsError,
            "NoiseSettings codec must apply DimensionType's inclusive min_y range before the NoiseSettings guards.");

        Require(
            NoiseSettings.CODEC.Parse(
                ops,
                ParseJson("""{"min_y":-64,"height":-16}""")).IsError,
            "NoiseSettings codec must reject negative heights through its field range.");
    }

    private static void CheckCreateGuards()
    {
        Require(
            NoiseSettings.Create(-64, 384) == new NoiseSettings(-64, 384),
            "NoiseSettings.create must retain valid aligned settings.");

        RequireThrows(
            () => NoiseSettings.Create(2016, 32),
            "NoiseSettings.create must reject a range above the dimension maximum.");

        RequireThrows(
            () => NoiseSettings.Create(-63, 384),
            "NoiseSettings.create must reject a non-section-aligned min_y.");

        RequireThrows(
            () => NoiseSettings.Create(-64, 10),
            "NoiseSettings.create must reject a non-section-aligned height.");

        Require(
            NoiseSettings.Create(-64, -16) == new NoiseSettings(-64, -16),
            "NoiseSettings.create must preserve the vanilla guard contract instead of adding the codec's non-negative height range.");
    }

    private static void CheckHeightAccessorIntersection()
    {
        NoiseSettings overworld = NoiseSettings.Create(-64, 384);

        Require(
            overworld.ClampToHeightAccessor(LevelHeightAccessor.Create(-16, 32)) ==
                new NoiseSettings(-16, 32),
            "Clamping noise settings to a contained build range must return the exact intersection.");

        Require(
            overworld.ClampToHeightAccessor(LevelHeightAccessor.Create(64, 64)) ==
                new NoiseSettings(64, 64),
            "Clamping noise settings to an upper build range must preserve its exact intersection.");

        Require(
            NoiseSettings.Create(-64, 16)
                .ClampToHeightAccessor(LevelHeightAccessor.Create(0, 16)) ==
                new NoiseSettings(0, -48),
            "A disjoint clamp must preserve vanilla's non-positive intersection height rather than silently normalize it.");
    }

    private static JsonElement ParseJson(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
