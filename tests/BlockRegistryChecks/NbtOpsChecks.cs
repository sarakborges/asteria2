using Asteria.Serialization;
using Asteria.Serialization.Nbt;
using System.Text.Json;

internal static class NbtOpsChecks
{
    public static void Run()
    {
        CheckPrimitiveOperations();
        CheckNumericAndBooleanSemantics();
        CheckMapOperations();
        CheckListOperations();
        CheckPrimitiveArrayPromotion();
        CheckRemoveAndShallowCopy();
        CheckDynamicConversion();
    }

    private static void CheckPrimitiveOperations()
    {
        NbtOps ops = NbtOps.INSTANCE;
        Require(
            ops.IsEmpty(ops.Empty) &&
            ReferenceEquals(ops.Empty, EndTag.INSTANCE) &&
            ops.EmptyMap is CompoundTag &&
            ops.EmptyList is ListTag &&
            !ops.CompressMaps,
            "NBT ops must use TAG_End for empty, compounds for maps and non-compressed map encoding.");

        Require(
            ops.CreateByte(-3) is ByteTag { Value: -3 } &&
            ops.CreateShort(123) is ShortTag { Value: 123 } &&
            ops.CreateInt(456) is IntTag { Value: 456 } &&
            ops.CreateLong(789L) is LongTag { Value: 789 } &&
            ops.CreateFloat(1.25f) is FloatTag { Value: 1.25f } &&
            ops.CreateDouble(2.5) is DoubleTag { Value: 2.5 },
            "NBT numeric creation must preserve the requested scalar type.");

        Require(
            ops.CreateNumeric(NumberValue.Of(1.25f)) is DoubleTag { Value: 1.25 } &&
            ops.CreateBoolean(true) is ByteTag { Value: 1 } &&
            ops.CreateString("value") is StringTag { Value: "value" },
            "DynamicOps generic numeric creation must use a double tag, booleans a byte tag and strings a string tag.");

        Require(
            ops.GetStringValue(IntTag.ValueOf(5)).IsError &&
            ops.GetNumberValue(StringTag.ValueOf("5")).IsError,
            "NBT primitive readers must reject type coercions not defined by the format.");
    }

    private static void CheckNumericAndBooleanSemantics()
    {
        NbtOps ops = NbtOps.INSTANCE;
        Require(
            ops.GetNumberValue(ByteTag.ValueOf(-2)).GetOrThrow().IntValue == -2 &&
            ops.GetNumberValue(DoubleTag.ValueOf(1.5)).GetOrThrow().DoubleValue == 1.5,
            "NBT numeric lookup must retain the source numeric tag value.");

        Require(
            ops.GetBooleanValue(ByteTag.ValueOf(1)).GetOrThrow() &&
            !ops.GetBooleanValue(ByteTag.ZERO).GetOrThrow() &&
            ops.GetBooleanValue(FloatTag.ValueOf(-0.5f)).GetOrThrow(),
            "NBT boolean lookup must interpret any non-zero numeric value as true.");

        Require(
            ops.CreateUnsignedByte(255) is ByteTag { Value: -1 } &&
            ops.CreateUnsignedShort(65535) is ShortTag { Value: -1 } &&
            ops.CreateUnsignedInt(uint.MaxValue) is IntTag { Value: -1 } &&
            ops.CreateUnsignedLong(ulong.MaxValue) is LongTag { Value: -1 },
            "Unsigned numeric helpers must preserve their low bits in NBT signed scalar payloads.");
    }

    private static void CheckMapOperations()
    {
        NbtOps ops = NbtOps.INSTANCE;
        Tag first = ops.MergeToMap(
            ops.Empty,
            ops.CreateString("alpha"),
            IntTag.ValueOf(1)).GetOrThrow();
        Tag second = ops.MergeToMap(
            first,
            ops.CreateString("beta"),
            StringTag.ValueOf("two")).GetOrThrow();

        Require(
            first is CompoundTag original &&
            second is CompoundTag updated &&
            original.GetIntOr("alpha", -1) == 1 &&
            !original.Contains("beta") &&
            updated.GetStringOr("beta", "") == "two" &&
            !ReferenceEquals(original, updated),
            "Map merging must create a shallow-copied compound while preserving prior values.");

        MapLike<Tag> map = ops.GetMap(second).GetOrThrow();
        Require(
            map.Get("alpha").GetOrThrow().AsInt() == 1 &&
            map.Get(ops.CreateString("beta")).GetOrThrow().AsString() == "two" &&
            map.TryGet("beta", out Tag found) && found.AsString() == "two" &&
            map.Entries.Count == 2,
            "MapLike lookup and entries must use NBT string keys.");

        DataResult<Tag> invalidMap = ops.MergeToMap(
            StringTag.ValueOf("not-map"),
            ops.CreateString("key"),
            IntTag.ValueOf(1));
        Require(
            invalidMap.IsError && invalidMap.TryGetPartial(out Tag partialMap) &&
            partialMap.Equals(StringTag.ValueOf("not-map")),
            "Merging a scalar as a map must preserve the original value as the partial result.");

        DataResult<Tag> invalidKey = ops.MergeToMap(
            second,
            IntTag.ValueOf(7),
            IntTag.ValueOf(8));
        Require(
            invalidKey.IsError &&
            invalidKey.TryGetPartial(out Tag partialKey) &&
            partialKey is CompoundTag partialCompound &&
            partialCompound.Equals(second),
            "A non-string map key must return the original compound as a partial result.");

        DataResult<Tag> mixedKeys = ops.MergeToMap(
            ops.Empty,
            new KeyValuePair<Tag, Tag>[]
            {
                new(ops.CreateString("valid"), IntTag.ValueOf(3)),
                new(IntTag.ValueOf(1), IntTag.ValueOf(4))
            });
        Require(
            mixedKeys.IsError &&
            mixedKeys.TryGetPartial(out Tag mixedPartial) &&
            mixedPartial is CompoundTag mixedCompound &&
            mixedCompound.GetIntOr("valid", -1) == 3,
            "A batch map merge must retain valid string-keyed entries while reporting invalid keys.");

        Tag createdMap = ops.CreateMap(new[]
        {
            new KeyValuePair<Tag, Tag>(StringTag.ValueOf("a"), IntTag.ValueOf(1)),
            new KeyValuePair<Tag, Tag>(StringTag.ValueOf("b"), IntTag.ValueOf(2))
        });
        Require(
            createdMap is CompoundTag createdCompound && createdCompound.Size() == 2,
            "NBT map creation must create a compound from string-keyed entries.");
        Capture<NotSupportedException>(
            () => ops.CreateMap(new[]
            {
                new KeyValuePair<Tag, Tag>(IntTag.ValueOf(1), IntTag.ValueOf(2))
            }),
            "NBT map creation must reject keys that are not string tags.");

        Require(
            ops.GetMapValues(second).GetOrThrow().Count == 2 &&
            ops.Get(second, "missing").IsError &&
            ops.GetMap(StringTag.ValueOf("not-map")).IsError,
            "NBT map inspection must distinguish valid compounds, missing keys and non-map input.");
    }

    private static void CheckListOperations()
    {
        NbtOps ops = NbtOps.INSTANCE;
        Tag first = IntTag.ValueOf(1);
        Tag second = StringTag.ValueOf("two");
        Tag created = ops.CreateList(new[] { first, second });
        Require(
            created is ListTag list &&
            list.Size() == 2 &&
            ops.GetList(created).GetOrThrow().SequenceEqual(new[] { first, second }),
            "NBT list creation and extraction must preserve the list's ordered tag values.");

        Tag emptyMerged = ops.MergeToList(ops.Empty, Array.Empty<Tag>()).GetOrThrow();
        Require(
            emptyMerged is ListTag emptyList && emptyList.Size() == 0,
            "Merging an empty value sequence into TAG_End must produce an empty list tag.");

        Tag merged = ops.MergeToList(ops.EmptyList, IntTag.ValueOf(3)).GetOrThrow();
        merged = ops.MergeToList(merged, StringTag.ValueOf("four")).GetOrThrow();
        Require(
            merged is ListTag mixed &&
            mixed.Size() == 2 &&
            mixed.Get(0).Equals(IntTag.ValueOf(3)) &&
            mixed.Get(1).Equals(StringTag.ValueOf("four")),
            "Generic list collection must retain insertion order and heterogeneous tag values.");

        Tag invalid = ops.MergeToList(IntTag.ValueOf(9), StringTag.ValueOf("x")).GetPartialOrThrow();
        Require(
            invalid.Equals(IntTag.ValueOf(9)),
            "Merging into a non-list must preserve the original prefix as its partial value.");
    }

    private static void CheckPrimitiveArrayPromotion()
    {
        NbtOps ops = NbtOps.INSTANCE;

        Tag bytes = new ByteArrayTag(new byte[] { 1, 2 });
        Tag sameBytes = ops.MergeToList(bytes, ByteTag.ValueOf((sbyte)-1)).GetOrThrow();
        Require(
            sameBytes is ByteArrayTag byteArray &&
            byteArray.Data.SequenceEqual(new byte[] { 1, 2, 255 }),
            "Appending byte tags to a byte-array prefix must preserve BYTE_ARRAY storage and its raw bit pattern.");

        Tag promotedBytes = ops.MergeToList(bytes, IntTag.ValueOf(3)).GetOrThrow();
        Require(
            promotedBytes is ListTag byteList &&
            byteList.Get(0).Equals(ByteTag.ValueOf((sbyte)1)) &&
            byteList.Get(1).Equals(ByteTag.ValueOf((sbyte)2)) &&
            byteList.Get(2).Equals(IntTag.ValueOf(3)),
            "The first non-byte value must promote a byte-array collector to a generic list without losing existing signed values.");

        Tag ints = new IntArrayTag(new[] { 5, 6 });
        Tag sameInts = ops.MergeToList(ints, IntTag.ValueOf(7)).GetOrThrow();
        Require(
            sameInts is IntArrayTag intArray &&
            intArray.Data.SequenceEqual(new[] { 5, 6, 7 }),
            "Appending int tags to an int-array prefix must retain INT_ARRAY storage.");

        Tag promotedInts = ops.MergeToList(ints, LongTag.ValueOf(8)).GetOrThrow();
        Require(
            promotedInts is ListTag intList &&
            intList.Get(0).Equals(IntTag.ValueOf(5)) &&
            intList.Get(1).Equals(IntTag.ValueOf(6)) &&
            intList.Get(2).Equals(LongTag.ValueOf(8)),
            "An incompatible long value must promote an int-array collector into an ordered list.");

        Tag longs = new LongArrayTag(new[] { 9L });
        Tag sameLongs = ops.MergeToList(longs, LongTag.ValueOf(10)).GetOrThrow();
        Require(
            sameLongs is LongArrayTag longArray &&
            longArray.Data.SequenceEqual(new[] { 9L, 10L }),
            "Appending long tags to a long-array prefix must retain LONG_ARRAY storage.");

        Tag promotedLongs = ops.MergeToList(longs, ByteTag.ValueOf((sbyte)11)).GetOrThrow();
        Require(
            promotedLongs is ListTag longList &&
            longList.Get(0).Equals(LongTag.ValueOf(9)) &&
            longList.Get(1).Equals(ByteTag.ValueOf((sbyte)11)),
            "An incompatible byte value must promote a long-array collector into an ordered list.");
    }

    private static void CheckRemoveAndShallowCopy()
    {
        NbtOps ops = NbtOps.INSTANCE;
        var nested = new CompoundTag();
        nested.PutInt("value", 1);
        var original = new CompoundTag();
        original.PutCompound("nested", nested);
        original.PutInt("remove", 2);

        CompoundTag removed = (CompoundTag)ops.Remove(original, "remove");
        Require(
            !original.IsEmpty() &&
            original.Contains("remove") &&
            !removed.Contains("remove") &&
            ReferenceEquals(original.Get("nested"), removed.Get("nested")),
            "NBT remove must shallow-copy the top-level compound map but retain references to unchanged child tags.");

        CompoundTag changed = (CompoundTag)ops.MergeToMap(
            original,
            StringTag.ValueOf("new"),
            IntTag.ValueOf(3)).GetOrThrow();
        Require(
            !original.Contains("new") && changed.GetIntOr("new", -1) == 3 &&
            ReferenceEquals(original.Get("nested"), changed.Get("nested")),
            "NBT map merge must share unchanged child tags while owning an independent top-level map.");
    }

    private static void CheckDynamicConversion()
    {
        NbtOps nbt = NbtOps.INSTANCE;
        JsonOps json = JsonOps.INSTANCE;
        var root = new CompoundTag();
        root.PutByte("byte", unchecked((sbyte)255));
        root.PutLong("long", 0x0102030405060708L);
        root.PutBoolean("flag", true);
        root.PutString("name", "cave");
        root.PutIntArray("levels", new[] { 1, 2, 3 });

        JsonElement converted = nbt.ConvertTo(json, root);
        JsonElement byteValue = json.Get(converted, "byte").GetOrThrow();
        JsonElement longValue = json.Get(converted, "long").GetOrThrow();
        JsonElement flagValue = json.Get(converted, "flag").GetOrThrow();
        JsonElement levels = json.Get(converted, "levels").GetOrThrow();
        Require(
            json.GetNumberValue(byteValue).GetOrThrow().IntValue == -1 &&
            json.GetNumberValue(longValue).GetOrThrow().LongValue == 0x0102030405060708L &&
            json.GetNumberValue(flagValue).GetOrThrow().IntValue == 1 &&
            json.GetStringValue(json.Get(converted, "name").GetOrThrow()).GetOrThrow() == "cave" &&
            json.GetList(levels).GetOrThrow().Count == 3,
            "NBT-to-JSON conversion must recursively preserve scalar types, byte-boolean encoding and primitive-array elements.");

        Tag roundTrip = json.ConvertTo(nbt, converted);
        Require(
            roundTrip is CompoundTag result &&
            result.GetByteOr("byte", 0) == -1 &&
            result.GetLongOr("long", 0) == 0x0102030405060708L &&
            result.GetStringOr("name", "") == "cave" &&
            result.Get("levels") is ListTag,
            "JSON-to-NBT conversion must use the DynamicOps scalar and list factories recursively.");

        Tag nbtCopy = nbt.ConvertTo(nbt, root);
        Require(
            nbtCopy.Equals(root) &&
            !ReferenceEquals(nbtCopy, root) &&
            nbtCopy is CompoundTag copied &&
            !ReferenceEquals(copied.Get("levels"), root.Get("levels")),
            "Converting NBT to NBT must preserve specialized primitive arrays while deep-copying the value tree.");
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

        throw new InvalidOperationException(message);
    }
}
