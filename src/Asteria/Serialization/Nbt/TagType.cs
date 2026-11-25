namespace Asteria.Serialization.Nbt;

/// <summary>
/// Describes one NBT payload type and its streaming load/skip operations.
/// </summary>
public abstract class TagType
{
    public byte Id { get; }
    public string Name { get; }
    public string PrettyName { get; }
    public abstract int Size { get; }

    protected TagType(byte id, string name, string prettyName)
    {
        Id = id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        PrettyName = prettyName ?? throw new ArgumentNullException(nameof(prettyName));
    }

    public abstract Tag Load(NbtDataInput input, NbtAccounter accounter);

    public abstract void Skip(
        NbtDataInput input,
        int count,
        NbtAccounter accounter);

    public override string ToString() => PrettyName;

    public static TagType Invalid(byte id) =>
        new InvalidNbtTagType(id);

    private sealed class InvalidNbtTagType : TagType
    {
        public override int Size => 0;

        public InvalidNbtTagType(byte id)
            : base(id, $"INVALID[{id}]", $"UNKNOWN_{id}")
        {
        }

        public override Tag Load(NbtDataInput input, NbtAccounter accounter)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(accounter);
            throw new IOException($"Invalid NBT tag id: {Id}");
        }

        public override void Skip(
            NbtDataInput input,
            int count,
            NbtAccounter accounter)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(accounter);
            throw new IOException($"Invalid NBT tag id: {Id}");
        }
    }
}

/// <summary>
/// Typed NBT payload descriptor. Loaders own per-value accounting; static-size skip
/// operations account the same allocation cost without materializing tag instances.
/// </summary>
public sealed class TagType<TTag> : TagType where TTag : Tag
{
    private readonly Func<NbtDataInput, NbtAccounter, TTag> _loader;
    private readonly Action<NbtDataInput, int, NbtAccounter> _skipper;

    public override int Size { get; }

    public TagType(
        byte id,
        string name,
        string prettyName,
        int size,
        Func<NbtDataInput, NbtAccounter, TTag> loader,
        Action<NbtDataInput, int, NbtAccounter> skipper)
        : base(id, name, prettyName)
    {
        if (size < -1)
            throw new ArgumentOutOfRangeException(nameof(size));

        Size = size;
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _skipper = skipper ?? throw new ArgumentNullException(nameof(skipper));
    }

    public TTag LoadTyped(NbtDataInput input, NbtAccounter accounter)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(accounter);
        return _loader(input, accounter);
    }

    public override Tag Load(NbtDataInput input, NbtAccounter accounter) =>
        LoadTyped(input, accounter);

    public override void Skip(
        NbtDataInput input,
        int count,
        NbtAccounter accounter)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(accounter);
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        _skipper(input, count, accounter);
    }

    public static TagType<TTag> StaticSize(
        byte id,
        string name,
        string prettyName,
        int payloadSize,
        long selfSizeInBytes,
        Func<NbtDataInput, NbtAccounter, TTag> loader)
    {
        if (payloadSize < 0)
            throw new ArgumentOutOfRangeException(nameof(payloadSize));
        if (selfSizeInBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(selfSizeInBytes));

        return new TagType<TTag>(
            id,
            name,
            prettyName,
            payloadSize,
            loader,
            (input, count, accounter) =>
            {
                accounter.AccountBytes(selfSizeInBytes, count);
                long remaining = checked((long)payloadSize * count);
                while (remaining > 0)
                {
                    int requested = (int)Math.Min(remaining, int.MaxValue);
                    int skipped = input.SkipBytes(requested);
                    if (skipped != requested)
                        throw new EndOfStreamException();
                    remaining -= skipped;
                }
            });
    }

    public static TagType<TTag> VariableSize(
        byte id,
        string name,
        string prettyName,
        Func<NbtDataInput, NbtAccounter, TTag> loader,
        Action<NbtDataInput, int, NbtAccounter> skipper) =>
        new(id, name, prettyName, -1, loader, skipper);
}
