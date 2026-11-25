namespace Asteria.Serialization.Nbt;

/// <summary>
/// Base contract for one uncompressed NBT value. Container tags are added separately;
/// this contract already preserves their tag IDs, payload operations and accounting rules.
/// </summary>
public interface Tag
{
    const int MAX_DEPTH = 512;

    byte GetId();
    TagType GetTagType();
    void Write(NbtDataOutput output);
    Tag Copy();
    long SizeInBytes();

    string? AsString() => null;
    NumberValue? AsNumber() => null;
    sbyte? AsByte() => null;
    short? AsShort() => null;
    int? AsInt() => null;
    long? AsLong() => null;
    float? AsFloat() => null;
    double? AsDouble() => null;
}

/// <summary>Marker contract for NBT values whose payload is a primitive scalar.</summary>
public interface PrimitiveTag : Tag
{
}
