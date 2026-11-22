using Asteria.Serialization;

namespace Asteria.World.Clocks;

/// <summary>Persistable state for one registered world clock.</summary>
public sealed record ClockState(
    long TotalTicks,
    float PartialTick,
    float Rate,
    bool Paused)
{
    public static Codec<ClockState> CODEC { get; } =
        RecordCodecBuilder.Create<ClockState>(instance =>
            instance.Group(
                Codecs.LONG.FieldOf("total_ticks")
                    .ForGetter<ClockState>(static state => state.TotalTicks),
                Codecs.FLOAT.OptionalFieldOf("partial_tick", 0.0F)
                    .ForGetter<ClockState>(static state => state.PartialTick),
                Codecs.POSITIVE_FLOAT.OptionalFieldOf("rate", 1.0F)
                    .ForGetter<ClockState>(static state => state.Rate),
                Codecs.BOOL.OptionalFieldOf("paused", false)
                    .ForGetter<ClockState>(static state => state.Paused))
            .Apply(instance, static (totalTicks, partialTick, rate, paused) =>
                new ClockState(totalTicks, partialTick, rate, paused)));
}
