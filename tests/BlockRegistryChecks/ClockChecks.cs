using System.Text.Json;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.Clocks;

internal static class ClockChecks
{
    public static void Run()
    {
        CheckWorldClockCodecsAndRegistry();
        CheckTimeMarkerKeys();
        CheckOneShotMarkers();
        CheckPeriodicMarkers();
    }

    private static void CheckWorldClockCodecsAndRegistry()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Require(BuiltInRegistries.WorldClock.Count == 2,
            "The world-clock registry must contain both built-in clock definitions.");
        Require(BuiltInRegistries.WorldClock.GetHolder(WorldClocks.OVERWORLD) is not null &&
                BuiltInRegistries.WorldClock.GetHolder(WorldClocks.THE_END) is not null,
            "The overworld and the_end clock keys must resolve to registered holders.");

        Holder<WorldClock> overworld = BuiltInRegistries.WorldClock.GetHolder(WorldClocks.OVERWORLD)!;
        JsonElement encoded = WorldClock.CODEC.EncodeStart(ops, overworld).GetOrThrow();
        Require(encoded.GetString() == "asteria:overworld",
            "A world-clock holder must encode as its namespaced registry key.");
        Holder<WorldClock> decoded = WorldClock.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(decoded.Is(WorldClocks.OVERWORLD),
            "A world-clock holder codec must decode to the registered reference holder.");

        Require(WorldClock.CODEC.Parse(ops, ops.CreateString("missing_clock")).IsError,
            "World-clock holder codecs must reject unknown registry keys.");
        Require(WorldClock.DIRECT_CODEC.Parse(ops, ops.EmptyMap).GetOrThrow() == new WorldClock(),
            "A direct world-clock codec must decode the empty map to an empty definition.");
        Require(WorldClock.DIRECT_CODEC.EncodeStart(ops, new WorldClock()).GetOrThrow()
                .EnumerateObject().Count() == 0,
            "A direct world-clock codec must encode an empty map.");
    }

    private static void CheckTimeMarkerKeys()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        JsonElement encoded = ClockTimeMarker.KEY_CODEC.EncodeStart(ops, ClockTimeMarkers.DAY).GetOrThrow();
        Require(encoded.GetString() == "asteria:day" &&
                ClockTimeMarker.KEY_CODEC.Parse(ops, encoded).GetOrThrow() == ClockTimeMarkers.DAY,
            "Clock time marker keys must round-trip as namespaced registry paths.");

        RegistryKey<Registry<ClockTimeMarker>> otherRegistry =
            RegistryKey<ClockTimeMarker>.CreateRegistryKey(
                ResourceLocation.WithDefaultNamespace("other_clock_time_marker"));
        RegistryKey<ClockTimeMarker> wrongRegistryKey =
            RegistryKey<ClockTimeMarker>.Create(
                otherRegistry, ResourceLocation.WithDefaultNamespace("day"));
        Require(ClockTimeMarker.KEY_CODEC.EncodeStart(ops, wrongRegistryKey).IsError,
            "Clock time marker keys from another registry must not encode with the clock-marker key codec.");
    }

    private static void CheckOneShotMarkers()
    {
        Holder<WorldClock> clock = Holder<WorldClock>.Direct(new WorldClock());
        var marker = new ClockTimeMarker(clock, 10, null, false);
        Require(marker.GetRepetitionCount(9) == 0 &&
                marker.GetRepetitionCount(10) == 1 &&
                marker.GetRepetitionCount(100) == 1,
            "A non-periodic marker must occur once when total ticks reach its configured tick.");
        Require(marker.ResolveTimeToMoveTo(5) == 10 &&
                marker.ResolveTimeToMoveTo(100) == 10,
            "A non-periodic marker must resolve to its absolute tick.");
        Require(marker.OccursAt(10) && !marker.OccursAt(9) && !marker.OccursAt(11),
            "A non-periodic marker must occur only at its exact tick.");
    }

    private static void CheckPeriodicMarkers()
    {
        Holder<WorldClock> clock = Holder<WorldClock>.Direct(new WorldClock());
        var marker = new ClockTimeMarker(clock, 5, 20, true);
        Require(marker.GetRepetitionCount(0) == 0 &&
                marker.GetRepetitionCount(5) == 1 &&
                marker.GetRepetitionCount(24) == 1 &&
                marker.GetRepetitionCount(25) == 2,
            "Periodic markers must count every completed occurrence using quotient and remainder.");
        Require(marker.ResolveTimeToMoveTo(6) == 25 &&
                marker.ResolveTimeToMoveTo(5) == 25 &&
                marker.ResolveTimeToMoveTo(24) == 25,
            "Periodic markers must resolve to the next occurrence, advancing one full period when already on the marker.");
        Require(marker.OccursAt(5) && marker.OccursAt(25) && !marker.OccursAt(24),
            "Periodic markers must occur whenever total ticks modulo the period equals the marker tick.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
