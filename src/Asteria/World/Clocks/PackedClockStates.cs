using System.Collections.ObjectModel;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.Clocks;

/// <summary>Immutable snapshot of persisted state for registered world clocks.</summary>
public sealed class PackedClockStates
{
    private static readonly IReadOnlyDictionary<Holder<WorldClock>, ClockState> EmptyClocks =
        new ReadOnlyDictionary<Holder<WorldClock>, ClockState>(
            new Dictionary<Holder<WorldClock>, ClockState>());

    public static PackedClockStates EMPTY { get; } = new(EmptyClocks);

    public static Codec<PackedClockStates> CODEC { get; } =
        Codecs.DispatchedMap<Holder<WorldClock>, ClockState>(
                WorldClock.CODEC,
                static _ => ClockState.CODEC)
            .XMap(
                static clocks => new PackedClockStates(clocks),
                static packed => packed.Clocks);

    public IReadOnlyDictionary<Holder<WorldClock>, ClockState> Clocks { get; }

    public PackedClockStates(
        IReadOnlyDictionary<Holder<WorldClock>, ClockState> clocks)
    {
        ArgumentNullException.ThrowIfNull(clocks);
        Clocks = clocks.Count == 0
            ? EmptyClocks
            : new ReadOnlyDictionary<Holder<WorldClock>, ClockState>(
                clocks.ToDictionary(pair => pair.Key, pair => pair.Value));
    }
}
