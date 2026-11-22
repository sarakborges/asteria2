using System.Collections.ObjectModel;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.Attributes;
using Asteria.World.Clocks;

namespace Asteria.World.Timelines;

/// <summary>
/// A set of time-based environment attribute tracks and named time markers driven by one clock.
/// </summary>
public sealed class Timeline
{
    private readonly Holder<WorldClock> _clock;
    private readonly int? _periodTicks;
    private readonly IReadOnlyDictionary<IEnvironmentAttribute, IAttributeTrackDefinition> _tracks;
    private readonly IReadOnlyDictionary<RegistryKey<ClockTimeMarker>, TimeMarkerInfo> _timeMarkers;
    private readonly IReadOnlyCollection<IEnvironmentAttribute> _attributes;

    private static readonly IReadOnlyDictionary<IEnvironmentAttribute, IAttributeTrackDefinition> EmptyTracks =
        new ReadOnlyDictionary<IEnvironmentAttribute, IAttributeTrackDefinition>(
            new Dictionary<IEnvironmentAttribute, IAttributeTrackDefinition>());

    private static readonly IReadOnlyDictionary<RegistryKey<ClockTimeMarker>, TimeMarkerInfo> EmptyTimeMarkers =
        new ReadOnlyDictionary<RegistryKey<ClockTimeMarker>, TimeMarkerInfo>(
            new Dictionary<RegistryKey<ClockTimeMarker>, TimeMarkerInfo>());

    private static readonly Codec<IReadOnlyDictionary<IEnvironmentAttribute, IAttributeTrackDefinition>> TracksCodec =
        Codecs.DispatchedMap<IEnvironmentAttribute, IAttributeTrackDefinition>(
                EnvironmentAttributes.CODEC,
                static attribute => attribute.CreateTrackCodec())
            .XMap(
                static tracks => tracks.Count == 0
                    ? EmptyTracks
                    : new ReadOnlyDictionary<IEnvironmentAttribute, IAttributeTrackDefinition>(
                        tracks.ToDictionary(pair => pair.Key, pair => pair.Value)),
                static tracks => tracks);

    private static readonly Codec<IReadOnlyDictionary<RegistryKey<ClockTimeMarker>, TimeMarkerInfo>> TimeMarkersCodec =
        Codecs.DispatchedMap<RegistryKey<ClockTimeMarker>, TimeMarkerInfo>(
                ClockTimeMarker.KEY_CODEC,
                static _ => TimeMarkerInfo.CODEC)
            .XMap(
                static markers => markers.Count == 0
                    ? EmptyTimeMarkers
                    : new ReadOnlyDictionary<RegistryKey<ClockTimeMarker>, TimeMarkerInfo>(
                        markers.ToDictionary(pair => pair.Key, pair => pair.Value)),
                static markers => markers);

    private static readonly Codec<OptionalPeriodTicks> PeriodTicksCodec =
        Codecs.IntRange(1, int.MaxValue)
            .XMap(
                static value => new OptionalPeriodTicks(true, value),
                static optional => optional.Value);

    public static Codec<Timeline> DIRECT_CODEC { get; } = CreateDirectCodec();
    public static Codec<Holder<Timeline>> CODEC { get; } = CreateHolderCodec();

    public Holder<WorldClock> Clock => _clock;
    public int? PeriodTicks => _periodTicks;
    public IReadOnlyCollection<IEnvironmentAttribute> Attributes => _attributes;

    private Timeline(
        Holder<WorldClock> clock,
        int? periodTicks,
        IReadOnlyDictionary<IEnvironmentAttribute, IAttributeTrackDefinition> tracks,
        IReadOnlyDictionary<RegistryKey<ClockTimeMarker>, TimeMarkerInfo> timeMarkers)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _periodTicks = periodTicks;
        _tracks = tracks.Count == 0
            ? EmptyTracks
            : new ReadOnlyDictionary<IEnvironmentAttribute, IAttributeTrackDefinition>(
                tracks.ToDictionary(pair => pair.Key, pair => pair.Value));
        _attributes = Array.AsReadOnly(_tracks.Keys.ToArray());
        _timeMarkers = timeMarkers.Count == 0
            ? EmptyTimeMarkers
            : new ReadOnlyDictionary<RegistryKey<ClockTimeMarker>, TimeMarkerInfo>(
                timeMarkers.ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    public static Builder CreateBuilder(Holder<WorldClock> clock) => new(clock);

    public DataResult<Timeline> Validate() => ValidateInternal(this);

    private static DataResult<Timeline> ValidateInternal(Timeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        if (!timeline._periodTicks.HasValue)
            return DataResult<Timeline>.Success(timeline);

        int periodTicks = timeline._periodTicks.Value;
        foreach (KeyValuePair<RegistryKey<ClockTimeMarker>, TimeMarkerInfo> entry in timeline._timeMarkers)
        {
            int ticks = entry.Value.Ticks;
            if (ticks < 0 || ticks >= periodTicks)
            {
                return DataResult<Timeline>.Error(
                    () => $"Time Marker {entry.Key} must be in range [0; {periodTicks})");
            }
        }

        DataResult<Timeline> result = DataResult<Timeline>.Success(timeline);
        foreach (IAttributeTrackDefinition track in timeline._tracks.Values)
        {
            result = result.Apply2(
                static (current, _) => current,
                track.ValidatePeriod(periodTicks));
        }

        return result;
    }

    private static Codec<Timeline> CreateDirectCodec() =>
        RecordCodecBuilder.Create<Timeline>(instance =>
            instance.Group(
                WorldClock.CODEC.FieldOf("clock")
                    .ForGetter<Timeline>(static timeline => timeline._clock),
                PeriodTicksCodec.OptionalFieldOf("period_ticks", OptionalPeriodTicks.Empty)
                    .ForGetter<Timeline>(static timeline =>
                        OptionalPeriodTicks.FromNullable(timeline._periodTicks)),
                TracksCodec.OptionalFieldOf("tracks", EmptyTracks)
                    .ForGetter<Timeline>(static timeline => timeline._tracks),
                TimeMarkersCodec.OptionalFieldOf("time_markers", EmptyTimeMarkers)
                    .ForGetter<Timeline>(static timeline => timeline._timeMarkers))
            .Apply(instance, static (clock, period, tracks, markers) =>
                new Timeline(clock, period.ToNullable(), tracks, markers)))
        .Validate(ValidateInternal);

    private static Codec<Holder<Timeline>> CreateHolderCodec() =>
        Codecs.STRING.FlatXMap(
            name =>
            {
                ResourceLocation? location = ResourceLocation.TryParse(name);
                if (!location.HasValue)
                {
                    return DataResult<Holder<Timeline>>.Error(() =>
                        $"Invalid registry key '{name}' for {BuiltInRegistries.Timeline.Key}");
                }

                Holder<Timeline>.Reference? holder =
                    BuiltInRegistries.Timeline.GetHolder(location.Value);
                return holder is not null
                    ? DataResult<Holder<Timeline>>.Success(
                        holder, BuiltInRegistries.Timeline.RegistryLifecycle)
                    : DataResult<Holder<Timeline>>.Error(() =>
                        $"Unknown registry key '{name}' in {BuiltInRegistries.Timeline.Key}");
            },
            holder =>
            {
                ArgumentNullException.ThrowIfNull(holder);
                RegistryKey<Timeline>? key = holder.KeyOrNull;
                return key.HasValue && key.Value.IsFor(BuiltInRegistries.Timeline.Key)
                    ? DataResult<string>.Success(
                        key.Value.Location.ToString(), BuiltInRegistries.Timeline.RegistryLifecycle)
                    : DataResult<string>.Error(() =>
                        $"Unregistered timeline holder '{holder}'");
            });

    public int GetPeriodCount(ClockManager clockManager)
    {
        ArgumentNullException.ThrowIfNull(clockManager);
        if (!_periodTicks.HasValue)
            return 0;

        long totalTicks = GetTotalTicks(clockManager);
        return (int)(totalTicks / _periodTicks.Value);
    }

    public long GetCurrentTicks(ClockManager clockManager)
    {
        long totalTicks = GetTotalTicks(clockManager);
        return _periodTicks.HasValue
            ? totalTicks % _periodTicks.Value
            : totalTicks;
    }

    public long GetTotalTicks(ClockManager clockManager)
    {
        ArgumentNullException.ThrowIfNull(clockManager);
        return clockManager.GetInstance(_clock).TotalTicks;
    }

    public void RegisterTimeMarkers(Action<RegistryKey<ClockTimeMarker>, ClockTimeMarker> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        foreach (KeyValuePair<RegistryKey<ClockTimeMarker>, TimeMarkerInfo> entry in _timeMarkers)
        {
            TimeMarkerInfo info = entry.Value;
            output(entry.Key, new ClockTimeMarker(
                _clock, info.Ticks, _periodTicks, info.ShowInCommands));
        }
    }

    public IAttributeTrackSampler<TValue> CreateTrackSampler<TValue>(
        EnvironmentAttribute<TValue> attribute,
        ClockManager clockManager)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        ArgumentNullException.ThrowIfNull(clockManager);
        if (!_tracks.TryGetValue(attribute, out IAttributeTrackDefinition? track) ||
            track is not IAttributeTrack<TValue> typedTrack)
        {
            throw new InvalidOperationException($"Timeline has no track for {attribute}");
        }

        return typedTrack.BakeSampler(attribute, _clock, _periodTicks, clockManager);
    }

    public static void ValidateRegistry(
        Registry<Timeline> timelines,
        IDictionary<RegistryKey<Timeline>, Exception> loadingErrors)
    {
        ArgumentNullException.ThrowIfNull(timelines);
        ArgumentNullException.ThrowIfNull(loadingErrors);

        var timeMarkersByClock =
            new Dictionary<Holder<WorldClock>, HashSet<RegistryKey<ClockTimeMarker>>>();

        foreach (Holder<Timeline>.Reference timelineHolder in timelines.ListElements())
        {
            Timeline timeline = timelineHolder.Value;
            if (!timeMarkersByClock.TryGetValue(timeline._clock, out HashSet<RegistryKey<ClockTimeMarker>>? markers))
            {
                markers = new HashSet<RegistryKey<ClockTimeMarker>>();
                timeMarkersByClock.Add(timeline._clock, markers);
            }

            foreach (RegistryKey<ClockTimeMarker> marker in timeline._timeMarkers.Keys)
            {
                if (!markers.Add(marker))
                {
                    loadingErrors[timelineHolder.Key] = new InvalidOperationException(
                        $"{marker} was defined multiple times in {timeline._clock.GetRegisteredName()}");
                }
            }
        }
    }

    private static IReadOnlyDictionary<TKey, TValue> BuildMap<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>> entries)
        where TKey : notnull
    {
        var dictionary = new Dictionary<TKey, TValue>();
        foreach (KeyValuePair<TKey, TValue> entry in entries)
            dictionary.Add(entry.Key, entry.Value);
        return new ReadOnlyDictionary<TKey, TValue>(dictionary);
    }

    public sealed class Builder
    {
        private readonly Holder<WorldClock> _clock;
        private int? _periodTicks;
        private readonly List<KeyValuePair<IEnvironmentAttribute, IAttributeTrackDefinition>> _tracks = new();
        private readonly List<KeyValuePair<RegistryKey<ClockTimeMarker>, TimeMarkerInfo>> _timeMarkers = new();

        internal Builder(Holder<WorldClock> clock) =>
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));

        public Builder SetPeriodTicks(int periodTicks)
        {
            _periodTicks = periodTicks;
            return this;
        }

        public Builder AddModifierTrack<TValue, TArgument>(
            EnvironmentAttribute<TValue> attribute,
            AttributeModifier<TValue, TArgument> modifier,
            Action<KeyframeTrack<TArgument>.Builder> configureTrack)
        {
            ArgumentNullException.ThrowIfNull(attribute);
            ArgumentNullException.ThrowIfNull(modifier);
            ArgumentNullException.ThrowIfNull(configureTrack);
            attribute.Type.CheckAllowedModifier(modifier);

            KeyframeTrack<TArgument>.Builder argumentTrack = KeyframeTrack<TArgument>.CreateBuilder();
            configureTrack(argumentTrack);
            _tracks.Add(new KeyValuePair<IEnvironmentAttribute, IAttributeTrackDefinition>(
                attribute, new AttributeTrack<TValue, TArgument>(modifier, argumentTrack.Build())));
            return this;
        }

        public Builder AddTrack<TValue>(
            EnvironmentAttribute<TValue> attribute,
            Action<KeyframeTrack<TValue>.Builder> configureTrack) =>
            AddModifierTrack(attribute, AttributeModifiers.Override<TValue>(), configureTrack);

        public Builder AddTimeMarker(RegistryKey<ClockTimeMarker> id, int ticks) =>
            AddTimeMarker(id, ticks, false);

        public Builder AddTimeMarker(
            RegistryKey<ClockTimeMarker> id,
            int ticks,
            bool showInCommands)
        {
            _timeMarkers.Add(new KeyValuePair<RegistryKey<ClockTimeMarker>, TimeMarkerInfo>(
                id, new TimeMarkerInfo(ticks, showInCommands)));
            return this;
        }

        public Timeline Build()
        {
            IReadOnlyDictionary<IEnvironmentAttribute, IAttributeTrackDefinition> tracks =
                BuildMap(_tracks);
            IReadOnlyDictionary<RegistryKey<ClockTimeMarker>, TimeMarkerInfo> timeMarkers =
                BuildMap(_timeMarkers);
            return new Timeline(_clock, _periodTicks, tracks, timeMarkers);
        }
    }

    private readonly record struct OptionalPeriodTicks(bool HasValue, int Value)
    {
        public static OptionalPeriodTicks Empty { get; } = new(false, 0);

        public static OptionalPeriodTicks FromNullable(int? value) =>
            value.HasValue ? new OptionalPeriodTicks(true, value.Value) : Empty;

        public int? ToNullable() => HasValue ? Value : null;
    }

    private readonly record struct TimeMarkerInfo(int Ticks, bool ShowInCommands)
    {
        private static readonly Codec<int> NonNegativeInt =
            Codecs.IntRange(0, int.MaxValue);

        private static readonly Codec<TimeMarkerInfo> FullCodec =
            RecordCodecBuilder.Create<TimeMarkerInfo>(instance =>
                instance.Group(
                    NonNegativeInt.FieldOf("ticks")
                        .ForGetter<TimeMarkerInfo>(static marker => marker.Ticks),
                    Codecs.BOOL.OptionalFieldOf("show_in_commands", false)
                        .ForGetter<TimeMarkerInfo>(static marker => marker.ShowInCommands))
                .Apply(instance, static (ticks, show) => new TimeMarkerInfo(ticks, show)));

        public static Codec<TimeMarkerInfo> CODEC { get; } = new TimeMarkerInfoCodec();

        private sealed class TimeMarkerInfoCodec : Codec<TimeMarkerInfo>
        {
            public override DataResult<TData> Encode<TData>(
                TimeMarkerInfo input,
                DynamicOps<TData> ops,
                TData prefix) =>
                input.ShowInCommands
                    ? FullCodec.Encode(input, ops, prefix)
                    : NonNegativeInt.Encode(input.Ticks, ops, prefix);

            public override DataResult<Pair<TimeMarkerInfo, TData>> Decode<TData>(
                DynamicOps<TData> ops,
                TData input)
            {
                DataResult<Pair<TimeMarkerInfo, TData>> compact =
                    NonNegativeInt.Decode(ops, input).Map(pair =>
                        new Pair<TimeMarkerInfo, TData>(
                            new TimeMarkerInfo(pair.First, false), pair.Second));
                if (compact.IsSuccess)
                    return compact;

                DataResult<Pair<TimeMarkerInfo, TData>> expanded =
                    FullCodec.Decode(ops, input);
                if (expanded.IsSuccess)
                    return expanded;
                if (compact.HasResultOrPartial)
                    return compact;
                if (expanded.HasResultOrPartial)
                    return expanded;

                return DataResult<Pair<TimeMarkerInfo, TData>>.Error(() =>
                    $"Failed to parse either. First: {compact.ErrorMessage}; Second: {expanded.ErrorMessage}");
            }
        }
    }
}
