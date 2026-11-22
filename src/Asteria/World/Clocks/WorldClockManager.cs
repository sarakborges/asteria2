using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World.Timelines;

namespace Asteria.World.Clocks;

/// <summary>
/// Local, single-player clock manager. The world tick owner decides whether time advances;
/// no network synchronization or server gamerule dependency is introduced.
/// </summary>
public sealed class WorldClockManager : ClockManager
{
    private readonly Dictionary<Holder<WorldClock>, LocalClockInstance> _clocks = new();

    public bool IsDirty { get; private set; }

    /// <summary>Raised after explicit clock mutations so local derived caches can be invalidated.</summary>
    public event Action? StateChanged;

    public WorldClockManager(
        PackedClockStates? packedClockStates = null,
        IEnumerable<Timeline>? timelines = null)
    {
        foreach (Holder<WorldClock>.Reference holder in BuiltInRegistries.WorldClock.ListElements())
            _clocks.Add(holder, new LocalClockInstance());

        foreach (Timeline timeline in timelines ?? BuiltInRegistries.Timeline)
            timeline.RegisterTimeMarkers(RegisterTimeMarker);

        PackedClockStates savedStates = packedClockStates ?? PackedClockStates.EMPTY;
        foreach (KeyValuePair<Holder<WorldClock>, ClockState> entry in savedStates.Clocks)
            GetMutableInstance(entry.Key).LoadFrom(entry.Value);
    }

    public ClockInstance GetInstance(Holder<WorldClock> clock) =>
        GetMutableInstance(clock);

    /// <summary>Advances all clocks once when the owning world permits time progression.</summary>
    public void Tick(bool advanceTime)
    {
        if (!advanceTime)
            return;

        foreach (LocalClockInstance clock in _clocks.Values)
            clock.Tick();

        IsDirty = true;
    }

    public PackedClockStates PackState() =>
        new(_clocks.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.PackState()));

    public void MarkClean() => IsDirty = false;

    public void SetTotalTicks(Holder<WorldClock> clock, long totalTicks) =>
        ModifyClock(clock, instance =>
        {
            instance.TotalTicks = totalTicks;
            instance.PartialTick = 0.0F;
        });

    public MoveResult MoveToTimeMarker(
        Holder<WorldClock> clock,
        RegistryKey<ClockTimeMarker> timeMarkerId)
    {
        MoveResult result = MoveResult.NO_TIME_MARKER_FOUND;
        ModifyClock(clock, instance =>
        {
            if (!instance.TimeMarkers.TryGetValue(timeMarkerId, out ClockTimeMarker? marker))
            {
                result = MoveResult.NO_TIME_MARKER_FOUND;
            }
            else if (marker.OccursAt(instance.TotalTicks))
            {
                result = MoveResult.NOT_MOVED;
            }
            else
            {
                instance.TotalTicks = marker.ResolveTimeToMoveTo(instance.TotalTicks);
                instance.PartialTick = 0.0F;
                result = MoveResult.MOVED;
            }
        });

        return result;
    }

    public void AddTicks(Holder<WorldClock> clock, int ticks) =>
        ModifyClock(clock, instance =>
            instance.TotalTicks = Math.Max(unchecked(instance.TotalTicks + ticks), 0L));

    public void SetPaused(Holder<WorldClock> clock, bool paused) =>
        ModifyClock(clock, instance => instance.IsPaused = paused);

    public void SetRate(Holder<WorldClock> clock, float rate) =>
        ModifyClock(clock, instance => instance.Rate = rate);

    public bool IsAtTimeMarker(
        Holder<WorldClock> clock,
        RegistryKey<ClockTimeMarker> timeMarkerId)
    {
        LocalClockInstance instance = GetMutableInstance(clock);
        return instance.TimeMarkers.TryGetValue(timeMarkerId, out ClockTimeMarker? marker) &&
               marker.OccursAt(instance.TotalTicks);
    }

    public IEnumerable<RegistryKey<ClockTimeMarker>> CommandTimeMarkersForClock(
        Holder<WorldClock> clock) =>
        GetMutableInstance(clock).TimeMarkers
            .Where(pair => pair.Value.ShowInCommands)
            .Select(pair => pair.Key);

    private void RegisterTimeMarker(
        RegistryKey<ClockTimeMarker> timeMarkerId,
        ClockTimeMarker timeMarker) =>
        GetMutableInstance(timeMarker.Clock).TimeMarkers.Add(timeMarkerId, timeMarker);

    private LocalClockInstance GetMutableInstance(Holder<WorldClock> clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return _clocks.TryGetValue(clock, out LocalClockInstance? instance)
            ? instance
            : throw new InvalidOperationException(
                $"No clock initialized for definition: {clock}");
    }

    private void ModifyClock(Holder<WorldClock> clock, Action<LocalClockInstance> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action(GetMutableInstance(clock));
        IsDirty = true;
        StateChanged?.Invoke();
    }

    public enum MoveResult
    {
        NO_TIME_MARKER_FOUND,
        NOT_MOVED,
        MOVED
    }

    private sealed class LocalClockInstance : ClockInstance
    {
        public Dictionary<RegistryKey<ClockTimeMarker>, ClockTimeMarker> TimeMarkers { get; } = new();

        public long TotalTicks { get; set; }
        public float PartialTick { get; set; }
        public float Rate { get; set; } = 1.0F;
        public bool IsPaused { get; set; }

        public void LoadFrom(ClockState state)
        {
            TotalTicks = state.TotalTicks;
            PartialTick = state.PartialTick;
            Rate = state.Rate;
            IsPaused = state.Paused;
        }

        public void Tick()
        {
            if (IsPaused)
                return;

            PartialTick += Rate;
            int fullTicks = Mth.Floor(PartialTick);
            PartialTick -= fullTicks;
            TotalTicks = unchecked(TotalTicks + fullTicks);
        }

        public ClockState PackState() =>
            new(TotalTicks, PartialTick, Rate, IsPaused);
    }
}
