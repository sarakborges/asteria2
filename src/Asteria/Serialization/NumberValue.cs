using System.Globalization;
using System.Numerics;

namespace Asteria.Serialization;

/// <summary>
/// Numeric value retaining its original integral or floating-point representation,
/// so codecs can apply Java Number-style conversions without first losing precision.
/// </summary>
public readonly struct NumberValue : IEquatable<NumberValue>
{
    private readonly object _value;

    private NumberValue(object value) =>
        _value = value ?? throw new ArgumentNullException(nameof(value));

    public static NumberValue Of(sbyte value) => new(value);
    public static NumberValue Of(byte value) => new(value);
    public static NumberValue Of(short value) => new(value);
    public static NumberValue Of(ushort value) => new(value);
    public static NumberValue Of(int value) => new(value);
    public static NumberValue Of(uint value) => new(value);
    public static NumberValue Of(long value) => new(value);
    public static NumberValue Of(ulong value) => new(value);
    public static NumberValue Of(float value) => new(value);
    public static NumberValue Of(double value) => new(value);
    public static NumberValue Of(decimal value) => new(value);
    public static NumberValue Of(BigInteger value) => new(value);

    public static NumberValue Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!text.Contains('.') &&
            !text.Contains('e') && !text.Contains('E') &&
            BigInteger.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger integer))
        {
            return Of(integer);
        }

        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal decimalValue))
            return Of(decimalValue);

        return Of(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));
    }

    public int IntValue => _value switch
    {
        sbyte value => value,
        byte value => value,
        short value => value,
        ushort value => value,
        int value => value,
        uint value => unchecked((int)value),
        long value => unchecked((int)value),
        ulong value => unchecked((int)value),
        BigInteger value => unchecked((int)(uint)(value & uint.MaxValue)),
        decimal value => ToInt32(new BigInteger(decimal.Truncate(value))),
        float value => ToJavaInt(value),
        double value => ToJavaInt(value),
        _ => throw new InvalidOperationException("Uninitialized numeric value.")
    };

    public long LongValue => _value switch
    {
        sbyte value => value,
        byte value => value,
        short value => value,
        ushort value => value,
        int value => value,
        uint value => value,
        long value => value,
        ulong value => unchecked((long)value),
        BigInteger value => ToInt64(value),
        decimal value => ToInt64(new BigInteger(decimal.Truncate(value))),
        float value => ToJavaLong(value),
        double value => ToJavaLong(value),
        _ => throw new InvalidOperationException("Uninitialized numeric value.")
    };

    public float FloatValue => _value switch
    {
        BigInteger value => (float)value,
        _ => Convert.ToSingle(_value, CultureInfo.InvariantCulture)
    };

    public double DoubleValue => _value switch
    {
        BigInteger value => (double)value,
        _ => Convert.ToDouble(_value, CultureInfo.InvariantCulture)
    };

    public object BoxedValue => _value;

    public bool TryGetInt64Exact(out long value)
    {
        switch (_value)
        {
            case sbyte number:
                value = number;
                return true;
            case byte number:
                value = number;
                return true;
            case short number:
                value = number;
                return true;
            case ushort number:
                value = number;
                return true;
            case int number:
                value = number;
                return true;
            case uint number:
                value = number;
                return true;
            case long number:
                value = number;
                return true;
            case ulong number when number <= long.MaxValue:
                value = (long)number;
                return true;
            case BigInteger number when number >= long.MinValue && number <= long.MaxValue:
                value = (long)number;
                return true;
            case decimal number when number == decimal.Truncate(number) &&
                                     number >= long.MinValue && number <= long.MaxValue:
                value = (long)number;
                return true;
            case float number when float.IsFinite(number) &&
                                   number == MathF.Truncate(number) &&
                                   number >= long.MinValue && number < 9223372036854775808.0F:
                value = (long)number;
                return true;
            case double number when double.IsFinite(number) &&
                                    number == Math.Truncate(number) &&
                                    number >= long.MinValue && number < 9223372036854775808.0:
                value = (long)number;
                return true;
            default:
                value = default;
                return false;
        }
    }

    public bool Equals(NumberValue other) =>
        _value.Equals(other._value);

    public override bool Equals(object? obj) =>
        obj is NumberValue other && Equals(other);

    public override int GetHashCode() =>
        _value.GetHashCode();

    public override string ToString() =>
        _value switch
        {
            float value => value.ToString("R", CultureInfo.InvariantCulture),
            double value => value.ToString("R", CultureInfo.InvariantCulture),
            decimal value => value.ToString(CultureInfo.InvariantCulture),
            IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
            _ => _value.ToString() ?? string.Empty
        };

    private static int ToInt32(BigInteger value) =>
        unchecked((int)(uint)(value & uint.MaxValue));

    private static long ToInt64(BigInteger value)
    {
        BigInteger mask = (BigInteger.One << 64) - BigInteger.One;
        return unchecked((long)(ulong)(value & mask));
    }

    private static int ToJavaInt(double value)
    {
        if (double.IsNaN(value))
            return 0;
        if (value >= int.MaxValue)
            return int.MaxValue;
        if (value <= int.MinValue)
            return int.MinValue;
        return (int)value;
    }

    private static long ToJavaLong(double value)
    {
        if (double.IsNaN(value))
            return 0L;
        if (value >= long.MaxValue)
            return long.MaxValue;
        if (value <= long.MinValue)
            return long.MinValue;
        return (long)value;
    }
}
