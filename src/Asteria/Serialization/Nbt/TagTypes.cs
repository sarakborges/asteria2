namespace Asteria.Serialization.Nbt;

/// <summary>
/// Canonical dispatch table for every NBT tag ID defined by Minecraft Java 26.3.
/// </summary>
public static class TagTypes
{
    private static readonly TagType[] Types =
    [
        EndTag.TYPE,
        ByteTag.TYPE,
        ShortTag.TYPE,
        IntTag.TYPE,
        LongTag.TYPE,
        FloatTag.TYPE,
        DoubleTag.TYPE,
        ByteArrayTag.TYPE,
        StringTag.TYPE,
        ListTag.TYPE,
        CompoundTag.TYPE,
        IntArrayTag.TYPE,
        LongArrayTag.TYPE
    ];

    public const int Count = 13;

    public static TagType GetType(byte id) =>
        id < Types.Length
            ? Types[id]
            : TagType.Invalid(id);

    public static TagType GetType(sbyte id) =>
        id >= 0
            ? GetType((byte)id)
            : TagType.Invalid(unchecked((byte)id));

    public static IReadOnlyList<TagType> All =>
        Array.AsReadOnly(Types);
}
