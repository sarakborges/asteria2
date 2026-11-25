namespace Asteria.Serialization.Nbt;

/// <summary>The zero-payload END tag used as a list terminator and empty-root marker.</summary>
public sealed class EndTag : Tag
{
    private EndTag()
    {
    }

    public static EndTag INSTANCE { get; } = new();

    public static TagType<EndTag> TYPE { get; } = new(
        0,
        "END",
        "TAG_End",
        0,
        static (_, accounter) =>
        {
            accounter.AccountBytes(8);
            return INSTANCE;
        },
        static (_, _, _) =>
        {
        });

    public byte GetId() => 0;

    public TagType GetTagType() => TYPE;

    public void Write(NbtDataOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
    }

    public Tag Copy() => this;

    public long SizeInBytes() => 8;

    public string? AsString() => null;

    public override string ToString() => "END";
}
