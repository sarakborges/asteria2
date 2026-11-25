using System.Text;

namespace Asteria.Serialization.Nbt;

/// <summary>Modified-UTF string NBT scalar (tag ID 8).</summary>
public sealed record StringTag : PrimitiveTag
{
    private const long SELF_SIZE_IN_BYTES = 36;

    public string Value { get; }

    public StringTag(string value) =>
        Value = value ?? throw new ArgumentNullException(nameof(value));

    public static StringTag EMPTY { get; } = new(string.Empty);

    public static TagType<StringTag> TYPE { get; } =
        TagType<StringTag>.VariableSize(
            8,
            "STRING",
            "TAG_String",
            static (input, accounter) =>
            {
                accounter.AccountBytes(SELF_SIZE_IN_BYTES);
                string value = accounter.ReadUTF(input.ReadUTF());
                return ValueOf(value);
            },
            static (input, count, accounter) =>
            {
                for (int index = 0; index < count; index++)
                {
                    accounter.AccountBytes(SELF_SIZE_IN_BYTES);
                    _ = accounter.ReadUTF(input.ReadUTF());
                }
            });


    public static StringTag ValueOf(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Length == 0 ? EMPTY : new StringTag(value);
    }

    public byte GetId() => 8;
    public TagType GetTagType() => TYPE;

    public void Write(NbtDataOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.WriteUTF(Value);
    }

    public Tag Copy() => this;
    public long SizeInBytes() => SELF_SIZE_IN_BYTES + sizeof(char) * (long)Value.Length;
    public string AsString() => Value;

    public override string ToString() =>
        "\"" + Value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
