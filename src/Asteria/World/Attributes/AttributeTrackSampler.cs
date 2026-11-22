using Asteria.World.Timelines;

namespace Asteria.World.Attributes;

/// <summary>
/// Samples modifier arguments at explicit world ticks and caches the argument per caller tick ID.
/// The owner supplies time because Asteria does not yet have WorldClock or ClockManager.
/// </summary>
public sealed class AttributeTrackSampler<TValue, TArgument>
{
    private readonly AttributeModifier<TValue, TArgument> _modifier;
    private readonly KeyframeTrackSampler<TArgument> _argumentSampler;
    private bool _hasCachedArgument;
    private int _cachedTickId;
    private TArgument _cachedArgument = default!;

    public AttributeTrackSampler(
        AttributeTrack<TValue, TArgument> track,
        EnvironmentAttribute<TValue> attribute,
        int? periodTicks)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(attribute);

        _modifier = track.Modifier;
        _argumentSampler = track.ArgumentTrack.BakeSampler(
            periodTicks,
            track.Modifier.ArgumentKeyframeLerp(attribute));
    }

    public TValue ApplyTimeBased(
        TValue baseValue,
        long totalTicks,
        int cacheTickId)
    {
        if (!_hasCachedArgument || cacheTickId != _cachedTickId)
        {
            _cachedTickId = cacheTickId;
            _cachedArgument = _argumentSampler.Sample(totalTicks);
            _hasCachedArgument = true;
        }

        return _modifier.Apply(baseValue, _cachedArgument);
    }
}
