using Asteria.Serialization;

namespace Asteria.Serialization.Nbt;

/// <summary>Signed 16-bit NBT scalar (tag ID 2).</summary>
public sealed record ShortTag(short Value) : NumericTag
{
    private const long SELF_SIZE_IN_BYTES = 10;
    private static readonly ShortTag[] Cache = CreateCache();

    public static TagType<ShortTag> TYPE { get; } =
        TagType<ShortTag>.StaticSize(
            2,
            "SHORT",
            "TAG_Short",
            sizeof(short),
            SELF_SIZE_IN_BYTES,
            static (input, accounter) =>
            {
                accounter.AccountBytes(SELF_SIZE_IN_BYTES);
                return ValueOf(input.ReadShort());
            });

    public static ShortTag ValueOf(short value) =>
        value is >= -128 and <= 1024
            ? Cache[value + 128]
            : new ShortTag(value);

    private static ShortTag[] CreateCache()
    {
        var cache = new ShortTag[1153];
        for (int index = 0; index < cache.Length; index++)
            cache[index] = new ShortTag((short)(index - 128));
        return cache;
    }

    public byte GetId() => 2;
    public TagType GetTagType() => TYPE;

    public void Write(NbtDataOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.WriteShort(Value);
    }

    public Tag Copy() => this;
    public long SizeInBytes() => SELF_SIZE_IN_BYTES;
    public sbyte ByteValue() => unchecked((sbyte)Value);
    public short ShortValue() => Value;
    public int IntValue() => Value;
    public long LongValue() => Value;
    public float FloatValue() => Value;
    public double DoubleValue() => Value;
    public NumberValue Box() => NumberValue.Of(Value);

    public override string ToString() =>
        Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "s";
}
