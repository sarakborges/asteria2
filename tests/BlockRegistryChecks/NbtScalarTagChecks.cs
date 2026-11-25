using System.Globalization;
using Asteria.Serialization;
using Asteria.Serialization.Nbt;

internal static class NbtScalarTagChecks
{
    public static void Run()
    {
        CheckEndTag();
        CheckNumericMetadataAndCaching();
        CheckNumericConversions();
        CheckNumericBinaryRoundTrips();
        CheckStringTag();
        CheckTypeSkippingAndAccounting();
    }

    private static void CheckEndTag()
    {
        Require(
            ReferenceEquals(EndTag.INSTANCE.Copy(), EndTag.INSTANCE) &&
            EndTag.INSTANCE.GetId() == 0 &&
            ReferenceEquals(EndTag.INSTANCE.GetTagType(), EndTag.TYPE) &&
            EndTag.INSTANCE.SizeInBytes() == 8 &&
            EndTag.TYPE.Size == 0 &&
            EndTag.TYPE.Name == "END" &&
            EndTag.TYPE.PrettyName == "TAG_End",
            "END must be the canonical zero-payload tag with ID 0 and the vanilla accounting size.");

        using var stream = new MemoryStream();
        using (var output = new NbtDataOutput(stream, leaveOpen: true))
            EndTag.INSTANCE.Write(output);
        Require(stream.Length == 0, "The END tag must not write payload bytes.");

        using var inputStream = new MemoryStream();
        using var input = new NbtDataInput(inputStream, leaveOpen: true);
        var accounter = new NbtAccounter(8);
        Require(
            ReferenceEquals(EndTag.TYPE.LoadTyped(input, accounter), EndTag.INSTANCE) &&
            accounter.Usage == 8,
            "Loading END must account its object memory without consuming payload bytes.");
    }

    private static void CheckNumericMetadataAndCaching()
    {
        Require(
            ByteTag.ZERO.Value == 0 && ByteTag.ONE.Value == 1 &&
            ReferenceEquals(ByteTag.ValueOf((sbyte)-128), ByteTag.ValueOf((sbyte)-128)) &&
            ReferenceEquals(ByteTag.ValueOf((sbyte)127), ByteTag.ValueOf((sbyte)127)),
            "ByteTag must cache the complete signed-byte range and expose canonical zero/one values.");
        Require(
            ReferenceEquals(ShortTag.ValueOf(-128), ShortTag.ValueOf(-128)) &&
            ReferenceEquals(ShortTag.ValueOf(1024), ShortTag.ValueOf(1024)) &&
            !ReferenceEquals(ShortTag.ValueOf(-129), ShortTag.ValueOf(-129)),
            "ShortTag must cache only the inclusive [-128, 1024] range.");
        Require(
            ReferenceEquals(IntTag.ValueOf(-128), IntTag.ValueOf(-128)) &&
            ReferenceEquals(IntTag.ValueOf(1024), IntTag.ValueOf(1024)) &&
            !ReferenceEquals(IntTag.ValueOf(1025), IntTag.ValueOf(1025)),
            "IntTag must cache only the inclusive [-128, 1024] range.");
        Require(
            ReferenceEquals(LongTag.ValueOf(-128), LongTag.ValueOf(-128)) &&
            ReferenceEquals(LongTag.ValueOf(1024), LongTag.ValueOf(1024)) &&
            !ReferenceEquals(LongTag.ValueOf(1025), LongTag.ValueOf(1025)),
            "LongTag must cache only the inclusive [-128, 1024] range.");
        Require(
            ReferenceEquals(FloatTag.ValueOf(0.0f), FloatTag.ZERO) &&
            ReferenceEquals(FloatTag.ValueOf(-0.0f), FloatTag.ZERO) &&
            ReferenceEquals(DoubleTag.ValueOf(0.0), DoubleTag.ZERO) &&
            ReferenceEquals(DoubleTag.ValueOf(-0.0), DoubleTag.ZERO),
            "Floating tag factories must return their canonical zero values for either signed zero.");

        CheckTagType(ByteTag.ValueOf(2), 1, 9, "BYTE", "TAG_Byte");
        CheckTagType(ShortTag.ValueOf(2), 2, 10, "SHORT", "TAG_Short");
        CheckTagType(IntTag.ValueOf(2), 3, 12, "INT", "TAG_Int");
        CheckTagType(LongTag.ValueOf(2), 4, 16, "LONG", "TAG_Long");
        CheckTagType(FloatTag.ValueOf(2), 5, 12, "FLOAT", "TAG_Float");
        CheckTagType(DoubleTag.ValueOf(2), 6, 16, "DOUBLE", "TAG_Double");
    }

    private static void CheckTagType(
        NumericTag tag,
        byte expectedId,
        long expectedSize,
        string expectedName,
        string expectedPrettyName)
    {
        Tag value = (Tag)tag;
        TagType type = value.GetTagType();
        Require(
            value.GetId() == expectedId &&
            type.Id == expectedId &&
            type.Name == expectedName &&
            type.PrettyName == expectedPrettyName &&
            value.SizeInBytes() == expectedSize &&
            ReferenceEquals(value.Copy(), value),
            $"{expectedName} must preserve its NBT ID, type metadata, self-size and immutable copy behavior.");
    }

    private static void CheckNumericConversions()
    {
        NumericTag shortTag = ShortTag.ValueOf(unchecked((short)0x1234));
        Require(
            shortTag.ByteValue() == 0x34 &&
            shortTag.ShortValue() == 0x1234 &&
            shortTag.IntValue() == 0x1234 &&
            shortTag.LongValue() == 0x1234L,
            "Short numeric conversions must preserve signed widening and low-bit narrowing.");

        NumericTag longTag = LongTag.ValueOf(unchecked((long)0x1234567887654321));
        Require(
            longTag.ByteValue() == 0x21 &&
            longTag.ShortValue() == unchecked((short)0x4321) &&
            longTag.IntValue() == unchecked((int)0x87654321) &&
            longTag.LongValue() == unchecked((long)0x1234567887654321),
            "Long numeric conversions must use Java's low-bit narrowing semantics.");

        NumericTag nan = DoubleTag.ValueOf(double.NaN);
        Require(
            nan.IntValue() == 0 &&
            nan.LongValue() == 0 &&
            nan.ByteValue() == 0 &&
            nan.ShortValue() == 0,
            "Floating-to-integral numeric conversions must map NaN to zero.");

        NumericTag positiveInfinity = DoubleTag.ValueOf(double.PositiveInfinity);
        NumericTag negativeInfinity = DoubleTag.ValueOf(double.NegativeInfinity);
        Require(
            positiveInfinity.IntValue() == int.MaxValue &&
            positiveInfinity.LongValue() == long.MaxValue &&
            negativeInfinity.IntValue() == int.MinValue &&
            negativeInfinity.LongValue() == long.MinValue,
            "Floating-to-integral numeric conversions must saturate at the Java target limits.");

        Tag optionalViews = IntTag.ValueOf(-12);
        Require(
            optionalViews.AsNumber() is NumberValue boxedNumber &&
            boxedNumber.Equals(NumberValue.Of(-12)) &&
            optionalViews.AsString() == "-12" &&
            optionalViews.AsInt() == -12,
            "Numeric tags must expose their boxed number and optional primitive views through Tag.");
    }

    private static void CheckNumericBinaryRoundTrips()
    {
        CheckNumericRoundTrip(ByteTag.ValueOf(unchecked((sbyte)0xFE)), "FE");
        CheckNumericRoundTrip(ShortTag.ValueOf(unchecked((short)0xFEDC)), "FEDC");
        CheckNumericRoundTrip(IntTag.ValueOf(unchecked((int)0x89ABCDEF)), "89ABCDEF");
        CheckNumericRoundTrip(LongTag.ValueOf(unchecked((long)0x0123456789ABCDEF)), "0123456789ABCDEF");
        CheckNumericRoundTrip(FloatTag.ValueOf(1.0f), "3F800000");
        CheckNumericRoundTrip(DoubleTag.ValueOf(-2.0), "C000000000000000");

        Require(ByteTag.ValueOf(5).ToString() == "5b", "Byte SNBT text must use the b suffix.");
        Require(ShortTag.ValueOf(5).ToString() == "5s", "Short SNBT text must use the s suffix.");
        Require(IntTag.ValueOf(5).ToString() == "5", "Int SNBT text must not add a suffix.");
        Require(LongTag.ValueOf(5).ToString() == "5L", "Long SNBT text must use the L suffix.");
        Require(FloatTag.ValueOf(1.0f).ToString() == "1.0f", "Float SNBT text must preserve a decimal point and f suffix.");
        Require(DoubleTag.ValueOf(1.0).ToString() == "1.0d", "Double SNBT text must preserve a decimal point and d suffix.");
    }

    private static void CheckNumericRoundTrip(NumericTag value, string expectedHex)
    {
        using var stream = new MemoryStream();
        using (var output = new NbtDataOutput(stream, leaveOpen: true))
            ((Tag)value).Write(output);

        byte[] expected = Convert.FromHexString(expectedHex);
        Require(
            stream.ToArray().SequenceEqual(expected),
            $"NBT scalar tag {value.GetType().Name} must write its exact big-endian payload.");

        stream.Position = 0;
        using var input = new NbtDataInput(stream, leaveOpen: true);
        var accounter = new NbtAccounter(64);
        Tag decoded = value.GetTagType().Load(input, accounter);
        Require(
            decoded.Equals(value) &&
            accounter.Usage == ((Tag)value).SizeInBytes(),
            $"NBT scalar tag {value.GetType().Name} must round-trip through its typed descriptor and charge its memory cost.");
    }

    private static void CheckStringTag()
    {
        const string value = "A\0é漢😀";
        StringTag tag = StringTag.ValueOf(value);
        Require(
            tag.GetId() == 8 &&
            ReferenceEquals(tag.GetTagType(), StringTag.TYPE) &&
            tag.SizeInBytes() == 48 &&
            tag.AsString() == value &&
            ReferenceEquals(StringTag.ValueOf(string.Empty), StringTag.EMPTY),
            "StringTag must expose tag ID 8, canonical empty, exact UTF-16 memory accounting and original content.");

        using var stream = new MemoryStream();
        using (var output = new NbtDataOutput(stream, leaveOpen: true))
            ((Tag)tag).Write(output);

        stream.Position = 0;
        using var input = new NbtDataInput(stream, leaveOpen: true);
        var accounter = new NbtAccounter(64);
        StringTag decoded = StringTag.TYPE.LoadTyped(input, accounter);
        Require(
            decoded.Value == value &&
            accounter.Usage == tag.SizeInBytes(),
            "StringTag payload loading must round-trip modified UTF-8 and account for fixed plus UTF-16 storage.");
    }

    private static void CheckTypeSkippingAndAccounting()
    {
        byte[] bytes = Convert.FromHexString("0102030405060708");
        using var stream = new MemoryStream(bytes);
        using var input = new NbtDataInput(stream, leaveOpen: true);
        var accounter = new NbtAccounter(24);
        IntTag.TYPE.Skip(input, 2, accounter);
        Require(
            stream.Position == 8 &&
            accounter.Usage == 24,
            "Static-size int tag skipping must consume two payloads and account the same self-size as loading.");

        using var strings = new MemoryStream();
        using (var output = new NbtDataOutput(strings, leaveOpen: true))
        {
            output.WriteUTF("abc");
            output.WriteUTF("é");
        }
        strings.Position = 0;
        using var stringInput = new NbtDataInput(strings, leaveOpen: true);
        var stringAccounter = new NbtAccounter(100);
        StringTag.TYPE.Skip(stringInput, 2, stringAccounter);
        Require(
            strings.Position == strings.Length &&
            stringAccounter.Usage == 36 * 2 + 2 * 4,
            "Variable-size string tag skipping must account for each tag and each UTF-16 code unit.");

        TagType invalid = TagType.Invalid(99);
        Require(invalid.Name == "INVALID[99]" && invalid.PrettyName == "UNKNOWN_99",
            "Unknown NBT tag types must retain their numeric ID in diagnostic names.");
        using var emptyStream = new MemoryStream();
        using var emptyInput = new NbtDataInput(emptyStream, leaveOpen: true);
        Capture<IOException>(
            () => invalid.Load(emptyInput, new NbtAccounter(32)),
            "An invalid NBT type must fail explicitly rather than substituting another tag.");
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