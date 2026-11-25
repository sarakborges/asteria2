using Asteria.Serialization;
using System.Globalization;

namespace Asteria.Serialization.Nbt;

/// <summary>IEEE-754 double-precision NBT scalar (tag ID 6).</summary>
public sealed record DoubleTag(double Value) : NumericTag
{
    private const long SELF_SIZE_IN_BYTES = 16;

    public static DoubleTag ZERO { get; } = new(0.0);

    public static TagType<DoubleTag> TYPE { get; } =
        TagType<DoubleTag>.StaticSize(
            6,
            "DOUBLE",
            "TAG_Double",
            sizeof(double),
            SELF_SIZE_IN_BYTES,
            static (input, accounter) =>
            {
                accounter.AccountBytes(SELF_SIZE_IN_BYTES);
                return ValueOf(input.ReadDouble());
            });

    public static DoubleTag ValueOf(double value) =>
        value == 0.0 ? ZERO : new DoubleTag(value);

    public byte GetId() => 6;
    public TagType GetTagType() => TYPE;

    public void Write(NbtDataOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.WriteDouble(Value);
    }

    public Tag Copy() => this;
    public long SizeInBytes() => SELF_SIZE_IN_BYTES;
    public sbyte ByteValue() => unchecked((sbyte)ToJavaInt(Value));
    public short ShortValue() => unchecked((short)ToJavaInt(Value));
    public int IntValue() => ToJavaInt(Value);
    public long LongValue() => ToJavaLong(Value);
    public float FloatValue() => (float)Value;
    public double DoubleValue() => Value;
    public NumberValue Box() => NumberValue.Of(Value);

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
        if (value >= 9_223_372_036_854_775_808.0)
            return long.MaxValue;
        if (value <= long.MinValue)
            return long.MinValue;
        return (long)value;
    }

    public override string ToString() =>
        FloatTag.FormatFloating(Value.ToString("R", CultureInfo.InvariantCulture)) + "d";
}
