using System.Text;
using Asteria.Serialization.Nbt;

internal static class NbtBinaryIOChecks
{
    public static void Run()
    {
        CheckBigEndianPrimitiveLayout();
        CheckBigEndianPrimitiveRoundTrip();
        CheckModifiedUtfEncoding();
        CheckModifiedUtfValidation();
        CheckStreamOwnershipAndSkipping();
        CheckAccounterQuotaAndDepth();
    }

    private static void CheckBigEndianPrimitiveLayout()
    {
        using var stream = new MemoryStream();
        using (var output = new NbtDataOutput(stream, leaveOpen: true))
        {
            output.WriteSByte(-2);
            output.WriteShort(unchecked((short)0x1234));
            output.WriteUnsignedShort(0xFEDC);
            output.WriteInt(unchecked((int)0x89ABCDEF));
            output.WriteLong(unchecked((long)0x0123456789ABCDEF));
            output.WriteFloat(1.0f);
            output.WriteDouble(-2.0);
        }

        byte[] expected = Convert.FromHexString(
            "FE1234FEDC89ABCDEF0123456789ABCDEF3F800000C000000000000000");
        Require(
            stream.ToArray().SequenceEqual(expected),
            "NBT numeric output must use the exact big-endian Java DataOutput layout.");
    }

    private static void CheckBigEndianPrimitiveRoundTrip()
    {
        byte[] source = Convert.FromHexString(
            "FE1234FEDC89ABCDEF0123456789ABCDEF3F800000C000000000000000");
        using var stream = new MemoryStream(source, writable: false);
        using var input = new NbtDataInput(stream, leaveOpen: true);

        Require(input.ReadSByte() == -2, "NBT signed-byte reads must preserve two's-complement values.");
        Require(input.ReadShort() == 0x1234, "NBT signed-short reads must be big-endian.");
        Require(input.ReadUnsignedShort() == 0xFEDC, "NBT unsigned-short reads must preserve all bits.");
        Require(input.ReadInt() == unchecked((int)0x89ABCDEF), "NBT int reads must be big-endian.");
        Require(input.ReadLong() == unchecked((long)0x0123456789ABCDEF), "NBT long reads must be big-endian.");
        Require(input.ReadFloat() == 1.0f, "NBT float reads must preserve the IEEE-754 bit pattern.");
        Require(input.ReadDouble() == -2.0, "NBT double reads must preserve the IEEE-754 bit pattern.");
        Require(stream.Position == stream.Length, "The NBT reader must consume exactly the scalar payload.");
    }

    private static void CheckModifiedUtfEncoding()
    {
        const string value = "A\0é漢😀";
        using var stream = new MemoryStream();
        using (var output = new NbtDataOutput(stream, leaveOpen: true))
            output.WriteUTF(value);

        byte[] expected = Convert.FromHexString(
            "000E41C080C3A9E6BCA2EDA0BDEDB880");
        Require(
            stream.ToArray().SequenceEqual(expected),
            "NBT string output must use Java modified UTF-8, including NUL and UTF-16 surrogate code units.");

        stream.Position = 0;
        using var input = new NbtDataInput(stream, leaveOpen: true);
        Require(input.ReadUTF() == value, "NBT modified UTF-8 must round-trip BMP and supplementary characters.");

        using var emptyStream = new MemoryStream();
        using (var emptyOutput = new NbtDataOutput(emptyStream, leaveOpen: true))
            emptyOutput.WriteUTF(string.Empty);
        Require(
            emptyStream.ToArray().SequenceEqual(new byte[] { 0, 0 }),
            "An empty NBT string must still write its unsigned-short zero length.");
    }

    private static void CheckModifiedUtfValidation()
    {
        using var tooLongStream = new MemoryStream();
        using var output = new NbtDataOutput(tooLongStream, leaveOpen: true);
        Capture<IOException>(
            () => output.WriteUTF(new string('x', ushort.MaxValue + 1)),
            "Modified UTF output must reject a payload that cannot fit its unsigned-short byte length.");
        Require(
            tooLongStream.Length == 0,
            "An oversized modified UTF string must be rejected before writing a partial length or payload.");

        using var malformedStream = new MemoryStream(new byte[] { 0, 1, 0xFF });
        using var input = new NbtDataInput(malformedStream);
        Capture<IOException>(
            () => input.ReadUTF(),
            "Modified UTF input must reject unsupported leading-byte forms.");
    }

    private static void CheckStreamOwnershipAndSkipping()
    {
        var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        using (var input = new NbtDataInput(stream, leaveOpen: true))
        {
            Require(input.SkipBytes(2) == 2, "NBT SkipBytes must return the number of bytes skipped.");
            Require(input.ReadByte() == 3, "NBT SkipBytes must advance the underlying stream.");
            Require(input.SkipBytes(10) == 1, "NBT SkipBytes must stop at end-of-stream and report actual progress.");
        }

        Require(stream.CanRead, "leaveOpen must preserve caller-owned streams after disposing the NBT wrapper.");

        using var closedStream = new MemoryStream();
        var ownedOutput = new NbtDataOutput(closedStream);
        ownedOutput.Dispose();
        Capture<ObjectDisposedException>(
            () => ownedOutput.WriteByte(1),
            "Disposed NBT stream wrappers must reject further writes.");
        Require(!closedStream.CanWrite, "The default output wrapper must dispose its owned stream.");
    }

    private static void CheckAccounterQuotaAndDepth()
    {
        var accounter = new NbtAccounter(quota: 32, maxDepth: 2);
        accounter.AccountBytes(4);
        accounter.AccountBytes(2, 3);
        Require(accounter.Usage == 10, "NBT memory accounting must accumulate scalar and repeated-entry allocations.");

        accounter.PushDepth();
        accounter.PushDepth();
        Capture<NbtAccounterException>(
            () => accounter.PushDepth(),
            "NBT nesting must fail before entering a level beyond the configured maximum.");
        accounter.PopDepth();
        accounter.PopDepth();
        Require(accounter.Depth == 0, "Every successful NBT depth push must be balanced by a pop.");

        var utfAccounter = new NbtAccounter(quota: 8);
        Require(
            utfAccounter.ReadUTF("abcd") == "abcd" && utfAccounter.Usage == 8,
            "NBT string accounting must charge two bytes per UTF-16 code unit.");
        Capture<NbtAccounterException>(
            () => utfAccounter.AccountBytes(1),
            "NBT memory accounting must reject the first allocation beyond quota.");
        Require(
            utfAccounter.Usage == 8,
            "A failed NBT quota check must not mutate accounted usage.");

        Require(
            NbtAccounter.DefaultQuota().Quota == NbtAccounter.DEFAULT_NBT_QUOTA &&
            NbtAccounter.UncompressedQuota().Quota == NbtAccounter.UNCOMPRESSED_NBT_QUOTA &&
            NbtAccounter.UnlimitedHeap().Quota == long.MaxValue,
            "NBT quota factories must expose the vanilla default, uncompressed and unlimited budgets.");
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