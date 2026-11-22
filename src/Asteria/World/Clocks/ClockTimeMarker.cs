using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.Clocks;

/// <summary>A named position in a clock, optionally recurring at a fixed period.</summary>
public sealed record ClockTimeMarker(
    Holder<WorldClock> Clock,
    int Ticks,
    int? PeriodTicks,
    bool ShowInCommands)
{
    public static Codec<RegistryKey<ClockTimeMarker>> KEY_CODEC { get; } =
        Codecs.STRING.FlatXMap(
            name =>
            {
                ResourceLocation? location = ResourceLocation.TryParse(name);
                if (!location.HasValue)
                {
                    return DataResult<RegistryKey<ClockTimeMarker>>.Error(() =>
                        $"Invalid clock time marker key '{name}'");
                }

                return DataResult<RegistryKey<ClockTimeMarker>>.Success(
                    RegistryKey<ClockTimeMarker>.Create(
                        ClockTimeMarkers.ROOT_ID, location.Value));
            },
            key => key.IsFor(ClockTimeMarkers.ROOT_ID)
                ? DataResult<string>.Success(key.Location.ToString())
                : DataResult<string>.Error(() =>
                    $"Clock time marker key '{key}' does not belong to {ClockTimeMarkers.ROOT_ID}")
        );

    public long GetRepetitionCount(long totalTicks)
    {
        if (!PeriodTicks.HasValue)
            return totalTicks >= Ticks ? 1L : 0L;

        int periodTicks = PeriodTicks.Value;
        return totalTicks / periodTicks +
            (totalTicks % periodTicks >= Ticks ? 1L : 0L);
    }

    public long ResolveTimeToMoveTo(long totalTicks)
    {
        if (!PeriodTicks.HasValue)
            return Ticks;

        int periodTicks = PeriodTicks.Value;
        long duration = Ticks - totalTicks % periodTicks;
        return totalTicks + (duration > 0L ? duration : periodTicks + duration);
    }

    public bool OccursAt(long totalTicks) =>
        !PeriodTicks.HasValue
            ? Ticks == totalTicks
            : Ticks == totalTicks % PeriodTicks.Value;
}
