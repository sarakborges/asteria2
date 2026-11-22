using Asteria.Serialization;

namespace Asteria.World.Timelines;

/// <summary>An immutable ordered set of keyframes sharing one easing function.</summary>
public sealed class KeyframeTrack<T>
{
    public IReadOnlyList<Keyframe<T>> Keyframes { get; }
    public EasingType EasingType { get; }

    public KeyframeTrack(IEnumerable<Keyframe<T>> keyframes, EasingType easingType)
    {
        ArgumentNullException.ThrowIfNull(keyframes);
        ArgumentNullException.ThrowIfNull(easingType);

        Keyframe<T>[] snapshot = keyframes.ToArray();
        if (snapshot.Length == 0)
            throw new ArgumentException("Track has no keyframes", nameof(keyframes));

        Keyframes = Array.AsReadOnly(snapshot);
        EasingType = easingType;
    }

    public static MapCodec<KeyframeTrack<T>> MapCodec(Codec<T> valueCodec)
    {
        ArgumentNullException.ThrowIfNull(valueCodec);

        Codec<IReadOnlyList<Keyframe<T>>> keyframesCodec =
            Keyframe<T>.CreateCodec(valueCodec).ListOf().Validate(ValidateKeyframes);

        return RecordCodecBuilder.MapCodec<KeyframeTrack<T>>(instance =>
            instance.Group(
                keyframesCodec.FieldOf("keyframes")
                    .ForGetter<KeyframeTrack<T>>(static track => track.Keyframes),
                EasingType.CODEC.OptionalFieldOf("ease", EasingType.LINEAR)
                    .ForGetter<KeyframeTrack<T>>(static track => track.EasingType))
            .Apply(instance, static (keyframes, easing) =>
                new KeyframeTrack<T>(keyframes, easing)));
    }

    public static DataResult<IReadOnlyList<Keyframe<T>>> ValidateKeyframes(
        IReadOnlyList<Keyframe<T>> keyframes)
    {
        ArgumentNullException.ThrowIfNull(keyframes);
        if (keyframes.Count == 0)
            return DataResult<IReadOnlyList<Keyframe<T>>>.Error("Keyframes must not be empty");

        for (int index = 1; index < keyframes.Count; index++)
        {
            if (keyframes[index - 1].Ticks > keyframes[index].Ticks)
                return DataResult<IReadOnlyList<Keyframe<T>>>.Error(
                    "Keyframes must be ordered by ticks field");
        }

        if (keyframes.Count > 1)
        {
            int repeatCount = 0;
            int lastTicks = keyframes[^1].Ticks;
            foreach (Keyframe<T> keyframe in keyframes)
            {
                if (keyframe.Ticks == lastTicks)
                {
                    if (++repeatCount > 2)
                        return DataResult<IReadOnlyList<Keyframe<T>>>.Error(
                            $"More than 2 keyframes on same tick: {keyframe.Ticks}");
                }
                else
                {
                    repeatCount = 0;
                }

                lastTicks = keyframe.Ticks;
            }
        }

        return DataResult<IReadOnlyList<Keyframe<T>>>.Success(keyframes);
    }

    public static DataResult<KeyframeTrack<T>> ValidatePeriod(
        KeyframeTrack<T> track,
        int periodTicks)
    {
        ArgumentNullException.ThrowIfNull(track);
        foreach (Keyframe<T> keyframe in track.Keyframes)
        {
            if (keyframe.Ticks < 0 || keyframe.Ticks > periodTicks)
                return DataResult<KeyframeTrack<T>>.Error(
                    $"Keyframe at tick {keyframe.Ticks} must be in range [0; {periodTicks}]");
        }

        return DataResult<KeyframeTrack<T>>.Success(track);
    }

    public KeyframeTrackSampler<T> BakeSampler(int? periodTicks, LerpFunction<T> lerp)
    {
        ArgumentNullException.ThrowIfNull(lerp);
        return new KeyframeTrackSampler<T>(this, periodTicks, lerp);
    }

    public static Builder CreateBuilder() => new();

    public sealed class Builder
    {
        private readonly List<Keyframe<T>> _keyframes = new();
        private EasingType _easing = EasingType.LINEAR;

        public Builder AddKeyframe(int ticks, T value)
        {
            _keyframes.Add(new Keyframe<T>(ticks, value));
            return this;
        }

        public Builder SetEasing(EasingType easing)
        {
            _easing = easing ?? throw new ArgumentNullException(nameof(easing));
            return this;
        }

        public KeyframeTrack<T> Build()
        {
            IReadOnlyList<Keyframe<T>> validated =
                ValidateKeyframes(_keyframes.AsReadOnly()).GetOrThrow();
            return new KeyframeTrack<T>(validated, _easing);
        }
    }
}
