using System.IO.Compression;

namespace Asteria.Serialization.Nbt;

/// <summary>
/// Reads and writes named NBT roots, including gzip-compressed stream and file forms.
/// </summary>
public static class NbtIo
{
    private const long STRING_OBJECT_SIZE_IN_BYTES = 28;

    public static NbtNamedTag ReadNamedTag(
        NbtDataInput input,
        NbtAccounter accounter)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(accounter);

        byte typeId = input.ReadByte();
        if (typeId == EndTag.INSTANCE.GetId())
            return new NbtNamedTag(string.Empty, EndTag.INSTANCE);

        string name = input.ReadUTF();
        accounter.AccountBytes(STRING_OBJECT_SIZE_IN_BYTES);
        name = accounter.ReadUTF(name);

        Tag value = TagTypes.GetType(typeId).Load(input, accounter);
        return new NbtNamedTag(name, value);
    }

    public static NbtNamedTag ReadNamedTag(
        Stream stream,
        NbtAccounter accounter,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(accounter);

        using var input = new NbtDataInput(stream, leaveOpen);
        return ReadNamedTag(input, accounter);
    }

    public static void WriteNamedTag(
        NbtNamedTag namedTag,
        NbtDataOutput output)
    {
        ArgumentNullException.ThrowIfNull(namedTag);
        ArgumentNullException.ThrowIfNull(output);

        Tag tag = namedTag.Value;
        output.WriteByte(tag.GetId());
        if (tag.GetId() == EndTag.INSTANCE.GetId())
            return;

        output.WriteUTF(namedTag.Name);
        tag.Write(output);
    }

    public static void WriteNamedTag(
        NbtNamedTag namedTag,
        Stream stream,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(namedTag);
        ArgumentNullException.ThrowIfNull(stream);

        using var output = new NbtDataOutput(stream, leaveOpen);
        WriteNamedTag(namedTag, output);
        output.Flush();
    }

    /// <summary>
    /// Reads a named root and requires its payload to be a compound, as required by
    /// Minecraft's ordinary compressed and uncompressed NBT file APIs.
    /// </summary>
    public static CompoundTag ReadCompound(
        NbtDataInput input,
        NbtAccounter accounter)
    {
        NbtNamedTag root = ReadNamedTag(input, accounter);
        return root.Value as CompoundTag ??
            throw new NbtFormatException(
                $"Root tag must be a compound tag, got {root.Value.GetTagType().PrettyName}.");
    }

    public static CompoundTag ReadCompound(
        Stream stream,
        NbtAccounter? accounter = null,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var input = new NbtDataInput(stream, leaveOpen);
        return ReadCompound(input, accounter ?? NbtAccounter.DefaultQuota());
    }

    public static CompoundTag ReadCompound(
        string path,
        NbtAccounter? accounter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream stream = File.OpenRead(path);
        return ReadCompound(
            stream,
            accounter ?? NbtAccounter.DefaultQuota(),
            leaveOpen: true);
    }

    public static void WriteCompound(
        CompoundTag compound,
        NbtDataOutput output)
    {
        ArgumentNullException.ThrowIfNull(compound);
        ArgumentNullException.ThrowIfNull(output);
        WriteNamedTag(new NbtNamedTag(string.Empty, compound), output);
    }

    public static void WriteCompound(
        CompoundTag compound,
        Stream stream,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(compound);
        ArgumentNullException.ThrowIfNull(stream);
        using var output = new NbtDataOutput(stream, leaveOpen);
        WriteCompound(compound, output);
        output.Flush();
    }

    public static void WriteCompound(
        CompoundTag compound,
        string path)
    {
        ArgumentNullException.ThrowIfNull(compound);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using FileStream stream = new(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        using var output = new NbtDataOutput(stream, leaveOpen: true);
        WriteCompound(compound, output);
        output.Flush();
        stream.Flush(flushToDisk: true);
    }

    public static CompoundTag ReadCompressed(
        Stream stream,
        NbtAccounter? accounter = null,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var gzip = new GZipStream(
            stream,
            CompressionMode.Decompress,
            leaveOpen);
        using var input = new NbtDataInput(gzip, leaveOpen: true);
        return ReadCompound(
            input,
            accounter ?? NbtAccounter.DefaultQuota());
    }

    public static CompoundTag ReadCompressed(
        string path,
        NbtAccounter? accounter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream stream = File.OpenRead(path);
        return ReadCompressed(
            stream,
            accounter ?? NbtAccounter.DefaultQuota(),
            leaveOpen: true);
    }

    public static void WriteCompressed(
        CompoundTag compound,
        Stream stream,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(compound);
        ArgumentNullException.ThrowIfNull(stream);

        using var gzip = new GZipStream(
            stream,
            CompressionLevel.Optimal,
            leaveOpen);
        using (var output = new NbtDataOutput(gzip, leaveOpen: true))
        {
            WriteCompound(compound, output);
            output.Flush();
        }
    }

    public static void WriteCompressed(
        CompoundTag compound,
        string path)
    {
        ArgumentNullException.ThrowIfNull(compound);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using FileStream stream = new(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        using (var gzip = new GZipStream(
                   stream,
                   CompressionLevel.Optimal,
                   leaveOpen: true))
        using (var output = new NbtDataOutput(gzip, leaveOpen: true))
        {
            WriteCompound(compound, output);
            output.Flush();
        }

        stream.Flush(flushToDisk: true);
    }
}
