using Asteria.Serialization;

namespace Asteria.Serialization.Nbt;

/// <summary>Signed 8-bit NBT scalar (tag ID 1).</summary>
public sealed record ByteTag(sbyte Value) : NumericTag
{
    private const long SELF_SIZE_IN_BYTES = 9;
    private static readonly ByteTag[] Cache = CreateCache();

    public static readonly ByteTag ZERO = Cache[128];
    public static readonly ByteTag ONE = Cache[129];

    public static TagType<ByteTag> TYPE { get; } =
        TagType<ByteTag>.StaticSize(
            1,
            "BYTE",
            "TAG_Byte",
            sizeof(sbyte),
            SELF_SIZE_IN_BYTES,
            static (input, accounter) =>
            {
                accounter.AccountBytes(SELF_SIZE_IN_BYTES);
                return ValueOf(input.ReadSByte());
            });

    public static ByteTag ValueOf(sbyte value) =>
        Cache[128 + value];

    public static ByteTag ValueOf(bool value) =>
        value ? ONE : ZERO;

    private static ByteTag[] CreateCache()
    {
        var cache = new ByteTag[256];
        for (int index = 0; index < cache.Length; index++)
            cache[index] = new ByteTag(unchecked((sbyte)(index - 128)));
        return cache;
    }

    public byte GetId() => 1;
    public TagType GetTagType() => TYPE;

    public void Write(NbtDataOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.WriteSByte(Value);
    }

    public Tag Copy() => this;
    public long SizeInBytes() => SELF_SIZE_IN_BYTES;
    public sbyte ByteValue() => Value;
    public short ShortValue() => Value;
    public int IntValue() => Value;
    public long LongValue() => Value;
    public float FloatValue() => Value;
    public double DoubleValue() => Value;
    public NumberValue Box() => NumberValue.Of(Value);

    public override string ToString() =>
        Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "b";
}
