using System.IO.Compression;
using Asteria.Serialization.Nbt;

internal static class NbtIoChecks
{
    public static void Run()
    {
        CheckNamedRootLayout();
        CheckCompoundRootValidation();
        CheckEndRoot();
        CheckStreamOwnership();
        CheckGzipRoundTrip();
        CheckFileRoundTrips();
        CheckRootNameAccounting();
    }

    private static void CheckNamedRootLayout()
    {
        var compound = new CompoundTag();
        compound.PutInt("v", 42);
        var root = new NbtNamedTag("root", compound);

        using var stream = new MemoryStream();
        NbtIo.WriteNamedTag(root, stream, leaveOpen: true);
        byte[] expected = Convert.FromHexString(
            "0A0004726F6F74030001760000002A00");
        Require(
            stream.ToArray().SequenceEqual(expected),
            "A named NBT root must write its tag ID, modified-UTF name and payload in that order.");

        stream.Position = 0;
        var accounter = new NbtAccounter(512);
        NbtNamedTag decoded = NbtIo.ReadNamedTag(stream, accounter, leaveOpen: true);
        Require(
            decoded.Name == "root" &&
            decoded.Value is CompoundTag decodedCompound &&
            decodedCompound.GetIntOr("v", -1) == 42 &&
            accounter.Depth == 0,
            "A named NBT root must round-trip its name and complete compound payload.");
    }

    private static void CheckCompoundRootValidation()
    {
        var compound = new CompoundTag();
        compound.PutString("id", "chunk");
        using var stream = new MemoryStream();
        NbtIo.WriteCompound(compound, stream, leaveOpen: true);
        byte[] bytes = stream.ToArray();
        Require(
            bytes[0] == 10 &&
            bytes[1] == 0 &&
            bytes[2] == 0,
            "The ordinary compound file writer must use TAG_Compound with an empty root name.");

        stream.Position = 0;
        CompoundTag decoded = NbtIo.ReadCompound(
            stream,
            new NbtAccounter(1024),
            leaveOpen: true);
        Require(
            decoded.Equals(compound),
            "Ordinary compound file I/O must round-trip a compound root.");

        using var byteStream = new MemoryStream();
        NbtIo.WriteNamedTag(
            new NbtNamedTag("not_compound", ByteTag.ValueOf((sbyte)1)),
            byteStream,
            leaveOpen: true);
        byteStream.Position = 0;
        Capture<NbtFormatException>(
            () => NbtIo.ReadCompound(
                byteStream,
                new NbtAccounter(128),
                leaveOpen: true),
            "The compound reader must reject a non-compound root rather than silently converting it.");
    }

    private static void CheckEndRoot()
    {
        using var stream = new MemoryStream();
        NbtIo.WriteNamedTag(
            new NbtNamedTag("ignored", EndTag.INSTANCE),
            stream,
            leaveOpen: true);
        Require(
            stream.ToArray().SequenceEqual(new byte[] { 0 }),
            "An END root must write only its ID, without a name or payload.");

        stream.Position = 0;
        NbtNamedTag decoded = NbtIo.ReadNamedTag(
            stream,
            new NbtAccounter(8),
            leaveOpen: true);
        Require(
            ReferenceEquals(decoded.Value, EndTag.INSTANCE) &&
            decoded.Name == string.Empty,
            "An END root must decode to the canonical sentinel with no root name.");
    }

    private static void CheckStreamOwnership()
    {
        var compound = new CompoundTag();
        compound.PutInt("a", 1);

        var outputStream = new MemoryStream();
        NbtIo.WriteCompound(compound, outputStream, leaveOpen: true);
        Require(
            outputStream.CanWrite,
            "Uncompressed NBT writing must preserve a caller-owned stream when leaveOpen is true.");

        outputStream.Position = 0;
        _ = NbtIo.ReadCompound(
            outputStream,
            new NbtAccounter(256),
            leaveOpen: true);
        Require(
            outputStream.CanRead,
            "Uncompressed NBT reading must preserve a caller-owned stream when leaveOpen is true.");

        var ownedStream = new MemoryStream();
        NbtIo.WriteCompound(compound, ownedStream);
        Require(
            !ownedStream.CanWrite,
            "Uncompressed NBT writing must close an owned stream by default.");
    }

    private static void CheckGzipRoundTrip()
    {
        var compound = new CompoundTag();
        compound.PutString("id", "block_entity");
        compound.PutLong("packed", unchecked((long)0x0123456789ABCDEF));
        var array = new LongArrayTag(new[] { 1L, 2L, 3L });
        compound.Put("longs", array);

        using var compressed = new MemoryStream();
        NbtIo.WriteCompressed(compound, compressed, leaveOpen: true);
        byte[] gzipBytes = compressed.ToArray();
        Require(
            gzipBytes.Length > 18 &&
            gzipBytes[0] == 0x1F &&
            gzipBytes[1] == 0x8B,
            "Compressed NBT output must use a valid GZIP stream.");

        compressed.Position = 0;
        CompoundTag decoded = NbtIo.ReadCompressed(
            compressed,
            new NbtAccounter(2048),
            leaveOpen: true);
        Require(
            decoded.Equals(compound) &&
            compressed.CanRead,
            "GZIP NBT streams must round-trip nested scalar and primitive-array values without taking ownership of the input stream.");

        using var invalid = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        Capture<InvalidDataException>(
            () => NbtIo.ReadCompressed(invalid, new NbtAccounter(128)),
            "Compressed NBT reading must reject input that is not GZIP data.");
    }

    private static void CheckFileRoundTrips()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "asteria2-nbt-" + Guid.NewGuid().ToString("N") + ".dat");
        try
        {
            var compound = new CompoundTag();
            compound.PutString("name", "saved");
            compound.PutIntArray("positions", new[] { 1, -2, 3 });

            NbtIo.WriteCompound(compound, path);
            Require(
                NbtIo.ReadCompound(path, new NbtAccounter(2048)).Equals(compound),
                "Uncompressed NBT path helpers must read and write the named compound root.");

            NbtIo.WriteCompressed(compound, path);
            Require(
                NbtIo.ReadCompressed(path, new NbtAccounter(2048)).Equals(compound),
                "Compressed NBT path helpers must read and write the GZIP compound root.");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static void CheckRootNameAccounting()
    {
        var compound = new CompoundTag();
        using var stream = new MemoryStream();
        NbtIo.WriteNamedTag(
            new NbtNamedTag("abcd", compound),
            stream,
            leaveOpen: true);
        stream.Position = 0;

        Capture<NbtAccounterException>(
            () => NbtIo.ReadNamedTag(
                stream,
                new NbtAccounter(35),
                leaveOpen: true),
            "Named root reading must account the root name object and UTF-16 characters before constructing its payload.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static TException Capture<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(message, exception);
        }

        throw new InvalidOperationException(message);
    }
}
