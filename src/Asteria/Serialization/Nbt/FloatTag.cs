using Asteria.Serialization;
using System.Globalization;

namespace Asteria.Serialization.Nbt;

/// <summary>IEEE-754 single-precision NBT scalar (tag ID 5).</summary>
public sealed record FloatTag(float Value) : NumericTag
{
    private const long SELF_SIZE_IN_BYTES = 12;

    public static FloatTag ZERO { get; } = new(0.0f);

    public static TagType<FloatTag> TYPE { get; } =
        TagType<FloatTag>.StaticSize(
            5,
            "FLOAT",
            "TAG_Float",
            sizeof(float),
            SELF_SIZE_IN_BYTES,
            static (input, accounter) =>
            {
                accounter.AccountBytes(SELF_SIZE_IN_BYTES);
                return ValueOf(input.ReadFloat());
            });

    public static FloatTag ValueOf(float value) =>
        value == 0.0f ? ZERO : new FloatTag(value);

    public byte GetId() => 5;
    public TagType GetTagType() => TYPE;

    public void Write(NbtDataOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.WriteFloat(Value);
    }

    public Tag Copy() => this;
    public long SizeInBytes() => SELF_SIZE_IN_BYTES;
    public sbyte ByteValue() => unchecked((sbyte)ToJavaInt(Value));
    public short ShortValue() => unchecked((short)ToJavaInt(Value));
    public int IntValue() => ToJavaInt(Value);
    public long LongValue() => ToJavaLong(Value);
    public float FloatValue() => Value;
    public double DoubleValue() => Value;
    public NumberValue Box() => NumberValue.Of(Value);

    private static int ToJavaInt(float value)
    {
        if (float.IsNaN(value))
            return 0;
        if (value >= int.MaxValue)
            return int.MaxValue;
        if (value <= int.MinValue)
            return int.MinValue;
        return (int)value;
    }

    private static long ToJavaLong(float value)
    {
        if (float.IsNaN(value))
            return 0L;
        if (value >= 9_223_372_036_854_775_808.0F)
            return long.MaxValue;
        if (value <= long.MinValue)
            return long.MinValue;
        return (long)value;
    }

    public override string ToString() =>
        FormatFloating(Value.ToString("R", CultureInfo.InvariantCulture)) + "f";

    internal static string FormatFloating(string text) =>
        text.Contains('.') || text.Contains('E', StringComparison.OrdinalIgnoreCase)
            ? text
            : text + ".0";
}
