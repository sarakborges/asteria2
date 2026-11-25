using Asteria.Serialization.Nbt;

internal static class NbtArrayTagChecks
{
    public static void Run()
    {
        CheckArrayMetadataAndAccounting();
        CheckArrayRoundTrips();
        CheckCollectionMutationAndViews();
        CheckTypeSkipping();
        CheckMalformedLengths();
    }

    private static void CheckArrayMetadataAndAccounting()
    {
        var bytes = new ByteArrayTag(new byte[] { 0, 127, 128, 255 });
        var ints = new IntArrayTag(new[] { 0, -1, int.MinValue, int.MaxValue });
        var longs = new LongArrayTag(new[] { 0L, -1L, long.MinValue, long.MaxValue });

        CheckMetadata(bytes, 7, "BYTE[]", "TAG_Byte_Array", 28);
        CheckMetadata(ints, 11, "INT[]", "TAG_Int_Array", 40);
        CheckMetadata(longs, 12, "LONG[]", "TAG_Long_Array", 56);

        Require(
            ReferenceEquals(bytes.AsByteArray(), bytes.Data) &&
            ReferenceEquals(ints.AsIntArray(), ints.Data) &&
            ReferenceEquals(longs.AsLongArray(), longs.Data),
            "Primitive array tag accessors must expose the same typed backing arrays.");
    }

    private static void CheckMetadata(
        CollectionTag value,
        byte id,
        string name,
        string prettyName,
        long expectedSize)
    {
        Tag tag = (Tag)value;
        TagType type = tag.GetTagType();
        Require(
            tag.GetId() == id &&
            type.Id == id &&
            type.Name == name &&
            type.PrettyName == prettyName &&
            tag.SizeInBytes() == expectedSize &&
            value.Size() == 4 &&
            value.Count() == 4,
            $"{name} must expose its vanilla ID, type metadata, element count and memory size.");
        Require(ReferenceEquals(tag.Copy().GetType(), tag.GetType()) &&
                !ReferenceEquals(tag.Copy(), tag),
            $"{name} copy must preserve its type and produce a new tag instance.");
    }

    private static void CheckArrayRoundTrips()
    {
        CheckRoundTrip(
            new ByteArrayTag(new byte[] { 0x00, 0x7F, 0x80, 0xFF }),
            "00000004007F80FF",
            28);
        CheckRoundTrip(
            new IntArrayTag(new[] { 0x01020304, -1, int.MinValue }),
            "0000000301020304FFFFFFFF80000000",
            36);
        CheckRoundTrip(
            new LongArrayTag(new[] { 0x0102030405060708L, -1L, long.MinValue }),
            "000000030102030405060708FFFFFFFFFFFFFFFF8000000000000000",
            48);
    }

    private static void CheckRoundTrip(CollectionTag value, string expectedHex, long expectedSize)
    {
        using var stream = new MemoryStream();
        using (var output = new NbtDataOutput(stream, leaveOpen: true))
            ((Tag)value).Write(output);
        Require(
            stream.ToArray().SequenceEqual(Convert.FromHexString(expectedHex)),
            $"{value.GetType().Name} must write its length-prefixed, big-endian NBT payload.");

        stream.Position = 0;
        using var input = new NbtDataInput(stream, leaveOpen: true);
        var accounter = new NbtAccounter(expectedSize);
        Tag decoded = value.GetTagType().Load(input, accounter);
        Require(
            decoded.Equals(value) &&
            decoded.SizeInBytes() == expectedSize &&
            accounter.Usage == expectedSize,
            $"{value.GetType().Name} must read its payload and charge base-plus-elements allocation.");
    }

    private static void CheckCollectionMutationAndViews()
    {
        var bytes = new ByteArrayTag(new byte[] { 1, 2, 3 });
        Require(bytes.Get(1).Equals(ByteTag.ValueOf((sbyte)2)),
            "ByteArrayTag indexed reads must box signed byte values.");
        Require(bytes.SetTag(1, ByteTag.ValueOf(8)) && bytes.Data.SequenceEqual(new byte[] { 1, 8, 3 }),
            "ByteArrayTag setTag must accept only byte tags and replace the selected value.");
        Require(!bytes.SetTag(1, IntTag.ValueOf(9)),
            "ByteArrayTag setTag must reject mismatched scalar tag types.");
        Require(bytes.AddTag(1, ByteTag.ValueOf((sbyte)-2)) &&
                bytes.Data.SequenceEqual(new byte[] { 1, 254, 8, 3 }),
            "ByteArrayTag addTag must insert signed byte payloads at the requested index.");
        Require(bytes.Remove(2).Equals(ByteTag.ValueOf((sbyte)8)) &&
                bytes.Data.SequenceEqual(new byte[] { 1, 254, 3 }),
            "ByteArrayTag remove must return the removed signed byte and close the gap.");

        var ints = new IntArrayTag(new[] { 1, 2 });
        Require(ints.AddTag(2, IntTag.ValueOf(3)) &&
                ints.SetTag(0, IntTag.ValueOf(-1)) &&
                ints.Data.SequenceEqual(new[] { -1, 2, 3 }),
            "IntArrayTag mutation must use int scalar tags and preserve insertion order.");
        Require(!ints.AddTag(0, LongTag.ValueOf(4)),
            "IntArrayTag must reject a long scalar value.");
        ints.Clear();
        Require(ints.IsEmpty() && ints.SizeInBytes() == 24,
            "Clearing an int array must retain only its object memory cost.");

        var longs = new LongArrayTag(new[] { 1L, 2L });
        Require(longs.AddTag(1, LongTag.ValueOf(9)) &&
                longs.Remove(0).Equals(LongTag.ValueOf(1)) &&
                longs.Data.SequenceEqual(new[] { 9L, 2L }),
            "LongArrayTag mutation must use long scalar tags and preserve order.");
        Require(!longs.SetTag(1, IntTag.ValueOf(3)),
            "LongArrayTag must reject an int scalar value.");

        CollectionTag iterable = longs;
        Require(iterable.Select(tag => tag.AsLong()).SequenceEqual(new long?[] { 9L, 2L }),
            "CollectionTag iteration must expose one boxed scalar tag per array element.");

        Tag copied = longs.Copy();
        longs.Data[0] = 10;
        Require(((LongArrayTag)copied).Data[0] == 9L,
            "Primitive array tag copies must own a separate backing array.");
    }

    private static void CheckTypeSkipping()
    {
        using var byteStream = new MemoryStream(Convert.FromHexString(
            "0000000201020000000103"));
        using var byteInput = new NbtDataInput(byteStream, leaveOpen: true);
        ByteArrayTag.TYPE.Skip(byteInput, 2, new NbtAccounter(0));
        Require(byteStream.Position == byteStream.Length,
            "BYTE[] variable-size skipping must consume each length-prefixed payload.");

        using var intStream = new MemoryStream(Convert.FromHexString(
            "00000002010203040000000500000001"));
        using var intInput = new NbtDataInput(intStream, leaveOpen: true);
        IntArrayTag.TYPE.Skip(intInput, 2, new NbtAccounter(0));
        Require(intStream.Position == intStream.Length,
            "INT[] variable-size skipping must consume each big-endian payload.");

        using var longStream = new MemoryStream(Convert.FromHexString(
            "00000001010203040506070800000002FFFFFFFFFFFFFFFF0000000000000001"));
        using var longInput = new NbtDataInput(longStream, leaveOpen: true);
        LongArrayTag.TYPE.Skip(longInput, 2, new NbtAccounter(0));
        Require(longStream.Position == longStream.Length,
            "LONG[] variable-size skipping must consume each big-endian payload.");
    }

    private static void CheckMalformedLengths()
    {
        foreach (TagType type in new TagType[]
        {
            ByteArrayTag.TYPE,
            IntArrayTag.TYPE,
            LongArrayTag.TYPE
        })
        {
            using var stream = new MemoryStream(Convert.FromHexString("FFFFFFFF"));
            using var input = new NbtDataInput(stream, leaveOpen: true);
            try
            {
                type.Load(input, new NbtAccounter(1024));
            }
            catch (NbtFormatException)
            {
                continue;
            }

            throw new InvalidOperationException(
                $"{type.Name} must reject a negative element count as malformed NBT.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
