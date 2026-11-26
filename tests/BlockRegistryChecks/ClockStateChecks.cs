using System.Text.Json;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.Clocks;

internal static class ClockStateChecks
{
    public static void Run()
    {
        CheckClockStateCodec();
        CheckPackedClockStatesCodec();
    }

    private static void CheckClockStateCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var state = new ClockState(9007199254740993L, 0.0F, 1.0F, false);
        JsonElement encoded = ClockState.CODEC.EncodeStart(ops, state).GetOrThrow();
        Require(ops.Get(encoded, "total_ticks").GetOrThrow().GetInt64() == state.TotalTicks,
            "ClockState must preserve full 64-bit total tick precision.");
        Require(!ops.Get(encoded, "partial_tick").TryGetResult(out _) &&
                !ops.Get(encoded, "rate").TryGetResult(out _) &&
                !ops.Get(encoded, "paused").TryGetResult(out _),
            "ClockState must omit its default partial tick, rate and paused fields.");

        ClockState decoded = ClockState.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(decoded == state,
            "ClockState codecs must round-trip and restore omitted field defaults.");

        JsonElement zeroRate = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(ops.CreateString("total_ticks"), ops.CreateLong(10)),
            new KeyValuePair<JsonElement, JsonElement>(ops.CreateString("rate"), ops.CreateFloat(0.0F))
        });
        Require(ClockState.CODEC.Parse(ops, zeroRate).IsError,
            "ClockState must reject a zero clock rate.");

        JsonElement negativeRate = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(ops.CreateString("total_ticks"), ops.CreateLong(10)),
            new KeyValuePair<JsonElement, JsonElement>(ops.CreateString("rate"), ops.CreateFloat(-0.5F))
        });
        Require(ClockState.CODEC.Parse(ops, negativeRate).IsError,
            "ClockState must reject negative clock rates.");
    }

    private static void CheckPackedClockStatesCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Holder<WorldClock> clock = BuiltInRegistries.WorldClock
            .GetHolder(WorldClocks.OVERWORLD)!;
        var packed = new PackedClockStates(new Dictionary<Holder<WorldClock>, ClockState>
        {
            [clock] = new ClockState(12345, 0.25F, 0.5F, true)
        });

        JsonElement encoded = PackedClockStates.CODEC.EncodeStart(ops, packed).GetOrThrow();
        JsonElement overworld = ops.Get(encoded, "minecraft:overworld").GetOrThrow();
        Require(ops.Get(overworld, "total_ticks").GetOrThrow().GetInt64() == 12345 &&
                ops.Get(overworld, "partial_tick").GetOrThrow().GetSingle() == 0.25F &&
                ops.Get(overworld, "rate").GetOrThrow().GetSingle() == 0.5F &&
                ops.Get(overworld, "paused").GetOrThrow().GetBoolean(),
            "PackedClockStates must encode clock holders as keys and complete per-clock state values.");

        PackedClockStates decoded = PackedClockStates.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(decoded.Clocks.Count == 1 && decoded.Clocks[clock] == packed.Clocks[clock],
            "PackedClockStates codecs must round-trip using registered clock holders.");
        Require(PackedClockStates.CODEC.EncodeStart(ops, PackedClockStates.EMPTY)
                .GetOrThrow().EnumerateObject().Count() == 0,
            "PackedClockStates.EMPTY must encode as an empty map.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
