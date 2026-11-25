using Asteria.Serialization;

namespace Asteria.Serialization.Nbt;

/// <summary>Signed 64-bit NBT scalar (tag ID 4).</summary>
public sealed record LongTag(long Value) : NumericTag
{
    private const long SELF_SIZE_IN_BYTES = 16;
    private static readonly LongTag[] Cache = CreateCache();

    public static TagType<LongTag> TYPE { get; } =
        TagType<LongTag>.StaticSize(
            4,
            "LONG",
            "TAG_Long",
            sizeof(long),
            SELF_SIZE_IN_BYTES,
            static (input, accounter) =>
            {
                accounter.AccountBytes(SELF_SIZE_IN_BYTES);
                return ValueOf(input.ReadLong());
            });

    public static LongTag ValueOf(long value) =>
        value is >= -128 and <= 1024
            ? Cache[(int)value + 128]
            : new LongTag(value);

    private static LongTag[] CreateCache()
    {
        var cache = new LongTag[1153];
        for (int index = 0; index < cache.Length; index++)
            cache[index] = new LongTag(index - 128);
        return cache;
    }

    public byte GetId() => 4;
    public TagType GetTagType() => TYPE;

    public void Write(NbtDataOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.WriteLong(Value);
    }

    public Tag Copy() => this;
    public long SizeInBytes() => SELF_SIZE_IN_BYTES;
    public sbyte ByteValue() => unchecked((sbyte)Value);
    public short ShortValue() => unchecked((short)Value);
    public int IntValue() => unchecked((int)Value);
    public long LongValue() => Value;
    public float FloatValue() => Value;
    public double DoubleValue() => Value;
    public NumberValue Box() => NumberValue.Of(Value);

    public override string ToString() =>
        Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "L";
}
