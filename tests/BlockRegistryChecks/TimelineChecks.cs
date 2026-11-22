using System.Text.Json;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.Attributes;
using Asteria.World.Clocks;
using Asteria.World.Timelines;

internal static class TimelineChecks
{
    private sealed class TestClockInstance : ClockInstance
    {
        public long TotalTicks { get; set; }
        public float PartialTick => 0.0F;
        public float Rate => 1.0F;
        public bool IsPaused => false;
    }

    private sealed class TestClockManager : ClockManager
    {
        private readonly ClockInstance _instance;
        public TestClockManager(ClockInstance instance) => _instance = instance;
        public ClockInstance GetInstance(Holder<WorldClock> clock) => _instance;
    }

    public static void Run()
    {
        CheckDefaultAndDirectCodecs();
        CheckTrackAndTimeMarkerCodecForms();
        CheckPeriodValidation();
        CheckClockSamplingAndMarkers();
        CheckDuplicateTrackAndMarkerIds();
        CheckRegistryMarkerUniqueness();
        CheckTimelineHolderCodecRejectsUnknownKey();
    }

    private static void CheckDefaultAndDirectCodecs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Holder<WorldClock> clock = BuiltInRegistries.WorldClock.GetHolder(WorldClocks.OVERWORLD)!;
        Timeline empty = Timeline.CreateBuilder(clock).Build();

        JsonElement emptyEncoded = Timeline.DIRECT_CODEC.EncodeStart(ops, empty).GetOrThrow();
        Require(ops.Get(emptyEncoded, "clock").GetOrThrow().GetString() == "asteria:overworld",
            "A direct timeline must encode its registered clock holder.");
        Require(!ops.Get(emptyEncoded, "period_ticks").TryGetResult(out _) &&
                !ops.Get(emptyEncoded, "tracks").TryGetResult(out _) &&
                !ops.Get(emptyEncoded, "time_markers").TryGetResult(out _),
            "Absent period and empty track/marker maps must be omitted.");

        Timeline decodedEmpty = Timeline.DIRECT_CODEC.Parse(ops, emptyEncoded).GetOrThrow();
        Require(decodedEmpty.PeriodTicks is null && decodedEmpty.Attributes.Count == 0,
            "A timeline with omitted optional fields must decode to an unperiodic empty timeline.");

        Require(Timeline.CODEC.Parse(ops, ops.CreateString("asteria:missing_timeline")).IsError,
            "The timeline holder codec must reject a key not present in the timeline registry.");
    }

    private static void CheckTrackAndTimeMarkerCodecForms()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Holder<WorldClock> clock = BuiltInRegistries.WorldClock.GetHolder(WorldClocks.OVERWORLD)!;
        Timeline timeline = Timeline.CreateBuilder(clock)
            .SetPeriodTicks(20)
            .AddTrack(
                EnvironmentAttributes.FOG_END_DISTANCE,
                track => track.AddKeyframe(0, 100.0F).AddKeyframe(20, 200.0F))
            .AddTimeMarker(ClockTimeMarkers.DAY, 0)
            .AddTimeMarker(ClockTimeMarkers.NOON, 10, true)
            .Build();

        JsonElement encoded = Timeline.DIRECT_CODEC.EncodeStart(ops, timeline).GetOrThrow();
        JsonElement tracks = ops.Get(encoded, "tracks").GetOrThrow();
        Require(ops.Get(tracks, "asteria:visual/fog_end_distance").IsSuccess,
            "Timeline tracks must be keyed by registered environment-attribute identifiers.");

        JsonElement timeMarkers = ops.Get(encoded, "time_markers").GetOrThrow();
        Require(ops.Get(timeMarkers, "asteria:day").GetOrThrow().GetInt32() == 0,
            "A marker without show_in_commands must use the compact integer form.");
        JsonElement expandedNoon = ops.Get(timeMarkers, "asteria:noon").GetOrThrow();
        Require(ops.Get(expandedNoon, "ticks").GetOrThrow().GetInt32() == 10 &&
                ops.Get(expandedNoon, "show_in_commands").GetOrThrow().GetBoolean(),
            "A marker shown in commands must use the expanded record form.");

        Timeline decoded = Timeline.DIRECT_CODEC.Parse(ops, encoded).GetOrThrow();
        Require(decoded.PeriodTicks == 20 && decoded.Attributes.Count == 1,
            "Timeline direct codecs must round-trip the period and heterogeneous attribute track map.");
        List<ClockTimeMarker> decodedMarkers = new();
        decoded.RegisterTimeMarkers((_, marker) => decodedMarkers.Add(marker));
        Require(decodedMarkers.Count == 2 &&
                decodedMarkers.All(marker => marker.PeriodTicks == 20) &&
                decodedMarkers.Single(marker => marker.ShowInCommands).Ticks == 10,
            "Decoded time markers must inherit the owning timeline clock, period and visibility flag.");
    }

    private static void CheckPeriodValidation()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Holder<WorldClock> clock = BuiltInRegistries.WorldClock.GetHolder(WorldClocks.OVERWORLD)!;

        Timeline validEndpoint = Timeline.CreateBuilder(clock)
            .SetPeriodTicks(10)
            .AddTrack(EnvironmentAttributes.FOG_END_DISTANCE,
                track => track.AddKeyframe(0, 0.0F).AddKeyframe(10, 1.0F))
            .Build();
        Require(Timeline.DIRECT_CODEC.EncodeStart(ops, validEndpoint).IsSuccess,
            "Timeline validation must allow keyframes at the inclusive period endpoint.");

        Timeline trackOutside = Timeline.CreateBuilder(clock)
            .SetPeriodTicks(10)
            .AddTrack(EnvironmentAttributes.FOG_END_DISTANCE,
                track => track.AddKeyframe(0, 0.0F).AddKeyframe(11, 1.0F))
            .Build();
        Require(Timeline.DIRECT_CODEC.EncodeStart(ops, trackOutside).IsError,
            "Timeline validation must reject keyframes beyond the inclusive period endpoint.");

        Timeline markerAtPeriod = Timeline.CreateBuilder(clock)
            .SetPeriodTicks(10)
            .AddTimeMarker(ClockTimeMarkers.DAY, 10)
            .Build();
        DataResult<JsonElement> markerResult = Timeline.DIRECT_CODEC.EncodeStart(ops, markerAtPeriod);
        Require(markerResult.IsError &&
                markerResult.ErrorMessage == "Time Marker ResourceKey[asteria:clock_time_marker / asteria:day] must be in range [0; 10)",
            "Timeline marker validation must reject the exclusive period endpoint.");

        Timeline zeroPeriod = Timeline.CreateBuilder(clock).SetPeriodTicks(0).Build();
        Require(Timeline.DIRECT_CODEC.EncodeStart(ops, zeroPeriod).IsError,
            "Timeline codecs must reject a non-positive period.");
    }

    private static void CheckClockSamplingAndMarkers()
    {
        Holder<WorldClock> clock = BuiltInRegistries.WorldClock.GetHolder(WorldClocks.OVERWORLD)!;
        var clockInstance = new TestClockInstance { TotalTicks = 5 };
        var clockManager = new TestClockManager(clockInstance);
        Timeline timeline = Timeline.CreateBuilder(clock)
            .SetPeriodTicks(20)
            .AddTrack(EnvironmentAttributes.FOG_END_DISTANCE,
                track => track.AddKeyframe(0, 100.0F).AddKeyframe(10, 200.0F))
            .AddTimeMarker(ClockTimeMarkers.DAY, 0)
            .Build();

        Require(timeline.GetTotalTicks(clockManager) == 5 &&
                timeline.GetCurrentTicks(clockManager) == 5 &&
                timeline.GetPeriodCount(clockManager) == 0,
            "Timeline clock queries must use total ticks, modulo period, and integer period count.");

        IAttributeTrackSampler<float> sampler =
            timeline.CreateTrackSampler(EnvironmentAttributes.FOG_END_DISTANCE, clockManager);
        Require(sampler.ApplyTimeBased(999.0F, 1) == 150.0F,
            "Timeline track samplers must sample through the associated clock and override the base value.");

        clockInstance.TotalTicks = 9;
        Require(sampler.ApplyTimeBased(999.0F, 1) == 150.0F &&
                sampler.ApplyTimeBased(999.0F, 2) == 190.0F,
            "Timeline track samplers must preserve cacheTickId semantics while reading the selected clock.");

        List<(RegistryKey<ClockTimeMarker> Key, ClockTimeMarker Marker)> registered = new();
        timeline.RegisterTimeMarkers((key, marker) => registered.Add((key, marker)));
        Require(registered.Count == 1 &&
                registered[0].Marker.Clock.Is(WorldClocks.OVERWORLD) &&
                registered[0].Marker.PeriodTicks == 20,
            "Timeline time-marker registration must retain its clock holder and optional period.");
    }

    private static void CheckDuplicateTrackAndMarkerIds()
    {
        Holder<WorldClock> clock = BuiltInRegistries.WorldClock.GetHolder(WorldClocks.OVERWORLD)!;

        Timeline.Builder duplicateTrackBuilder = Timeline.CreateBuilder(clock)
            .AddTrack(EnvironmentAttributes.FOG_END_DISTANCE, track => track.AddKeyframe(0, 1.0F))
            .AddTrack(EnvironmentAttributes.FOG_END_DISTANCE, track => track.AddKeyframe(0, 2.0F));
        RequireThrows<ArgumentException>(() => duplicateTrackBuilder.Build(),
            "Timeline builder must reject duplicate tracks for the same attribute.");

        Timeline.Builder duplicateMarkerBuilder = Timeline.CreateBuilder(clock)
            .AddTimeMarker(ClockTimeMarkers.DAY, 0)
            .AddTimeMarker(ClockTimeMarkers.DAY, 1);
        RequireThrows<ArgumentException>(() => duplicateMarkerBuilder.Build(),
            "Timeline builder must reject duplicate time-marker keys.");
    }

    private static void CheckRegistryMarkerUniqueness()
    {
        Holder<WorldClock> clock = BuiltInRegistries.WorldClock.GetHolder(WorldClocks.OVERWORLD)!;
        Timeline first = Timeline.CreateBuilder(clock).AddTimeMarker(ClockTimeMarkers.NOON, 100).Build();
        Timeline second = Timeline.CreateBuilder(clock).AddTimeMarker(ClockTimeMarkers.NOON, 200).Build();
        var registry = new Registry<Timeline>(ResourceLocation.WithDefaultNamespace("timeline_test"));
        RegistryKey<Timeline> firstKey = RegistryKey<Timeline>.Create(
            registry.Key, ResourceLocation.WithDefaultNamespace("first"));
        RegistryKey<Timeline> secondKey = RegistryKey<Timeline>.Create(
            registry.Key, ResourceLocation.WithDefaultNamespace("second"));
        registry.Register(firstKey, first, RegistrationInfo.BuiltIn);
        registry.Register(secondKey, second, RegistrationInfo.BuiltIn);
        registry.Freeze();

        var errors = new Dictionary<RegistryKey<Timeline>, Exception>();
        Timeline.ValidateRegistry(registry, errors);
        Require(errors.Count == 1 && errors.ContainsKey(secondKey) &&
                errors[secondKey].Message.Contains("was defined multiple times", StringComparison.Ordinal),
            "Timeline registry validation must reject a time-marker key reused for the same clock.");
    }

    private static void CheckTimelineHolderCodecRejectsUnknownKey()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Require(Timeline.CODEC.Parse(ops, ops.CreateString("asteria:unknown")).IsError,
            "Timeline.CODEC must only accept holders already present in the timeline registry.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(message, exception);
        }

        throw new InvalidOperationException(message);
    }
}
