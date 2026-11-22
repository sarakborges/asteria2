using Asteria.Serialization;

namespace Asteria.World.Timelines;

/// <summary>Easing curve applied to normalized progress between two keyframes.</summary>
public abstract class EasingType
{
    private static readonly Dictionary<string, EasingType> MutableSimpleRegistry =
        new(StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, EasingType> SIMPLE_REGISTRY =>
        MutableSimpleRegistry;

    public static readonly EasingType CONSTANT = RegisterSimple("constant", static _ => 0.0F);
    public static readonly EasingType LINEAR = RegisterSimple("linear", static x => x);
    public static readonly EasingType IN_BACK = RegisterSimple("in_back", Ease.InBack);
    public static readonly EasingType IN_BOUNCE = RegisterSimple("in_bounce", Ease.InBounce);
    public static readonly EasingType IN_CIRC = RegisterSimple("in_circ", Ease.InCirc);
    public static readonly EasingType IN_CUBIC = RegisterSimple("in_cubic", Ease.InCubic);
    public static readonly EasingType IN_ELASTIC = RegisterSimple("in_elastic", Ease.InElastic);
    public static readonly EasingType IN_EXPO = RegisterSimple("in_expo", Ease.InExpo);
    public static readonly EasingType IN_QUAD = RegisterSimple("in_quad", Ease.InQuad);
    public static readonly EasingType IN_QUART = RegisterSimple("in_quart", Ease.InQuart);
    public static readonly EasingType IN_QUINT = RegisterSimple("in_quint", Ease.InQuint);
    public static readonly EasingType IN_SINE = RegisterSimple("in_sine", Ease.InSine);
    public static readonly EasingType IN_OUT_BACK = RegisterSimple("in_out_back", Ease.InOutBack);
    public static readonly EasingType IN_OUT_BOUNCE = RegisterSimple("in_out_bounce", Ease.InOutBounce);
    public static readonly EasingType IN_OUT_CIRC = RegisterSimple("in_out_circ", Ease.InOutCirc);
    public static readonly EasingType IN_OUT_CUBIC = RegisterSimple("in_out_cubic", Ease.InOutCubic);
    public static readonly EasingType IN_OUT_ELASTIC = RegisterSimple("in_out_elastic", Ease.InOutElastic);
    public static readonly EasingType IN_OUT_EXPO = RegisterSimple("in_out_expo", Ease.InOutExpo);
    public static readonly EasingType IN_OUT_QUAD = RegisterSimple("in_out_quad", Ease.InOutQuad);
    public static readonly EasingType IN_OUT_QUART = RegisterSimple("in_out_quart", Ease.InOutQuart);
    public static readonly EasingType IN_OUT_QUINT = RegisterSimple("in_out_quint", Ease.InOutQuint);
    public static readonly EasingType IN_OUT_SINE = RegisterSimple("in_out_sine", Ease.InOutSine);
    public static readonly EasingType OUT_BACK = RegisterSimple("out_back", Ease.OutBack);
    public static readonly EasingType OUT_BOUNCE = RegisterSimple("out_bounce", Ease.OutBounce);
    public static readonly EasingType OUT_CIRC = RegisterSimple("out_circ", Ease.OutCirc);
    public static readonly EasingType OUT_CUBIC = RegisterSimple("out_cubic", Ease.OutCubic);
    public static readonly EasingType OUT_ELASTIC = RegisterSimple("out_elastic", Ease.OutElastic);
    public static readonly EasingType OUT_EXPO = RegisterSimple("out_expo", Ease.OutExpo);
    public static readonly EasingType OUT_QUAD = RegisterSimple("out_quad", Ease.OutQuad);
    public static readonly EasingType OUT_QUART = RegisterSimple("out_quart", Ease.OutQuart);
    public static readonly EasingType OUT_QUINT = RegisterSimple("out_quint", Ease.OutQuint);
    public static readonly EasingType OUT_SINE = RegisterSimple("out_sine", Ease.OutSine);

    public static readonly Codec<EasingType> CODEC = new EasingCodec();

    protected EasingType() { }

    public abstract float Apply(float x);

    public static EasingType RegisterSimple(string id, EasingType easing)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(easing);
        if (!MutableSimpleRegistry.TryAdd(id, easing))
            throw new InvalidOperationException($"Duplicate easing type '{id}'.");
        return easing;
    }

    public static EasingType CubicBezier(float x1, float y1, float x2, float y2) =>
        new CubicBezierEasing(new CubicBezierControls(x1, y1, x2, y2));

    public static EasingType SymmetricCubicBezier(float x1, float y1) =>
        CubicBezier(x1, y1, 1.0F - x1, 1.0F - y1);

    private sealed class SimpleEasing(string id, Func<float, float> apply) : EasingType
    {
        public string Id { get; } = id;
        public override float Apply(float x) => apply(x);
        public override string ToString() => Id;
    }

    public readonly record struct CubicBezierControls(float X1, float Y1, float X2, float Y2)
    {
        public static Codec<CubicBezierControls> CODEC { get; } =
            Codecs.FLOAT.ListOf(4, 4)
                .XMap(
                    static values => new CubicBezierControls(values[0], values[1], values[2], values[3]),
                    static controls => new[] { controls.X1, controls.Y1, controls.X2, controls.Y2 })
                .Validate(static controls =>
                {
                    if (controls.X1 < 0.0F || controls.X1 > 1.0F)
                        return DataResult<CubicBezierControls>.Error("x1 must be in range [0; 1]");
                    if (controls.X2 < 0.0F || controls.X2 > 1.0F)
                        return DataResult<CubicBezierControls>.Error("x2 must be in range [0; 1]");
                    return DataResult<CubicBezierControls>.Success(controls);
                });
    }

    public sealed class CubicBezierEasing : EasingType, IEquatable<CubicBezierEasing>
    {
        public static Codec<CubicBezierEasing> CUBIC_BEZIER_CODEC { get; } =
            CubicBezierControls.CODEC.FieldOf("cubic_bezier")
                .XMap<CubicBezierEasing>(
                    static controls => new CubicBezierEasing(controls),
                    static easing => easing.Controls)
                .Codec();

        private readonly CubicCurve _xCurve;
        private readonly CubicCurve _yCurve;

        public CubicBezierControls Controls { get; }

        public CubicBezierEasing(CubicBezierControls controls)
        {
            Controls = controls;
            _xCurve = CurveFromControls(controls.X1, controls.X2);
            _yCurve = CurveFromControls(controls.Y1, controls.Y2);
        }

        public override float Apply(float x) =>
            _yCurve.Sample(SolveT(x));

        private float SolveT(float x)
        {
            float t = x;
            for (int i = 0; i < 4; i++)
            {
                float error = _xCurve.Sample(t) - x;
                if (MathF.Abs(error) < 1.0E-5F)
                    return t;

                float gradient = _xCurve.SampleGradient(t);
                if (gradient < 1.0E-5F)
                    break;

                t -= Math.Clamp(error / gradient, -0.25F, 0.25F);
            }

            return SolveTBisect(x, t);
        }

        private float SolveTBisect(float x, float initialT)
        {
            float t0 = 0.0F;
            float t1 = 1.0F;
            float t;
            for (t = initialT; t0 < t1; t = (t1 + t0) / 2.0F)
            {
                float error = _xCurve.Sample(t) - x;
                if (MathF.Abs(error) < 1.0E-5F)
                    return t;

                if (error < 0.0F)
                    t0 = t;
                else
                    t1 = t;
            }

            return t;
        }

        private static CubicCurve CurveFromControls(float first, float second) =>
            new(3.0F * first - 3.0F * second + 1.0F, -6.0F * first + 3.0F * second, 3.0F * first);

        public bool Equals(CubicBezierEasing? other) =>
            other is not null && Controls.Equals(other.Controls);

        public override bool Equals(object? obj) =>
            obj is CubicBezierEasing other && Equals(other);

        public override int GetHashCode() => Controls.GetHashCode();

        public override string ToString() =>
            $"CubicBezier({Controls.X1}, {Controls.Y1}, {Controls.X2}, {Controls.Y2})";

        private readonly record struct CubicCurve(float A, float B, float C)
        {
            public float Sample(float t) =>
                ((A * t + B) * t + C) * t;

            public float SampleGradient(float t) =>
                (3.0F * A * t + 2.0F * B) * t + C;
        }
    }

    private sealed class EasingCodec : Codec<EasingType>
    {
        public override DataResult<TData> Encode<TData>(
            EasingType input,
            DynamicOps<TData> ops,
            TData prefix)
        {
            ArgumentNullException.ThrowIfNull(input);

            if (input is CubicBezierEasing cubicBezier)
                return CubicBezierEasing.CUBIC_BEZIER_CODEC.Encode(cubicBezier, ops, prefix);

            foreach (KeyValuePair<string, EasingType> entry in MutableSimpleRegistry)
            {
                if (ReferenceEquals(entry.Value, input))
                    return Codecs.STRING.Encode(entry.Key, ops, prefix);
            }

            return DataResult<TData>.Error(() =>
                $"Unregistered easing type '{input}'.");
        }

        public override DataResult<Pair<EasingType, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input)
        {
            DataResult<Pair<string, TData>> simple = Codecs.STRING.Decode(ops, input);
            if (simple.TryGetResult(out Pair<string, TData> simplePair))
            {
                if (MutableSimpleRegistry.TryGetValue(simplePair.First, out EasingType? easing))
                    return DataResult<Pair<EasingType, TData>>.Success(
                        new Pair<EasingType, TData>(easing, simplePair.Second));

                DataResult<Pair<CubicBezierEasing, TData>> notBezier =
                    CubicBezierEasing.CUBIC_BEZIER_CODEC.Decode(ops, input);
                return DataResult<Pair<EasingType, TData>>.Error(
                    () => $"Unknown easing type '{simplePair.First}'. {notBezier.ErrorMessage}");
            }

            return CubicBezierEasing.CUBIC_BEZIER_CODEC.Decode(ops, input).Map(pair =>
                new Pair<EasingType, TData>(pair.First, pair.Second));
        }
    }

    private static EasingType RegisterSimple(string id, Func<float, float> apply) =>
        RegisterSimple(id, new SimpleEasing(id, apply));
}
