using System.Text.Json;
using Asteria.Serialization;
using Asteria.World.Attributes;
using Asteria.World.Attributes.Modifiers;
using Asteria.Content.Registries;
using Asteria.World.Clocks;
using Asteria.World.Timelines;

internal static class KeyframeTrackChecks
{
    private sealed class MutableClockInstance : ClockInstance
    {
        public long TotalTicks { get; set; }
        public float PartialTick { get; set; }
        public float Rate { get; set; } = 1.0F;
        public bool IsPaused { get; set; }
    }

    private sealed class TestClockManager : ClockManager
    {
        private readonly ClockInstance _instance;

        public TestClockManager(ClockInstance instance) =>
            _instance = instance ?? throw new ArgumentNullException(nameof(instance));

        public ClockInstance GetInstance(Holder<WorldClock> clock)
        {
            ArgumentNullException.ThrowIfNull(clock);
            return _instance;
        }
    }

    public static void Run()
    {
        CheckKeyframeCodec();
        CheckTrackCodecAndValidation();
        CheckPeriodValidation();
        CheckNonPeriodicSampling();
        CheckPeriodicSampling();
        CheckEasingCodecsAndCurves();
        CheckAttributeTrackCodecsAndSampling();
    }

    private static void CheckKeyframeCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Codec<Keyframe<float>> codec = Keyframe<float>.CreateCodec(Codecs.FLOAT);

        JsonElement encoded = codec.EncodeStart(ops, new Keyframe<float>(12, 0.75F)).GetOrThrow();
        Require(ops.Get(encoded, "ticks").GetOrThrow().GetInt32() == 12 &&
                ops.Get(encoded, "value").GetOrThrow().GetSingle() == 0.75F,
            "Keyframes must encode ticks and the typed value as named fields.");
        Require(codec.Parse(ops, encoded).GetOrThrow() == new Keyframe<float>(12, 0.75F),
            "Keyframe codecs must round-trip.");

        JsonElement negativeTicks = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(ops.CreateString("ticks"), ops.CreateInt(-1)),
            new KeyValuePair<JsonElement, JsonElement>(ops.CreateString("value"), ops.CreateFloat(0.5F))
        });
        Require(codec.Parse(ops, negativeTicks).IsError,
            "Keyframe codecs must reject negative tick values.");
    }

    private static void CheckTrackCodecAndValidation()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Codec<KeyframeTrack<float>> codec = KeyframeTrack<float>.MapCodec(Codecs.FLOAT).Codec();
        KeyframeTrack<float> track = KeyframeTrack<float>.CreateBuilder()
            .AddKeyframe(0, 0.0F)
            .AddKeyframe(10, 1.0F)
            .Build();

        JsonElement encoded = codec.EncodeStart(ops, track).GetOrThrow();
        Require(!ops.Get(encoded, "ease").TryGetResult(out _),
            "Linear easing must be omitted as the optional default.");
        KeyframeTrack<float> decoded = codec.Parse(ops, encoded).GetOrThrow();
        Require(decoded.Keyframes.SequenceEqual(track.Keyframes) &&
                ReferenceEquals(decoded.EasingType, EasingType.LINEAR),
            "Keyframe tracks must round-trip and default their easing to linear.");

        JsonElement empty = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("keyframes"),
                ops.EmptyList)
        });
        Require(codec.Parse(ops, empty).IsError,
            "Keyframe tracks must reject an empty keyframe list.");

        JsonElement unordered = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("keyframes"),
                ops.CreateList(new[]
                {
                    ops.CreateMap(new[]
                    {
                        new KeyValuePair<JsonElement, JsonElement>(ops.CreateString("ticks"), ops.CreateInt(10)),
                        new KeyValuePair<JsonElement, JsonElement>(ops.CreateString("value"), ops.CreateFloat(1.0F))
                    }),
                    ops.CreateMap(new[]
                    {
                        new KeyValuePair<JsonElement, JsonElement>(ops.CreateString("ticks"), ops.CreateInt(9)),
                        new KeyValuePair<JsonElement, JsonElement>(ops.CreateString("value"), ops.CreateFloat(0.0F))
                    })
                }))
        });
        Require(codec.Parse(ops, unordered).IsError,
            "Keyframe tracks must reject keyframes that are not ordered by ticks.");

        RequireThrows<ArgumentException>(
            () => new KeyframeTrack<float>(Array.Empty<Keyframe<float>>(), EasingType.LINEAR),
            "The KeyframeTrack constructor must reject an empty track.");
    }

    private static void CheckPeriodValidation()
    {
        KeyframeTrack<float> valid = KeyframeTrack<float>.CreateBuilder()
            .AddKeyframe(0, 0.0F)
            .AddKeyframe(20, 1.0F)
            .Build();
        Require(KeyframeTrack<float>.ValidatePeriod(valid, 20).IsSuccess,
            "A keyframe at the period endpoint must be accepted.");

        KeyframeTrack<float> outside = KeyframeTrack<float>.CreateBuilder()
            .AddKeyframe(0, 0.0F)
            .AddKeyframe(21, 1.0F)
            .Build();
        DataResult<KeyframeTrack<float>> result = KeyframeTrack<float>.ValidatePeriod(outside, 20);
        Require(result.IsError &&
                result.ErrorMessage == "Keyframe at tick 21 must be in range [0; 20]",
            "Period validation must reject ticks above the inclusive period endpoint.");
    }

    private static void CheckNonPeriodicSampling()
    {
        KeyframeTrack<float> track = KeyframeTrack<float>.CreateBuilder()
            .AddKeyframe(10, 0.0F)
            .AddKeyframe(20, 100.0F)
            .Build();
        KeyframeTrackSampler<float> sampler = track.BakeSampler(null, LerpFunction<float>.OfFloat());

        Require(sampler.Sample(0) == 0.0F &&
                sampler.Sample(10) == 0.0F &&
                sampler.Sample(15) == 50.0F &&
                sampler.Sample(20) == 100.0F &&
                sampler.Sample(30) == 100.0F,
            "Non-periodic tracks must hold their first and final values and interpolate between keyframes.");

        KeyframeTrack<float> immediate = KeyframeTrack<float>.CreateBuilder()
            .AddKeyframe(0, 1.0F)
            .AddKeyframe(5, 2.0F)
            .AddKeyframe(5, 3.0F)
            .AddKeyframe(10, 4.0F)
            .Build();
        Require(immediate.BakeSampler(null, LerpFunction<float>.OfFloat()).Sample(5) == 3.0F,
            "Two keyframes at the same tick must produce an immediate transition to the later keyframe.");
    }

    private static void CheckPeriodicSampling()
    {
        KeyframeTrack<float> track = KeyframeTrack<float>.CreateBuilder()
            .AddKeyframe(5, 10.0F)
            .AddKeyframe(15, 20.0F)
            .Build();
        KeyframeTrackSampler<float> sampler =
            track.BakeSampler(20, LerpFunction<float>.OfFloat());

        Require(sampler.Sample(5) == 10.0F &&
                sampler.Sample(15) == 20.0F &&
                sampler.Sample(0) == 15.0F &&
                sampler.Sample(20) == 15.0F &&
                sampler.Sample(-1) == 16.0F,
            "Periodic tracks must interpolate across the period boundary and use floor-mod for negative ticks.");
    }

    private static void CheckEasingCodecsAndCurves()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Require(EasingType.SIMPLE_REGISTRY.Count == 32,
            "The easing registry must contain all 32 reference easing types.");

        foreach (KeyValuePair<string, EasingType> entry in EasingType.SIMPLE_REGISTRY)
        {
            JsonElement encoded = EasingType.CODEC.EncodeStart(ops, entry.Value).GetOrThrow();
            Require(encoded.GetString() == entry.Key &&
                    ReferenceEquals(EasingType.CODEC.Parse(ops, encoded).GetOrThrow(), entry.Value),
                "Every built-in easing must round-trip under its serialized name.");
        }

        Require(EasingType.CONSTANT.Apply(0.5F) == 0.0F &&
                EasingType.LINEAR.Apply(0.5F) == 0.5F &&
                Math.Abs(EasingType.OUT_BOUNCE.Apply(1.0F) - 1.0F) < 0.000001F,
            "Built-in easing functions must preserve constant, linear and bounce endpoint behavior.");

        EasingType cubic = EasingType.CubicBezier(0.0F, 0.0F, 1.0F, 1.0F);
        JsonElement cubicEncoded = EasingType.CODEC.EncodeStart(ops, cubic).GetOrThrow();
        Require(ops.Get(cubicEncoded, "cubic_bezier").GetOrThrow().GetArrayLength() == 4 &&
                Math.Abs(EasingType.CODEC.Parse(ops, cubicEncoded).GetOrThrow().Apply(0.35F) - 0.35F) < 0.0001F,
            "Cubic Bézier easing must use its four-control codec and solve a linear curve.");

        JsonElement invalidCubic = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("cubic_bezier"),
                ops.CreateList(new[]
                {
                    ops.CreateFloat(-0.1F), ops.CreateFloat(0.0F),
                    ops.CreateFloat(1.0F), ops.CreateFloat(1.0F)
                }))
        });
        Require(EasingType.CODEC.Parse(ops, invalidCubic).IsError,
            "Cubic Bézier easing must reject X controls outside [0, 1].");
    }

    private static void CheckAttributeTrackCodecsAndSampling()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Codec<IAttributeTrack<float>> codec =
            AttributeTrack.CreateCodec(EnvironmentAttributes.FOG_END_DISTANCE);

        var clockInstance = new MutableClockInstance { TotalTicks = 5 };
        var clockManager = new TestClockManager(clockInstance);
        Holder<WorldClock> clock = Holder<WorldClock>.Direct(new WorldClock());

        AttributeTrack<float, float> overrideTrack = new(
            AttributeModifiers.Override<float>(),
            KeyframeTrack<float>.CreateBuilder()
                .AddKeyframe(0, 10.0F)
                .AddKeyframe(10, 20.0F)
                .Build());
        JsonElement overrideEncoded = codec.EncodeStart(
            ops, overrideTrack).GetOrThrow();
        Require(!ops.Get(overrideEncoded, "modifier").TryGetResult(out _),
            "Override is the implicit default modifier and must be omitted from attribute-track data.");

        IAttributeTrack<float> overrideDecoded = codec.Parse(ops, overrideEncoded).GetOrThrow();
        var overrideTyped = (AttributeTrack<float, float>)overrideDecoded;
        AttributeTrackSampler<float, float> overrideSampler =
            overrideTyped.BakeSampler(EnvironmentAttributes.FOG_END_DISTANCE, clock, null, clockManager);
        Require(overrideSampler.ApplyTimeBased(999.0F, 1) == 15.0F,
            "Override tracks must interpolate attribute values and replace the base value.");

        AttributeTrack<float, float> addTrack = new(
            FloatModifier.ADD,
            KeyframeTrack<float>.CreateBuilder()
                .AddKeyframe(0, 2.0F)
                .AddKeyframe(10, 6.0F)
                .Build());
        JsonElement addEncoded = codec.EncodeStart(ops, addTrack).GetOrThrow();
        Require(ops.Get(addEncoded, "modifier").GetOrThrow().GetString() == "add",
            "Non-default track modifiers must be written by their registered serialized name.");

        IAttributeTrack<float> addDecoded = codec.Parse(ops, addEncoded).GetOrThrow();
        var addSampler = ((AttributeTrack<float, float>)addDecoded)
            .BakeSampler(EnvironmentAttributes.FOG_END_DISTANCE, clock, null, clockManager);
        Require(addSampler.ApplyTimeBased(100.0F, 1) == 104.0F,
            "Non-override tracks must interpolate their modifier arguments before applying the modifier.");

        var alphaTrack = new AttributeTrack<float, FloatWithAlpha>(
            FloatModifier.ALPHA_BLEND,
            KeyframeTrack<FloatWithAlpha>.CreateBuilder()
                .AddKeyframe(0, new FloatWithAlpha(10.0F, 0.0F))
                .AddKeyframe(10, new FloatWithAlpha(20.0F, 1.0F))
                .Build());
        var alphaSampler = alphaTrack.BakeSampler(EnvironmentAttributes.FOG_END_DISTANCE, clock, null, clockManager);
        Require(Math.Abs(alphaSampler.ApplyTimeBased(100.0F, 10) - 57.5F) < 0.000001F,
            "Modifier-specific keyframe interpolation must interpolate both alpha-blend argument fields.");

        clockInstance.TotalTicks = 9;
        Require(addSampler.ApplyTimeBased(100.0F, 1) == 104.0F,
            "An attribute-track sampler must reuse its cached argument for the same tick ID, even if the clock has advanced.");
        Require(Math.Abs(addSampler.ApplyTimeBased(100.0F, 2) - 105.6F) < 0.000001F,
            "An attribute-track sampler must read the clock again when the cache tick ID changes.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(message, exception);
        }

        throw new InvalidOperationException(message);
    }
}
