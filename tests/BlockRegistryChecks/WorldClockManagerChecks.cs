using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.World.Clocks;
using Asteria.World.Timelines;

internal static class WorldClockManagerChecks
{
    public static void Run()
    {
        CheckInitializesAndLoadsState();
        CheckTickingAndMutations();
        CheckMarkerOperations();
        CheckDirtyStateAndUnknownClock();
    }

    private static void CheckInitializesAndLoadsState()
    {
        Holder<WorldClock> overworld = BuiltInRegistries.WorldClock.GetHolder(WorldClocks.OVERWORLD)!;
        Holder<WorldClock> theEnd = BuiltInRegistries.WorldClock.GetHolder(WorldClocks.THE_END)!;
        Timeline timeline = Timeline.CreateBuilder(overworld)
            .SetPeriodTicks(24000)
            .AddTimeMarker(ClockTimeMarkers.DAY, 1000, true)
            .AddTimeMarker(ClockTimeMarkers.NOON, 6000)
            .Build();
        var saved = new PackedClockStates(new Dictionary<Holder<WorldClock>, ClockState>
        {
            [overworld] = new ClockState(12000, 0.75F, 0.5F, true)
        });

        var manager = new WorldClockManager(saved, new[] { timeline });
        ClockInstance restored = manager.GetInstance(overworld);
        ClockInstance newClock = manager.GetInstance(theEnd);
        Require(restored.TotalTicks == 12000 && restored.PartialTick == 0.75F &&
                restored.Rate == 0.5F && restored.IsPaused,
            "WorldClockManager must restore registered clock state from PackedClockStates.");
        Require(newClock.TotalTicks == 0 && newClock.PartialTick == 0.0F &&
                newClock.Rate == 1.0F && !newClock.IsPaused,
            "WorldClockManager must initialize clocks absent from persisted state with vanilla defaults.");
        Require(manager.CommandTimeMarkersForClock(overworld).SequenceEqual(new[] { ClockTimeMarkers.DAY }),
            "Only timeline markers with show_in_commands must appear in the command marker list.");
    }

    private static void CheckTickingAndMutations()
    {
        Holder<WorldClock> clock = BuiltInRegistries.WorldClock.GetHolder(WorldClocks.OVERWORLD)!;
        var manager = new WorldClockManager();
        manager.SetPaused(clock, false);
        manager.SetTotalTicks(clock, 100);
        manager.SetRate(clock, 0.5F);
        manager.MarkClean();

        manager.Tick(advanceTime: false);
        Require(manager.GetInstance(clock).TotalTicks == 100 && !manager.IsDirty,
            "A world that disables time advancement must not tick clocks or dirty their saved state.");

        manager.Tick(advanceTime: true);
        Require(manager.GetInstance(clock).TotalTicks == 100 &&
                manager.GetInstance(clock).PartialTick == 0.5F && manager.IsDirty,
            "Clock ticks must accumulate fractional rate in partial_tick.");
        manager.MarkClean();

        manager.Tick(advanceTime: true);
        Require(manager.GetInstance(clock).TotalTicks == 101 &&
                manager.GetInstance(clock).PartialTick == 0.0F,
            "The integral part of accumulated partial_tick must advance total_ticks.");

        manager.SetRate(clock, 1.25F);
        manager.Tick(advanceTime: true);
        Require(manager.GetInstance(clock).TotalTicks == 102 &&
                Math.Abs(manager.GetInstance(clock).PartialTick - 0.25F) < 0.000001F,
            "Clock ticking must use floor and retain the fractional remainder.");

        manager.AddTicks(clock, -500);
        Require(manager.GetInstance(clock).TotalTicks == 0,
            "Adding negative ticks must clamp total_ticks to zero.");
    }

    private static void CheckMarkerOperations()
    {
        Holder<WorldClock> clock = BuiltInRegistries.WorldClock.GetHolder(WorldClocks.OVERWORLD)!;
        Timeline timeline = Timeline.CreateBuilder(clock)
            .SetPeriodTicks(24000)
            .AddTimeMarker(ClockTimeMarkers.NOON, 6000)
            .Build();
        var manager = new WorldClockManager(timelines: new[] { timeline });

        Require(manager.MoveToTimeMarker(clock, ClockTimeMarkers.NOON) ==
                WorldClockManager.MoveResult.MOVED &&
                manager.GetInstance(clock).TotalTicks == 6000 &&
                manager.GetInstance(clock).PartialTick == 0.0F,
            "Moving to a periodic time marker must resolve the next occurrence and clear partial_tick.");
        Require(manager.IsAtTimeMarker(clock, ClockTimeMarkers.NOON),
            "IsAtTimeMarker must identify the registered marker at the current clock tick.");
        Require(manager.MoveToTimeMarker(clock, ClockTimeMarkers.NOON) ==
                WorldClockManager.MoveResult.NOT_MOVED,
            "Moving to a marker that already occurs at the current tick must not change time.");
        Require(manager.MoveToTimeMarker(clock, ClockTimeMarkers.DAY) ==
                WorldClockManager.MoveResult.NO_TIME_MARKER_FOUND,
            "Moving to an unregistered time marker must return NO_TIME_MARKER_FOUND.");

        manager.SetTotalTicks(clock, 6001);
        Require(!manager.IsAtTimeMarker(clock, ClockTimeMarkers.NOON),
            "IsAtTimeMarker must return false away from the marker tick.");
        Require(manager.MoveToTimeMarker(clock, ClockTimeMarkers.NOON) ==
                WorldClockManager.MoveResult.MOVED &&
                manager.GetInstance(clock).TotalTicks == 30000,
            "Moving forward from just after noon must select the next period occurrence.");
    }

    private static void CheckDirtyStateAndUnknownClock()
    {
        Holder<WorldClock> clock = BuiltInRegistries.WorldClock.GetHolder(WorldClocks.OVERWORLD)!;
        var manager = new WorldClockManager();
        int invalidations = 0;
        manager.StateChanged += () => invalidations++;

        manager.SetPaused(clock, true);
        manager.SetRate(clock, 2.0F);
        manager.SetTotalTicks(clock, 50);
        Require(invalidations == 3 && manager.IsDirty,
            "Explicit clock mutations must dirty saved state and notify local derived-cache owners.");
        PackedClockStates snapshot = manager.PackState();
        Require(snapshot.Clocks[clock] == new ClockState(50, 0.0F, 2.0F, true),
            "Clock manager snapshots must preserve all mutable clock fields.");

        manager.MarkClean();
        Require(!manager.IsDirty,
            "MarkClean must clear the dirty flag after a successful local save.");

        Holder<WorldClock> unknown = Holder<WorldClock>.Direct(new WorldClock());
        try
        {
            manager.GetInstance(unknown);
        }
        catch (InvalidOperationException exception) when (
            exception.Message == $"No clock initialized for definition: {unknown}")
        {
            return;
        }

        throw new InvalidOperationException(
            "WorldClockManager must reject unregistered clock holders with the reference diagnostic.");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

}
