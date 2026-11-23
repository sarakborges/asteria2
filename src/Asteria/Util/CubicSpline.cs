using System.Globalization;
using Asteria.Core;
using Asteria.Serialization;

namespace Asteria.Util;

/// <summary>
/// Cubic spline implementation matching Minecraft Java 26.3.
/// Coordinates are bounded float functions over a caller-supplied evaluation context.
/// </summary>
public abstract class CubicSpline<TContext>
{
    public abstract void ForEachCoordinate(Action<IBoundedFloatFunction<TContext>> consumer);

    public abstract CubicSpline<TNextContext> MapCoordinates<TNextContext>(
        Func<IBoundedFloatFunction<TContext>, IBoundedFloatFunction<TNextContext>> mapper);

    public abstract Interval Range();

    public abstract string ParityString();

    public static float Sample(CubicSpline<TContext> spline, TContext context)
    {
        ArgumentNullException.ThrowIfNull(spline);

        return spline switch
        {
            Multipoint multipoint => Multipoint.Sample(multipoint, context),
            Constant constant => constant.Value,
            _ => throw new InvalidOperationException($"Unsupported cubic spline type {spline.GetType().FullName}.")
        };
    }

    public static IBoundedFloatFunction<TContext> AsSampler(CubicSpline<TContext> spline)
    {
        ArgumentNullException.ThrowIfNull(spline);

        return spline switch
        {
            Multipoint multipoint => new SplineSampler(multipoint),
            Constant constant => BoundedFloatFunctions.Constant<TContext>(constant.Value),
            _ => throw new InvalidOperationException($"Unsupported cubic spline type {spline.GetType().FullName}.")
        };
    }

    /// <summary>
    /// Creates the recursive codec used by Minecraft 26.3: constants are floats,
    /// while multipoint splines are records containing a coordinate and non-empty points.
    /// </summary>
    public static Codec<CubicSpline<TContext>> CreateCodec(
        Codec<IBoundedFloatFunction<TContext>> coordinateCodec)
    {
        ArgumentNullException.ThrowIfNull(coordinateCodec);
        return new RecursiveCodec(coordinateCodec);
    }

    private sealed record SplinePoint(
        float Location,
        CubicSpline<TContext> Value,
        float Derivative);

    private sealed class RecursiveCodec : Codec<CubicSpline<TContext>>
    {
        private readonly Codec<float> _constantCodec = Codecs.FLOAT;
        private readonly MapCodec<Multipoint> _multipointCodec;

        public RecursiveCodec(Codec<IBoundedFloatFunction<TContext>> coordinateCodec)
        {
            _multipointCodec = CreateMultipointCodec(coordinateCodec, this);
        }

        public override DataResult<TData> Encode<TData>(
            CubicSpline<TContext> input,
            DynamicOps<TData> ops,
            TData prefix)
        {
            ArgumentNullException.ThrowIfNull(input);

            return input switch
            {
                Constant constant => _constantCodec.Encode(constant.Value, ops, prefix),
                Multipoint multipoint => _multipointCodec.Codec().Encode(multipoint, ops, prefix),
                _ => DataResult<TData>.Error(
                    () => $"Unsupported cubic spline type {input.GetType().FullName}.")
            };
        }

        public override DataResult<Pair<CubicSpline<TContext>, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input)
        {
            DataResult<Pair<CubicSpline<TContext>, TData>> constant =
                _constantCodec.Decode(ops, input).Map(pair =>
                    new Pair<CubicSpline<TContext>, TData>(
                        new Constant(pair.First),
                        pair.Second));

            if (constant.IsSuccess)
                return constant;

            DataResult<Pair<CubicSpline<TContext>, TData>> multipoint =
                _multipointCodec.Codec().Decode(ops, input).Map(pair =>
                    new Pair<CubicSpline<TContext>, TData>(
                        pair.First,
                        pair.Second));

            if (multipoint.IsSuccess)
                return multipoint;
            if (constant.HasResultOrPartial)
                return constant;
            if (multipoint.HasResultOrPartial)
                return multipoint;

            return DataResult<Pair<CubicSpline<TContext>, TData>>.Error(
                () => $"Failed to parse either. First: {constant.ErrorMessage}; Second: {multipoint.ErrorMessage}");
        }

        private static MapCodec<Multipoint> CreateMultipointCodec(
            Codec<IBoundedFloatFunction<TContext>> coordinateCodec,
            Codec<CubicSpline<TContext>> recursiveCodec)
        {
            Codec<SplinePoint> pointCodec =
                RecordCodecBuilder.Create<SplinePoint>(instance =>
                    instance.Group(
                            Codecs.FLOAT.FieldOf("location")
                                .ForGetter<SplinePoint>(static point => point.Location),
                            recursiveCodec.FieldOf("value")
                                .ForGetter<SplinePoint>(static point => point.Value),
                            Codecs.FLOAT.FieldOf("derivative")
                                .ForGetter<SplinePoint>(static point => point.Derivative))
                        .Apply(
                            instance,
                            static (location, value, derivative) =>
                                new SplinePoint(location, value, derivative)));

            return RecordCodecBuilder.MapCodec<Multipoint>(instance =>
                instance.Group(
                        coordinateCodec.FieldOf("coordinate")
                            .ForGetter<Multipoint>(static spline => spline.Coordinate),
                        pointCodec.ListOf(1).FieldOf("points")
                            .ForGetter<Multipoint>(PackPoints))
                    .Apply(
                        instance,
                        static (coordinate, points) =>
                            new Multipoint(
                                coordinate,
                                points.Select(static point => point.Location).ToArray(),
                                points.Select(static point => point.Value).ToArray(),
                                points.Select(static point => point.Derivative).ToArray())));
        }

        private static IReadOnlyList<SplinePoint> PackPoints(Multipoint spline)
        {
            var points = new SplinePoint[spline.Locations.Length];
            for (int index = 0; index < points.Length; index++)
            {
                points[index] = new SplinePoint(
                    spline.Locations[index],
                    spline.Values[index],
                    spline.Derivatives[index]);
            }

            return points;
        }

        public override string ToString() => "CubicSpline";
    }

    public static Constant CreateConstant(float value) => new(value);

    public static Builder CreateBuilder(IBoundedFloatFunction<TContext> coordinate) =>
        new(coordinate);

    public static Builder CreateBuilder(
        IBoundedFloatFunction<TContext> coordinate,
        Func<float, float> valueTransformer) =>
        new(coordinate, valueTransformer);

    private sealed class SplineSampler : IBoundedFloatFunction<TContext>
    {
        private readonly Multipoint _spline;

        public SplineSampler(Multipoint spline) => _spline = spline;

        public float Apply(TContext context) => Multipoint.Sample(_spline, context);

        public Interval Range() => _spline.Range();
    }

    public sealed class Builder
    {
        private readonly IBoundedFloatFunction<TContext> _coordinate;
        private readonly Func<float, float> _valueTransformer;
        private readonly List<float> _locations = new();
        private readonly List<CubicSpline<TContext>> _values = new();
        private readonly List<float> _derivatives = new();

        internal Builder(
            IBoundedFloatFunction<TContext> coordinate,
            Func<float, float>? valueTransformer = null)
        {
            _coordinate = coordinate ?? throw new ArgumentNullException(nameof(coordinate));
            _valueTransformer = valueTransformer ?? (static value => value);
        }

        public Builder AddPoint(float location, float value) =>
            AddPoint(location, new Constant(_valueTransformer(value)), 0.0f);

        public Builder AddPoint(float location, float value, float derivative) =>
            AddPoint(location, new Constant(_valueTransformer(value)), derivative);

        public Builder AddPoint(float location, CubicSpline<TContext> spline) =>
            AddPoint(location, spline, 0.0f);

        private Builder AddPoint(float location, CubicSpline<TContext> spline, float derivative)
        {
            ArgumentNullException.ThrowIfNull(spline);

            if (_locations.Count > 0 && location <= _locations[^1])
                throw new ArgumentException("Please register points in ascending order", nameof(location));

            _locations.Add(location);
            _values.Add(spline);
            _derivatives.Add(derivative);
            return this;
        }

        public CubicSpline<TContext> Build()
        {
            if (_locations.Count == 0)
                throw new InvalidOperationException("No elements added");

            return new Multipoint(
                _coordinate,
                _locations.ToArray(),
                _values.ToArray(),
                _derivatives.ToArray());
        }
    }

    public sealed class Constant : CubicSpline<TContext>, IEquatable<Constant>
    {
        public float Value { get; }

        public Constant(float value) => Value = value;

        public override Interval Range() => Interval.OfExact(Value);

        public override void ForEachCoordinate(Action<IBoundedFloatFunction<TContext>> consumer)
        {
            // The constant node has no coordinates; the reference implementation is a no-op.
        }

        public override CubicSpline<TNextContext> MapCoordinates<TNextContext>(
            Func<IBoundedFloatFunction<TContext>, IBoundedFloatFunction<TNextContext>> mapper)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            return new CubicSpline<TNextContext>.Constant(Value);
        }

        public override string ParityString() =>
            $"k={Value.ToString("F3", CultureInfo.InvariantCulture)}";

        public bool Equals(Constant? other) =>
            other is not null && FloatEquals(Value, other.Value);

        public override bool Equals(object? obj) => obj is Constant other && Equals(other);

        public override int GetHashCode() => FloatHash(Value);
    }

    public sealed class Multipoint : CubicSpline<TContext>, IEquatable<Multipoint>
    {
        public IBoundedFloatFunction<TContext> Coordinate { get; }
        public float[] Locations { get; }
        public IReadOnlyList<CubicSpline<TContext>> Values { get; }
        public float[] Derivatives { get; }

        public Multipoint(
            IBoundedFloatFunction<TContext> coordinate,
            float[] locations,
            IReadOnlyList<CubicSpline<TContext>> values,
            float[] derivatives)
        {
            Coordinate = coordinate ?? throw new ArgumentNullException(nameof(coordinate));
            Locations = locations ?? throw new ArgumentNullException(nameof(locations));
            Values = values ?? throw new ArgumentNullException(nameof(values));
            Derivatives = derivatives ?? throw new ArgumentNullException(nameof(derivatives));
            ValidateSizes(Locations, Values, Derivatives);
        }

        public override Interval Range()
        {
            int lastIndex = Locations.Length - 1;
            float minValue = float.PositiveInfinity;
            float maxValue = float.NegativeInfinity;
            Interval inputRange = Coordinate.Range();
            if (inputRange.IsNaI())
                return inputRange;

            if (inputRange.Min() < Locations[0])
            {
                Interval firstRange = Values[0].Range();
                float edge1 = LinearExtend(inputRange.Min(), Locations, firstRange.Min(), Derivatives, 0);
                float edge2 = LinearExtend(inputRange.Min(), Locations, firstRange.Max(), Derivatives, 0);
                minValue = Math.Min(minValue, Math.Min(edge1, edge2));
                maxValue = Math.Max(maxValue, Math.Max(edge1, edge2));
            }

            if (inputRange.Max() > Locations[lastIndex])
            {
                Interval lastRange = Values[lastIndex].Range();
                float edge1 = LinearExtend(inputRange.Max(), Locations, lastRange.Min(), Derivatives, lastIndex);
                float edge2 = LinearExtend(inputRange.Max(), Locations, lastRange.Max(), Derivatives, lastIndex);
                minValue = Math.Min(minValue, Math.Min(edge1, edge2));
                maxValue = Math.Max(maxValue, Math.Max(edge1, edge2));
            }

            Interval[] valueRanges = Values.Select(static value => value.Range()).ToArray();
            foreach (Interval range in valueRanges)
            {
                minValue = Math.Min(minValue, range.Min());
                maxValue = Math.Max(maxValue, range.Max());
            }

            for (int index = 0; index < lastIndex; index++)
            {
                float x1 = Locations[index];
                float x2 = Locations[index + 1];
                float xDiff = x2 - x1;
                Interval range1 = valueRanges[index];
                Interval range2 = valueRanges[index + 1];
                float min1 = range1.Min();
                float max1 = range1.Max();
                float min2 = range2.Min();
                float max2 = range2.Max();
                float d1 = Derivatives[index];
                float d2 = Derivatives[index + 1];

                if (d1 != 0.0f || d2 != 0.0f)
                {
                    float p1 = d1 * xDiff;
                    float p2 = d2 * xDiff;
                    float minLerp1 = Math.Min(min1, min2);
                    float maxLerp1 = Math.Max(max1, max2);
                    float minA = p1 - max2 + min1;
                    float maxA = p1 - min2 + max1;
                    float minB = -p2 + min2 - max1;
                    float maxB = -p2 + max2 - min1;
                    float minLerp2 = Math.Min(minA, minB);
                    float maxLerp2 = Math.Max(maxA, maxB);
                    minValue = Math.Min(minValue, minLerp1 + 0.25f * minLerp2);
                    maxValue = Math.Max(maxValue, maxLerp1 + 0.25f * maxLerp2);
                }
            }

            return Interval.Of(minValue, maxValue);
        }

        public override void ForEachCoordinate(Action<IBoundedFloatFunction<TContext>> consumer)
        {
            ArgumentNullException.ThrowIfNull(consumer);
            consumer(Coordinate);

            foreach (CubicSpline<TContext> spline in Values)
                spline.ForEachCoordinate(consumer);
        }

        public override CubicSpline<TNextContext> MapCoordinates<TNextContext>(
            Func<IBoundedFloatFunction<TContext>, IBoundedFloatFunction<TNextContext>> mapper)
        {
            ArgumentNullException.ThrowIfNull(mapper);

            return new CubicSpline<TNextContext>.Multipoint(
                mapper(Coordinate),
                Locations,
                Values.Select(value => value.MapCoordinates(mapper)).ToArray(),
                Derivatives);
        }

        public override string ParityString()
        {
            string locations = FormatArray(Locations);
            string derivatives = FormatArray(Derivatives);
            string values = string.Join(", ", Values.Select(static value => value.ParityString()));
            return $"Spline{{coordinate={Coordinate}, locations={locations}, derivatives={derivatives}, values=[{values}]}}";
        }

        public bool Equals(Multipoint? other) =>
            other is not null &&
            Equals(Coordinate, other.Coordinate) &&
            FloatArrayEquals(Locations, other.Locations) &&
            FloatArrayEquals(Derivatives, other.Derivatives) &&
            Values.SequenceEqual(other.Values);

        public override bool Equals(object? obj) => obj is Multipoint other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Coordinate);
            AddFloatArrayHash(ref hash, Locations);
            hash.Add(Values.Count);
            foreach (CubicSpline<TContext> value in Values)
                hash.Add(value);
            AddFloatArrayHash(ref hash, Derivatives);
            return hash.ToHashCode();
        }

        public static float Sample(Multipoint spline, TContext context)
        {
            float input = spline.Coordinate.Apply(context);
            int start = FindIntervalStart(spline.Locations, input);
            int lastIndex = spline.Locations.Length - 1;

            if (start < 0)
            {
                return LinearExtend(
                    input,
                    spline.Locations,
                    CubicSpline<TContext>.Sample(spline.Values[0], context),
                    spline.Derivatives,
                    0);
            }

            if (start == lastIndex)
            {
                return LinearExtend(
                    input,
                    spline.Locations,
                    CubicSpline<TContext>.Sample(spline.Values[lastIndex], context),
                    spline.Derivatives,
                    lastIndex);
            }

            float x1 = spline.Locations[start];
            float x2 = spline.Locations[start + 1];
            float t = (input - x1) / (x2 - x1);
            CubicSpline<TContext> f1 = spline.Values[start];
            CubicSpline<TContext> f2 = spline.Values[start + 1];
            float d1 = spline.Derivatives[start];
            float d2 = spline.Derivatives[start + 1];
            float y1 = CubicSpline<TContext>.Sample(f1, context);
            float y2 = CubicSpline<TContext>.Sample(f2, context);
            float a = d1 * (x2 - x1) - (y2 - y1);
            float b = -d2 * (x2 - x1) + (y2 - y1);

            return Mth.Lerp(t, y1, y2) + t * (1.0f - t) * Mth.Lerp(t, a, b);
        }

        private static int FindIntervalStart(float[] locations, float input)
        {
            int low = 0;
            int high = locations.Length;

            while (low < high)
            {
                int middle = low + ((high - low) >> 1);
                if (input < locations[middle])
                    high = middle;
                else
                    low = middle + 1;
            }

            return low - 1;
        }

        private static float LinearExtend(
            float input,
            float[] locations,
            float value,
            float[] derivatives,
            int index)
        {
            float derivative = derivatives[index];
            return derivative == 0.0f
                ? value
                : value + derivative * (input - locations[index]);
        }

        private static void ValidateSizes(
            float[] locations,
            IReadOnlyList<CubicSpline<TContext>> values,
            float[] derivatives)
        {
            if (locations.Length != values.Count || locations.Length != derivatives.Length)
            {
                throw new ArgumentException(
                    $"All lengths must be equal, got: {locations.Length} {values.Count} {derivatives.Length}");
            }

            if (locations.Length == 0)
                throw new ArgumentException("Cannot create a multipoint spline with no points");
        }
    }

    private static string FormatArray(float[] values) =>
        "[" + string.Join(", ", values.Select(value => value.ToString("F3", CultureInfo.InvariantCulture))) + "]";

    private static bool FloatEquals(float left, float right) =>
        FloatBits(left) == FloatBits(right);

    private static int FloatBits(float value) =>
        float.IsNaN(value) ? 0x7FC00000 : BitConverter.SingleToInt32Bits(value);

    private static int FloatHash(float value) => FloatBits(value);

    private static bool FloatArrayEquals(float[] left, float[] right)
    {
        if (left.Length != right.Length)
            return false;

        for (int index = 0; index < left.Length; index++)
        {
            if (!FloatEquals(left[index], right[index]))
                return false;
        }

        return true;
    }

    private static void AddFloatArrayHash(ref HashCode hash, float[] values)
    {
        hash.Add(values.Length);
        foreach (float value in values)
            hash.Add(FloatHash(value));
    }
}
