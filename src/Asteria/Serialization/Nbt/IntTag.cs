using Asteria.Serialization;

namespace Asteria.Serialization.Nbt;

/// <summary>Signed 32-bit NBT scalar (tag ID 3).</summary>
public sealed record IntTag(int Value) : NumericTag
{
    private const long SELF_SIZE_IN_BYTES = 12;
    private static readonly IntTag[] Cache = CreateCache();

    public static TagType<IntTag> TYPE { get; } =
        TagType<IntTag>.StaticSize(
            3,
            "INT",
            "TAG_Int",
            sizeof(int),
            SELF_SIZE_IN_BYTES,
            static (input, accounter) =>
            {
                accounter.AccountBytes(SELF_SIZE_IN_BYTES);
                return ValueOf(input.ReadInt());
            });

    public static IntTag ValueOf(int value) =>
        value is >= -128 and <= 1024
            ? Cache[value + 128]
            : new IntTag(value);

    private static IntTag[] CreateCache()
    {
        var cache = new IntTag[1153];
        for (int index = 0; index < cache.Length; index++)
            cache[index] = new IntTag(index - 128);
        return cache;
    }

    public byte GetId() => 3;
    public TagType GetTagType() => TYPE;

    public void Write(NbtDataOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.WriteInt(Value);
    }

    public Tag Copy() => this;
    public long SizeInBytes() => SELF_SIZE_IN_BYTES;
    public sbyte ByteValue() => unchecked((sbyte)Value);
    public short ShortValue() => unchecked((short)Value);
    public int IntValue() => Value;
    public long LongValue() => Value;
    public float FloatValue() => Value;
    public double DoubleValue() => Value;
    public NumberValue Box() => NumberValue.Of(Value);

    public override string ToString() =>
        Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
