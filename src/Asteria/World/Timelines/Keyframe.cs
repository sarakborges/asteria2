using Asteria.Serialization;

namespace Asteria.World.Timelines;

/// <summary>A value active at a specific tick within a timeline period.</summary>
public sealed record Keyframe<T>(int Ticks, T Value)
{
    public static Codec<Keyframe<T>> CreateCodec(Codec<T> valueCodec)
    {
        ArgumentNullException.ThrowIfNull(valueCodec);

        return RecordCodecBuilder.Create<Keyframe<T>>(instance =>
            instance.Group(
                Codecs.IntRange(0, int.MaxValue)
                    .FieldOf("ticks")
                    .ForGetter<Keyframe<T>>(static keyframe => keyframe.Ticks),
                valueCodec.FieldOf("value")
                    .ForGetter<Keyframe<T>>(static keyframe => keyframe.Value))
            .Apply(instance, static (ticks, value) => new Keyframe<T>(ticks, value)));
    }
}
