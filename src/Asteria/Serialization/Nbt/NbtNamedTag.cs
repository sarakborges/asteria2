namespace Asteria.Serialization.Nbt;

/// <summary>A named root NBT value. The root name is not stored on the tag itself.</summary>
public sealed record NbtNamedTag
{
    public string Name { get; }
    public Tag Value { get; }

    public NbtNamedTag(string name, Tag value)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }
}
