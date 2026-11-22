using Asteria.Serialization;
using Asteria.World.Timelines;

namespace Asteria.World.Attributes;

/// <summary>Type-erased attribute track used by the modifier-dispatch codec.</summary>
public interface IAttributeTrack<TValue>
{
    IAttributeModifier<TValue> Modifier { get; }
    DataResult<IAttributeTrack<TValue>> ValidatePeriod(int periodTicks);
}

/// <summary>A keyframed modifier argument track for one environment attribute value type.</summary>
public sealed record AttributeTrack<TValue, TArgument>(
    AttributeModifier<TValue, TArgument> Modifier,
    KeyframeTrack<TArgument> ArgumentTrack) : IAttributeTrack<TValue>
{
    IAttributeModifier<TValue> IAttributeTrack<TValue>.Modifier => Modifier;

    public DataResult<IAttributeTrack<TValue>> ValidatePeriod(int periodTicks) =>
        KeyframeTrack<TArgument>.ValidatePeriod(ArgumentTrack, periodTicks)
            .Map<IAttributeTrack<TValue>>(_ => this);

    public AttributeTrackSampler<TValue, TArgument> BakeSampler(
        EnvironmentAttribute<TValue> attribute,
        int? periodTicks)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        return new AttributeTrackSampler<TValue, TArgument>(this, attribute, periodTicks);
    }
}

/// <summary>Codec factory for the modifier-dispatched attribute-track schema.</summary>
public static class AttributeTrack
{
    public static Codec<IAttributeTrack<TValue>> CreateCodec<TValue>(
        EnvironmentAttribute<TValue> attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);

        MapCodec<IAttributeModifier<TValue>> modifierCodec =
            attribute.Type.ModifierCodec.OptionalFieldOf(
                "modifier",
                AttributeModifiers.Override<TValue>());

        return MapCodec<IAttributeTrack<TValue>>.Dispatch(
            modifierCodec,
            static track => DataResult<IAttributeModifier<TValue>>.Success(track.Modifier),
            modifier => modifier.CreateTrackCodec(attribute))
            .Codec();
    }

    public static DataResult<IAttributeTrack<TValue>> ValidatePeriod<TValue>(
        IAttributeTrack<TValue> track,
        int periodTicks)
    {
        ArgumentNullException.ThrowIfNull(track);
        return track.ValidatePeriod(periodTicks);
    }
}
