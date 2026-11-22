using Asteria.World.Attributes;

namespace Asteria.World.Timelines;

/// <summary>Precomputed segments for sampling an ordered keyframe track.</summary>
public sealed class KeyframeTrackSampler<T>
{
    private readonly int? _periodTicks;
    private readonly LerpFunction<T> _lerp;
    private readonly IReadOnlyList<Segment> _segments;

    internal KeyframeTrackSampler(
        KeyframeTrack<T> track,
        int? periodTicks,
        LerpFunction<T> lerp)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(lerp);
        _periodTicks = periodTicks;
        _lerp = lerp;
        _segments = BakeSegments(track, periodTicks);
    }

    private static IReadOnlyList<Segment> BakeSegments(
        KeyframeTrack<T> track,
        int? periodTicks)
    {
        IReadOnlyList<Keyframe<T>> keyframes = track.Keyframes;
        if (keyframes.Count == 1)
        {
            T value = keyframes[0].Value;
            return Array.AsReadOnly(new[]
            {
                new Segment(EasingType.CONSTANT, value, 0, value, 0)
            });
        }

        var segments = new List<Segment>();
        if (periodTicks.HasValue)
        {
            Keyframe<T> first = keyframes[0];
            Keyframe<T> last = keyframes[^1];

            segments.Add(new Segment(
                track.EasingType,
                last.Value,
                unchecked(last.Ticks - periodTicks.Value),
                first.Value,
                first.Ticks));
            AddSegmentsFromKeyframes(track, keyframes, segments);
            segments.Add(new Segment(
                track.EasingType,
                last.Value,
                last.Ticks,
                first.Value,
                unchecked(first.Ticks + periodTicks.Value)));
        }
        else
        {
            AddSegmentsFromKeyframes(track, keyframes, segments);
        }

        return segments.AsReadOnly();
    }

    private static void AddSegmentsFromKeyframes(
        KeyframeTrack<T> track,
        IReadOnlyList<Keyframe<T>> keyframes,
        List<Segment> output)
    {
        for (int index = 0; index < keyframes.Count - 1; index++)
        {
            Keyframe<T> from = keyframes[index];
            Keyframe<T> to = keyframes[index + 1];
            output.Add(new Segment(
                track.EasingType,
                from.Value,
                from.Ticks,
                to.Value,
                to.Ticks));
        }
    }

    public T Sample(long ticks)
    {
        long sampleTicks = LoopTicks(ticks);
        Segment segment = GetSegmentAt(sampleTicks);

        if (sampleTicks <= segment.FromTicks)
            return segment.FromValue;
        if (sampleTicks >= segment.ToTicks)
            return segment.ToValue;

        float alpha = (float)(sampleTicks - segment.FromTicks) /
            (segment.ToTicks - segment.FromTicks);
        float easedAlpha = segment.Easing.Apply(alpha);
        return _lerp.Apply(easedAlpha, segment.FromValue, segment.ToValue);
    }

    private Segment GetSegmentAt(long currentTicks)
    {
        foreach (Segment segment in _segments)
        {
            if (currentTicks < segment.ToTicks)
                return segment;
        }

        return _segments[^1];
    }

    private long LoopTicks(long ticks)
    {
        if (!_periodTicks.HasValue)
            return ticks;

        long period = _periodTicks.Value;
        long remainder = ticks % period;
        if (remainder != 0 && ((remainder < 0) != (period < 0)))
            remainder += period;
        return remainder;
    }

    private sealed record Segment(
        EasingType Easing,
        T FromValue,
        int FromTicks,
        T ToValue,
        int ToTicks);
}
