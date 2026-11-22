using Asteria.Content.Registries;
using Asteria.World.Clocks;
using Asteria.World.Timelines;

namespace Asteria.World.Attributes;

/// <summary>Type-erased sampling contract exposed by a timeline track.</summary>
public interface IAttributeTrackSampler<TValue>
{
    TValue ApplyTimeBased(TValue baseValue, int cacheTickId);
}

/// <summary>Samples a modifier argument from its registered world clock and caches it per caller tick ID.</summary>
public sealed class AttributeTrackSampler<TValue, TArgument> : IAttributeTrackSampler<TValue>
{
    private readonly AttributeModifier<TValue, TArgument> _modifier;
    private readonly KeyframeTrackSampler<TArgument> _argumentSampler;
    private bool _hasCachedArgument;
    private int _cachedTickId;
    private TArgument _cachedArgument = default!;

    private readonly Holder<WorldClock> _clock;
    private readonly ClockManager _clockManager;

    public AttributeTrackSampler(
        AttributeTrack<TValue, TArgument> track,
        EnvironmentAttribute<TValue> attribute,
        Holder<WorldClock> clock,
        int? periodTicks,
        ClockManager clockManager)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(attribute);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(clockManager);

        _modifier = track.Modifier;
        _clock = clock;
        _clockManager = clockManager;
        _argumentSampler = track.ArgumentTrack.BakeSampler(
            periodTicks,
            track.Modifier.ArgumentKeyframeLerp(attribute));
    }

    public TValue ApplyTimeBased(TValue baseValue, int cacheTickId)
    {
        if (!_hasCachedArgument || cacheTickId != _cachedTickId)
        {
            _cachedTickId = cacheTickId;
            _cachedArgument = _argumentSampler.Sample(
                _clockManager.GetInstance(_clock).TotalTicks);
            _hasCachedArgument = true;
        }

        return _modifier.Apply(baseValue, _cachedArgument);
    }
}
