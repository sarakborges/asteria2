using Asteria.Serialization.Nbt;

internal static class NbtContainerTagChecks
{
    public static void Run()
    {
        CheckTagTypeDispatch();
        CheckListTagRoundTrip();
        CheckHeterogeneousListWrapping();
        CheckListMalformedDataAndDepth();
        CheckCompoundTagRoundTrip();
        CheckCompoundDuplicateNamesAndAccounting();
        CheckContainerSkipping();
        CheckCompoundDepthAndCopies();
    }

    private static void CheckTagTypeDispatch()
    {
        TagType[] expected =
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

        Require(
            TagTypes.Count == 13 &&
            TagTypes.All.Count == expected.Length &&
            expected.Select((type, index) =>
                ReferenceEquals(TagTypes.GetType((byte)index), type)).All(static value => value),
            "The NBT type table must resolve every defined ID 0 through 12 to its canonical singleton.");
        Require(
            TagTypes.GetType((byte)99).Id == 99 &&
            TagTypes.GetType((byte)99).Name == "INVALID[99]",
            "Unknown NBT IDs must resolve to a diagnostic invalid type rather than a different registered type.");
        Capture<IOException>(
            () => TagTypes.GetType((byte)99).Load(
                new NbtDataInput(new MemoryStream(), leaveOpen: true),
                new NbtAccounter(32)),
            "Unknown NBT types must fail explicitly when a payload is requested.");
    }

    private static void CheckListTagRoundTrip()
    {
        var list = new ListTag();
        list.AddTag(0, IntTag.ValueOf(12));
        list.AddTag(1, IntTag.ValueOf(-34));
        Require(
            list.GetId() == 9 &&
            ReferenceEquals(list.GetTagType(), ListTag.TYPE) &&
            list.IdentifyRawElementType() == 3 &&
            list.Size() == 2 &&
            list.SizeInBytes() == 36 + 8 + 24 &&
            list.AsList() == list,
            "ListTag must expose its type, homogeneous element ID, count, memory footprint and Tag view.");

        using var stream = new MemoryStream();
        using (var output = new NbtDataOutput(stream, leaveOpen: true))
            list.Write(output);

        Require(
            stream.ToArray().SequenceEqual(Convert.FromHexString("03000000020000000CFFFFFFDE")),
            "A homogeneous int list must write element ID, count and the ordered payloads.");

        stream.Position = 0;
        using var input = new NbtDataInput(stream, leaveOpen: true);
        var accounter = new NbtAccounter(68);
        ListTag decoded = ListTag.TYPE.LoadTyped(input, accounter);
        Require(
            decoded.Equals(list) &&
            decoded.Get(0).AsInt() == 12 &&
            decoded.Get(1).AsInt() == -34 &&
            accounter.Usage == list.SizeInBytes() &&
            accounter.Depth == 0,
            "ListTag must round-trip homogeneous values, account container/references/children and restore recursion depth.");

        ListTag copy = (ListTag)list.Copy();
        Require(
            copy.Equals(list) && !ReferenceEquals(copy, list),
            "ListTag.Copy must preserve equality while creating a distinct list.");
    }

    private static void CheckHeterogeneousListWrapping()
    {
        var list = new ListTag();
        list.AddTag(0, IntTag.ValueOf(5));
        list.AddTag(1, StringTag.ValueOf("x"));
        Require(
            list.IdentifyRawElementType() == 10,
            "A heterogeneous in-memory list must serialize as a list of wrapped compounds.");

        using var stream = new MemoryStream();
        using (var output = new NbtDataOutput(stream, leaveOpen: true))
            list.Write(output);

        byte[] payload = stream.ToArray();
        Require(
            payload[0] == 10 &&
            payload.Length > 7,
            "Heterogeneous list output must use TAG_Compound as its on-disk element ID.");

        stream.Position = 0;
        using var input = new NbtDataInput(stream, leaveOpen: true);
        ListTag decoded = ListTag.TYPE.LoadTyped(input, new NbtAccounter(4096));
        Require(
            decoded.Size() == 2 &&
            decoded.Get(0).Equals(IntTag.ValueOf(5)) &&
            decoded.Get(1).Equals(StringTag.ValueOf("x")),
            "ListTag load must remove the one-empty-name compound wrapper used by heterogeneous list serialization.");

        var explicitWrapper = new CompoundTag();
        explicitWrapper.Put(string.Empty, ByteTag.ValueOf((sbyte)9));
        var wrappedList = new ListTag();
        wrappedList.AddTag(0, explicitWrapper);
        using var wrappedStream = new MemoryStream();
        using (var wrappedOutput = new NbtDataOutput(wrappedStream, leaveOpen: true))
            wrappedList.Write(wrappedOutput);
        wrappedStream.Position = 0;
        using var wrappedInput = new NbtDataInput(wrappedStream, leaveOpen: true);
        ListTag roundTrippedWrapper = ListTag.TYPE.LoadTyped(wrappedInput, new NbtAccounter(4096));
        Require(
            roundTrippedWrapper.Get(0) is CompoundTag retainedWrapper &&
            retainedWrapper.Size() == 1 &&
            retainedWrapper.Get(string.Empty)!.Equals(ByteTag.ValueOf((sbyte)9)),
            "A one-empty-name compound item must be restored after exactly one outer heterogeneous-list wrapper is removed.");
    }

    private static void CheckListMalformedDataAndDepth()
    {
        using var missingTypeStream = new MemoryStream(Convert.FromHexString("0000000001"));
        using var missingTypeInput = new NbtDataInput(missingTypeStream, leaveOpen: true);
        Capture<NbtFormatException>(
            () => ListTag.TYPE.LoadTyped(missingTypeInput, new NbtAccounter(1024)),
            "A non-empty list must not use END as its element type.");

        using var negativeCountStream = new MemoryStream(Convert.FromHexString("0000FFFFFFFF"));
        using var negativeCountInput = new NbtDataInput(negativeCountStream, leaveOpen: true);
        Capture<NbtFormatException>(
            () => ListTag.TYPE.LoadTyped(negativeCountInput, new NbtAccounter(1024)),
            "ListTag must reject negative element counts.");

        using var emptyStream = new MemoryStream(Convert.FromHexString("0000000000"));
        using var emptyInput = new NbtDataInput(emptyStream, leaveOpen: true);
        var zeroDepth = new NbtAccounter(1024, maxDepth: 0);
        Capture<NbtAccounterException>(
            () => ListTag.TYPE.LoadTyped(emptyInput, zeroDepth),
            "ListTag must enforce the configured recursion depth.");
        Require(zeroDepth.Depth == 0,
            "A failed ListTag depth push must not leak recursion depth.");
    }

    private static void CheckCompoundTagRoundTrip()
    {
        var compound = new CompoundTag();
        compound.PutInt("health", 20);
        compound.PutString("name", "golem");
        compound.PutBoolean("active", true);
        compound.PutByteArray("bytes", new byte[] { 1, 2, 3 });
        var list = new ListTag();
        list.AddTag(0, StringTag.ValueOf("north"));
        list.AddTag(1, StringTag.ValueOf("south"));
        compound.PutList("directions", list);

        Require(
            compound.GetId() == 10 &&
            ReferenceEquals(compound.GetTagType(), CompoundTag.TYPE) &&
            compound.Size() == 5 &&
            compound.GetIntOr("health", -1) == 20 &&
            compound.GetStringOr("name", string.Empty) == "golem" &&
            compound.GetBooleanOr("active", false) &&
            compound.GetCompoundOrEmpty("missing").IsEmpty() &&
            compound.GetByteArray("bytes")!.SequenceEqual(new byte[] { 1, 2, 3 }),
            "CompoundTag must expose named primitive, list, array and default accessors.");

        using var stream = new MemoryStream();
        using (var output = new NbtDataOutput(stream, leaveOpen: true))
            compound.Write(output);

        stream.Position = 0;
        using var input = new NbtDataInput(stream, leaveOpen: true);
        CompoundTag decoded = CompoundTag.TYPE.LoadTyped(input, new NbtAccounter(16_384));
        Require(
            decoded.Equals(compound) &&
            decoded.GetListOrEmpty("directions").Get(1).AsString() == "south",
            "CompoundTag must round-trip named nested tags through the complete type dispatch table.");

        CompoundTag copy = (CompoundTag)compound.Copy();
        ((ByteArrayTag)compound.Get("bytes")!).Data[0] = 99;
        Require(
            ((ByteArrayTag)copy.Get("bytes")!).Data[0] == 1,
            "CompoundTag.Copy must deep-copy nested mutable payload tags.");
    }

    private static void CheckCompoundDuplicateNamesAndAccounting()
    {
        using var stream = new MemoryStream();
        using (var output = new NbtDataOutput(stream, leaveOpen: true))
        {
            output.WriteByte(3);
            output.WriteUTF("x");
            output.WriteInt(1);
            output.WriteByte(3);
            output.WriteUTF("x");
            output.WriteInt(2);
            output.WriteByte(0);
        }

        stream.Position = 0;
        using var input = new NbtDataInput(stream, leaveOpen: true);
        var accounter = new NbtAccounter(168);
        CompoundTag compound = CompoundTag.TYPE.LoadTyped(input, accounter);
        Require(
            compound.Size() == 1 &&
            compound.GetIntOr("x", -1) == 2 &&
            accounter.Usage == 168,
            "Repeated compound names must overwrite the prior value without charging a second map entry, while both encoded values and names are accounted.");
        Require(accounter.Depth == 0,
            "A successfully loaded compound must restore its recursion depth.");
    }

    private static void CheckContainerSkipping()
    {
        var compound = new CompoundTag();
        compound.PutInt("id", 7);
        var list = new ListTag();
        list.AddTag(0, StringTag.ValueOf("alpha"));
        list.AddTag(1, StringTag.ValueOf("beta"));
        compound.PutList("names", list);

        using var stream = new MemoryStream();
        using (var output = new NbtDataOutput(stream, leaveOpen: true))
        {
            compound.Write(output);
            compound.Write(output);
        }
        stream.Position = 0;
        using var input = new NbtDataInput(stream, leaveOpen: true);
        var accounter = new NbtAccounter(24);
        CompoundTag.TYPE.Skip(input, 2, accounter);
        Require(
            stream.Position == stream.Length &&
            accounter.Usage == 24 &&
            accounter.Depth == 0,
            "Compound/list skip paths must account fixed-size scalar values while avoiding skipped compound entries, names and string allocations.");
    }

    private static void CheckCompoundDepthAndCopies()
    {
        using var emptyStream = new MemoryStream(new byte[] { 0 });
        using var input = new NbtDataInput(emptyStream, leaveOpen: true);
        var zeroDepth = new NbtAccounter(1024, maxDepth: 0);
        Capture<NbtAccounterException>(
            () => CompoundTag.TYPE.LoadTyped(input, zeroDepth),
            "CompoundTag must enforce the configured recursion depth.");
        Require(zeroDepth.Depth == 0,
            "A failed CompoundTag depth push must not leak recursion depth.");

        var compound = new CompoundTag();
        compound.PutInt("value", 1);
        Require(compound.Remove("value")!.Equals(IntTag.ValueOf(1)) && compound.IsEmpty(),
            "CompoundTag remove must return the removed value and update emptiness.");
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
