using System.Globalization;

namespace Asteria.Util;

/// <summary>
/// Closed interval arithmetic used by Minecraft Java 26.3 density-function bounds.
/// NaI is represented by the unique NaI singleton, not by arbitrary NaN bounds.
/// </summary>
public sealed class Interval : IEquatable<Interval>
{
    public static readonly Interval NaI = new(float.NaN, float.NaN);
    public static readonly Interval INFINITE = new(float.NegativeInfinity, float.PositiveInfinity);
    private static readonly Interval NEGATIVE_ONE_TO_ONE = new(-1.0f, 1.0f);
    private static readonly Interval ZERO_TO_ONE = new(0.0f, 1.0f);

    private readonly float _min;
    private readonly float _max;

    private Interval(float min, float max)
    {
        _min = min;
        _max = max;
    }

    public static Interval Of(float min, float max)
    {
        if (max < min)
            throw new ArgumentException($"max ({Format(max)}) < min ({Format(min)})");
        if (float.IsNaN(min) || float.IsNaN(max))
            throw new ArgumentException($"Bounds cannot include NaN [{Format(min)}; {Format(max)}]: use Interval.NaI explicitly");
        if (min == float.NegativeInfinity && max == float.PositiveInfinity)
            return INFINITE;
        if (max == 1.0f)
        {
            if (min == 0.0f)
                return ZERO_TO_ONE;
            if (min == -1.0f)
                return NEGATIVE_ONE_TO_ONE;
        }
        return new Interval(min, max);
    }

    public static Interval OfSymmetric(float range) => Of(-range, range);

    public static Interval OfExact(float value) => Of(value, value);

    public static Interval Encapsulating(IReadOnlyList<Interval> intervals)
    {
        ArgumentNullException.ThrowIfNull(intervals);
        if (intervals.Count == 0)
            throw new ArgumentException("At least one interval required", nameof(intervals));

        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;
        foreach (Interval interval in intervals)
        {
            ArgumentNullException.ThrowIfNull(interval);
            if (!interval.IsNaI())
            {
                min = Math.Min(interval._min, min);
                max = Math.Max(interval._max, max);
            }
        }
        return max < min ? NaI : Of(min, max);
    }

    public static Interval Encapsulating(params Interval[] intervals) =>
        Encapsulating((IReadOnlyList<Interval>)intervals);

    public static Interval Encapsulating(float first, float second)
    {
        if (float.IsNaN(first) && float.IsNaN(second))
            return NaI;
        if (float.IsNaN(first))
            return OfExact(second);
        return float.IsNaN(second)
            ? OfExact(first)
            : Of(Math.Min(first, second), Math.Max(first, second));
    }

    private static Interval Encapsulating(Interval first, float second)
    {
        if (float.IsNaN(second))
            return first;
        return first.IsNaI()
            ? OfExact(second)
            : Of(Math.Min(first._min, second), Math.Max(first._max, second));
    }

    public static Interval Add(Interval left, Interval right)
    {
        float min = left._min + right._min;
        float max = left._max + right._max;
        return !float.IsNaN(min) && !float.IsNaN(max) ? Of(min, max) : NaI;
    }

    public static Interval Sub(Interval left, Interval right)
    {
        float min = left._min - right._max;
        float max = left._max - right._min;
        return !float.IsNaN(min) && !float.IsNaN(max) ? Of(min, max) : NaI;
    }

    public static Interval Mul(Interval left, Interval right)
    {
        if (left.IsNaI() || right.IsNaI())
            return NaI;

        float minMin = MulBound(left._min, right._min);
        float minMax = MulBound(left._min, right._max);
        float maxMin = MulBound(left._max, right._min);
        float maxMax = MulBound(left._max, right._max);
        return Of(
            Math.Min(Math.Min(minMin, minMax), Math.Min(maxMin, maxMax)),
            Math.Max(Math.Max(minMin, minMax), Math.Max(maxMin, maxMax)));
    }

    private static float MulBound(float left, float right) =>
        left != 0.0f && right != 0.0f ? left * right : 0.0f;

    public static Interval Reciprocal(Interval input)
    {
        if (input.IsNaI() || (input._min == 0.0f && input._max == 0.0f))
            return NaI;
        if (!input.Contains(0.0f))
            return Of(1.0f / input._max, 1.0f / input._min);
        if (input._max == 0.0f)
            return Of(float.NegativeInfinity, 1.0f / input._min);
        return input._min == 0.0f
            ? Of(1.0f / input._max, float.PositiveInfinity)
            : INFINITE;
    }

    public static Interval Div(Interval left, Interval right) => Mul(left, Reciprocal(right));

    public static Interval Min(Interval left, Interval right) =>
        !left.IsNaI() && !right.IsNaI()
            ? Of(Math.Min(left._min, right._min), Math.Min(left._max, right._max))
            : NaI;

    public static Interval Max(Interval left, Interval right) =>
        !left.IsNaI() && !right.IsNaI()
            ? Of(Math.Max(left._min, right._min), Math.Max(left._max, right._max))
            : NaI;

    public static Interval Clamp(Interval input, float min, float max)
    {
        if (min > max)
            throw new ArgumentException($"min ({Format(min)}) > max ({Format(max)})");
        if (input.IsNaI())
            return NaI;
        if (input._min >= max)
            return OfExact(max);
        return input._max <= min
            ? OfExact(min)
            : Of(Math.Max(input._min, min), Math.Min(input._max, max));
    }

    public static Interval Abs(Interval input)
    {
        if (input.IsNaI())
            return NaI;
        float max = Math.Max(Math.Abs(input._min), Math.Abs(input._max));
        return input.Contains(0.0f)
            ? Of(0.0f, max)
            : Of(Math.Min(Math.Abs(input._min), Math.Abs(input._max)), max);
    }

    public static Interval Square(Interval input)
    {
        if (input.IsNaI())
            return NaI;
        float max = Math.Max(input._min * input._min, input._max * input._max);
        float min = input.Contains(0.0f)
            ? 0.0f
            : Math.Min(input._min * input._min, input._max * input._max);
        return Of(min, max);
    }

    public static Interval Pow(Interval baseInterval, Interval exponent)
    {
        if (baseInterval.IsNaI() || exponent.IsNaI())
            return NaI;
        if (baseInterval._min == baseInterval._max)
            return Pow(baseInterval._min, exponent);

        Interval result = Encapsulating(
            Pow(baseInterval._min, exponent),
            Pow(baseInterval._max, exponent));
        if (baseInterval.Contains(0.0f))
        {
            if (baseInterval._max > 0.0f)
                result = Encapsulating(result, Pow(0.0f, exponent));
            if (baseInterval._min < 0.0f)
                result = Encapsulating(result, Pow(-0.0f, exponent));
        }
        return result;
    }

    private static Interval Pow(float baseValue, Interval exponent)
    {
        if (float.IsNaN(baseValue) || exponent.IsNaI())
            return NaI;
        if (exponent._min == exponent._max)
        {
            float value = (float)Math.Pow(baseValue, exponent._min);
            return float.IsNaN(value) ? NaI : OfExact(value);
        }
        if (baseValue == 0.0f)
            return Mul(PowZeroBase(exponent), OfExact(MathF.CopySign(1.0f, baseValue)));
        if (baseValue == 1.0f)
            return OfExact(1.0f);
        return baseValue > 0.0f
            ? PowPositiveBase(baseValue, exponent)
            : PowNegativeBase(baseValue, exponent);
    }

    private static Interval PowPositiveBase(float baseValue, Interval exponent) =>
        float.IsFinite(exponent._min) && float.IsFinite(exponent._max)
            ? Encapsulating((float)Math.Pow(baseValue, exponent._min), (float)Math.Pow(baseValue, exponent._max))
            : PowInfiniteExponent(baseValue, exponent);

    private static Interval PowZeroBase(Interval exponent)
    {
        if (exponent.Contains(0.0f))
        {
            if (exponent._max == 0.0f)
                return Of(1.0f, float.PositiveInfinity);
            return exponent._min == 0.0f
                ? ZERO_TO_ONE
                : Of(0.0f, float.PositiveInfinity);
        }
        return exponent._max < 0.0f
            ? OfExact(float.PositiveInfinity)
            : OfExact(0.0f);
    }

    private static Interval PowInfiniteExponent(float baseValue, Interval exponent)
    {
        if (float.IsInfinity(exponent._min) && float.IsInfinity(exponent._max))
            return Of(0.0f, float.PositiveInfinity);
        if (float.IsInfinity(exponent._min))
        {
            float value = (float)Math.Pow(baseValue, exponent._max);
            return baseValue < 1.0f
                ? Of(value, float.PositiveInfinity)
                : Of(0.0f, value);
        }
        float other = (float)Math.Pow(baseValue, exponent._min);
        return baseValue < 1.0f
            ? Of(0.0f, other)
            : Of(other, float.PositiveInfinity);
    }

    private static Interval PowNegativeBase(float baseValue, Interval exponent)
    {
        float exponentMinInt = (float)Math.Ceiling(exponent._min);
        float exponentMaxInt = (float)Math.Floor(exponent._max);
        if (exponentMaxInt < exponentMinInt)
            return NaI;

        float baseToMinInt = (float)Math.Pow(baseValue, exponentMinInt);
        float baseToMaxInt = (float)Math.Pow(baseValue, exponentMaxInt);
        Interval result = Encapsulating(baseToMinInt, baseToMaxInt);
        if (float.IsInfinity(exponentMinInt))
            result = Encapsulating(result, -baseToMinInt);
        else if (exponentMinInt + 1.0f < exponentMaxInt)
            result = Encapsulating(result, (float)Math.Pow(baseValue, exponentMinInt + 1.0f));

        if (float.IsInfinity(exponentMaxInt))
            result = Encapsulating(result, -baseToMaxInt);
        else if (exponentMaxInt - 1.0f > exponentMinInt)
            result = Encapsulating(result, (float)Math.Pow(baseValue, exponentMaxInt - 1.0f));
        return result;
    }

    public static Interval Log(Interval input)
    {
        if (input._max < 0.0f)
            return NaI;
        Interval clippedInput = Max(input, OfExact(0.0f));
        return MapMonotonic(clippedInput, value => (float)Math.Log(value));
    }

    public static Interval MapMonotonic(Interval input, Func<float, float> monotonicOperation)
    {
        ArgumentNullException.ThrowIfNull(monotonicOperation);
        if (input.IsNaI())
            return NaI;

        float mappedMin = monotonicOperation(input._min);
        float mappedMax = monotonicOperation(input._max);
        if (float.IsNaN(mappedMin) || float.IsNaN(mappedMax))
            throw new InvalidOperationException("Monotonic operator should not produce NaN");
        return Of(Math.Min(mappedMin, mappedMax), Math.Max(mappedMin, mappedMax));
    }

    public static Interval Lerp(Interval alpha, Interval first, Interval second) =>
        !alpha.IsNaI() && !first.IsNaI() && !second.IsNaI()
            ? Encapsulating(
                Lerp(alpha, first._min, second._min),
                Lerp(alpha, first._max, second._min),
                Lerp(alpha, first._min, second._max),
                Lerp(alpha, first._max, second._max))
            : NaI;

    public static Interval Lerp(Interval alpha, float first, float second)
    {
        if (alpha.IsNaI() || float.IsNaN(first) || float.IsNaN(second))
            return NaI;
        return float.IsFinite(first) && float.IsFinite(second)
            ? LerpFiniteBounds(alpha, first, second)
            : LerpInfiniteBounds(alpha, first, second);
    }

    private static Interval LerpFiniteBounds(Interval alpha, float first, float second) =>
        Encapsulating(
            LerpFiniteBound(alpha._min, first, second),
            LerpFiniteBound(alpha._max, first, second));

    private static float LerpFiniteBound(float alpha, float first, float second) =>
        first + MulBound(alpha, second - first);

    private static Interval LerpInfiniteBounds(Interval alpha, float first, float second)
    {
        if (first == second)
            return OfExact(first);
        float newMin = LerpInfiniteBound(alpha._min, first, second);
        float newMax = LerpInfiniteBound(alpha._max, first, second);
        return !float.IsNaN(newMin) && !float.IsNaN(newMax)
            ? Encapsulating(newMin, newMax)
            : NaI;
    }

    private static float LerpInfiniteBound(float alpha, float first, float second)
    {
        float firstPart = MulBound(1.0f - alpha, first);
        float secondPart = MulBound(alpha, second);
        if (!float.IsInfinity(firstPart) || !float.IsInfinity(secondPart))
            return firstPart + secondPart;
        if (alpha <= 0.0f)
            return second > first ? float.NegativeInfinity : float.PositiveInfinity;
        if (alpha >= 1.0f)
            return second > first ? float.PositiveInfinity : float.NegativeInfinity;
        return float.NaN;
    }

    public static Interval Sign(Interval input)
    {
        if (input.IsNaI())
            return NaI;
        if (input._min == input._max)
            return OfExact(input._min == 0.0f ? input._min : input._min > 0.0f ? 1.0f : -1.0f);
        if (input.Contains(0.0f))
        {
            if (input._min == 0.0f)
                return Of(0.0f, 1.0f);
            return input._max == 0.0f ? Of(-1.0f, 0.0f) : Of(-1.0f, 1.0f);
        }
        return OfExact(input._min > 0.0f ? 1.0f : -1.0f);
    }

    public bool Contains(float value) => value >= _min && value <= _max;

    public bool Intersects(Interval other) => _min <= other._max && _max >= other._min;

    public bool IsNaI() => ReferenceEquals(this, NaI);

    public float Min() => _min;

    public float Max() => _max;

    public bool Equals(Interval? other) =>
        ReferenceEquals(this, other) ||
        (other is not null && _min == other._min && _max == other._max);

    public override bool Equals(object? obj) => obj is Interval other && Equals(other);

    public override int GetHashCode()
    {
        int minHash = JavaFloatHash(_min);
        return unchecked(minHash * 31 + JavaFloatHash(_max));
    }

    public override string ToString() =>
        IsNaI() ? "[NaN]" : $"[{Format(_min)}; {Format(_max)}]";

    private static int JavaFloatHash(float value)
    {
        int bits = BitConverter.SingleToInt32Bits(value);
        return float.IsNaN(value) ? 0x7FC00000 : bits;
    }

    private static string Format(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture);
}
