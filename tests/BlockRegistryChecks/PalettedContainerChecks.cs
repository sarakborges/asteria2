using Asteria.Content.Chunks;
using Asteria.Content.Blocks;
using Asteria.Content.Storage;
using Asteria.Serialization;

internal static class PalettedContainerChecks
{
    public static void Run()
    {
        CheckSingleValueRoundTrip();
        CheckLocalPaletteRoundTrip();
        CheckGlobalPaletteRepacking();
        CheckMalformedPackedData();
        CheckDataCodecs();
    }

    private static void CheckSingleValueRoundTrip()
    {
        PalettedContainerFactory factory = PalettedContainerFactory.CreateBuiltIn();
        PalettedContainer<BlockState> source = factory.CreateForBlockStates();
        IPalettedContainerReadOnly<BlockState> readOnly = source;
        PalettedContainerPackedData<BlockState> packed = readOnly.Pack(factory.BlockStatesStrategy);

        Require(
            packed.PaletteEntries.Count == 1 &&
            ReferenceEquals(packed.PaletteEntries[0], Blocks.AIR.DefaultState) &&
            packed.Storage is null &&
            packed.BitsPerEntry == 0,
            "A single-value container must pack to one palette entry, no long array and zero storage bits.");

        PalettedContainer<BlockState> restored =
            PalettedContainer<BlockState>.Unpack(factory.BlockStatesStrategy, packed).GetOrThrow();

        Require(
            restored.Mode == PaletteMode.SingleValue &&
            ReferenceEquals(restored.Get(15, 15, 15), Blocks.AIR.DefaultState),
            "Unpack must restore every coordinate of a single-value container.");
    }

    private static void CheckLocalPaletteRoundTrip()
    {
        PalettedContainerFactory factory = PalettedContainerFactory.CreateBuiltIn();
        PalettedContainer<BlockState> source = factory.CreateForBlockStates();
        source.Set(1, 0, 0, Blocks.STONE.DefaultState);
        source.Set(2, 0, 0, Blocks.DIRT.DefaultState);
        source.Set(3, 0, 0, Blocks.STONE.DefaultState);
        source.Set(0, 1, 2, Blocks.VOID_AIR.DefaultState);

        PalettedContainerPackedData<BlockState> packed = source.Pack();
        Require(
            packed.PaletteEntries.Count == 4 &&
            packed.BitsPerEntry == 4 &&
            packed.Storage is { Length: > 0 },
            "A local block-state palette must pack the used entries in first-seen order with the configured four-bit storage.");

        PalettedContainer<BlockState> restored =
            PalettedContainer<BlockState>.Unpack(factory.BlockStatesStrategy, packed).GetOrThrow();

        Require(
            restored.Mode == PaletteMode.Local &&
            ReferenceEquals(restored.Get(1, 0, 0), Blocks.STONE.DefaultState) &&
            ReferenceEquals(restored.Get(2, 0, 0), Blocks.DIRT.DefaultState) &&
            ReferenceEquals(restored.Get(3, 0, 0), Blocks.STONE.DefaultState) &&
            ReferenceEquals(restored.Get(0, 1, 2), Blocks.VOID_AIR.DefaultState) &&
            ReferenceEquals(restored.Get(15, 15, 15), Blocks.AIR.DefaultState),
            "Packing and unpacking a local palette must preserve both occupied and default coordinates.");
    }

    private static void CheckGlobalPaletteRepacking()
    {
        var globalMap = CrudeIncrementalIntIdentityHashBiMap<IdentityValue>.Create(512);
        var values = new IdentityValue[300];
        for (int index = 0; index < values.Length; index++)
        {
            values[index] = new IdentityValue(index);
            globalMap.Add(values[index]);
        }

        PalettedContainerStrategy<IdentityValue> strategy =
            PalettedContainerStrategy<IdentityValue>.CreateForBlockStates(globalMap);
        var source = new PalettedContainer<IdentityValue>(values[0], strategy);

        for (int index = 1; index <= 256; index++)
        {
            int x = index & 15;
            int z = index >> 4 & 15;
            int y = index >> 8;
            source.Set(x, y, z, values[index]);
        }

        Require(
            source.Mode == PaletteMode.Global && source.BitsPerEntry == 9,
            "A 257-entry block-state palette must promote to the global palette using the registry's resident bit width.");

        PalettedContainerPackedData<IdentityValue> packed = source.Pack();
        Require(
            packed.PaletteEntries.Count == 257 &&
            packed.BitsPerEntry == 9 &&
            packed.Storage is { Length: > 0 },
            "A global palette must be repacked to compact palette IDs and the serialized palette-size bit width.");

        PalettedContainer<IdentityValue> restored =
            PalettedContainer<IdentityValue>.Unpack(strategy, packed).GetOrThrow();

        Require(
            restored.Mode == PaletteMode.Global &&
            ReferenceEquals(restored.Get(0, 0, 0), values[0]) &&
            ReferenceEquals(restored.Get(1, 0, 0), values[1]) &&
            ReferenceEquals(restored.Get(0, 0, 1), values[16]) &&
            ReferenceEquals(restored.Get(1, 0, 1), values[17]) &&
            ReferenceEquals(restored.Get(0, 0, 15), values[240]) &&
            ReferenceEquals(restored.Get(0, 1, 0), values[256]),
            "Global palette unpack must rebuild registry IDs from compact disk IDs and preserve XYZ coordinates.");
    }


    private static void CheckDataCodecs()
    {
        PalettedContainerFactory factory = PalettedContainerFactory.CreateBuiltIn();
        DynamicOps<System.Text.Json.JsonElement> ops = JsonOps.INSTANCE;
        Codec<PalettedContainer<BlockState>> codec =
            PalettedContainer<BlockState>.CodecRW(
                BlockState.CODEC,
                factory.BlockStatesStrategy,
                Blocks.AIR.DefaultState);

        PalettedContainer<BlockState> single = factory.CreateForBlockStates();
        System.Text.Json.JsonElement singleJson = codec.EncodeStart(ops, single).GetOrThrow();
        Require(
            ops.Get(singleJson, "palette").IsSuccess &&
            ops.Get(singleJson, "data").IsError,
            "A single-value palette codec must write the palette field and omit the optional data field.");
        PalettedContainer<BlockState> singleDecoded = codec.Parse(ops, singleJson).GetOrThrow();
        Require(
            ReferenceEquals(singleDecoded.Get(15, 15, 15), Blocks.AIR.DefaultState),
            "The single-value palette record codec must restore the default block state.");

        PalettedContainer<BlockState> source = factory.CreateForBlockStates();
        source.Set(1, 0, 0, Blocks.STONE.DefaultState);
        source.Set(2, 0, 0, Blocks.DIRT.DefaultState);
        System.Text.Json.JsonElement encoded = codec.EncodeStart(ops, source).GetOrThrow();
        Require(
            ops.Get(encoded, "palette").IsSuccess &&
            ops.Get(encoded, "data").IsSuccess,
            "A multi-value palette codec must serialize both palette entries and packed data.");

        PalettedContainer<BlockState> decoded = codec.Parse(ops, encoded).GetOrThrow();
        Require(
            ReferenceEquals(decoded.Get(1, 0, 0), Blocks.STONE.DefaultState) &&
            ReferenceEquals(decoded.Get(2, 0, 0), Blocks.DIRT.DefaultState) &&
            ReferenceEquals(decoded.Get(15, 15, 15), Blocks.AIR.DefaultState),
            "The local palette data codec must round-trip block states and untouched defaults.");

        Codec<IPalettedContainerReadOnly<BlockState>> readOnlyCodec =
            PalettedContainer<BlockState>.CodecRO(
                BlockState.CODEC,
                factory.BlockStatesStrategy,
                Blocks.AIR.DefaultState);
        IPalettedContainerReadOnly<BlockState> readOnlyDecoded =
            readOnlyCodec.Parse(ops, encoded).GetOrThrow();
        Require(
            ReferenceEquals(readOnlyDecoded.Get(1, 0, 0), Blocks.STONE.DefaultState),
            "The read-only palette codec must decode into a container with the read-only contract.");

        var globalMap = CrudeIncrementalIntIdentityHashBiMap<IdentityValue>.Create(512);
        var values = new IdentityValue[300];
        for (int index = 0; index < values.Length; index++)
        {
            values[index] = new IdentityValue(index);
            globalMap.Add(values[index]);
        }

        PalettedContainerStrategy<IdentityValue> strategy =
            PalettedContainerStrategy<IdentityValue>.CreateForBlockStates(globalMap);
        var global = new PalettedContainer<IdentityValue>(values[0], strategy);
        for (int index = 1; index <= 256; index++)
        {
            global.Set(index & 15, index >> 8, index >> 4 & 15, values[index]);
        }

        Codec<IdentityValue> elementCodec = Codecs.INT.XMap(
            id => values[id],
            value => value.Id);
        Codec<PalettedContainer<IdentityValue>> globalCodec =
            PalettedContainer<IdentityValue>.CodecRW(elementCodec, strategy, values[0]);
        System.Text.Json.JsonElement globalJson = globalCodec.EncodeStart(ops, global).GetOrThrow();
        PalettedContainer<IdentityValue> globalDecoded =
            globalCodec.Parse(ops, globalJson).GetOrThrow();

        Require(
            globalDecoded.Mode == PaletteMode.Global &&
            ReferenceEquals(globalDecoded.Get(0, 1, 0), values[256]) &&
            ReferenceEquals(globalDecoded.Get(0, 0, 1), values[16]),
            "The record codec must round-trip global palette entries through compact saved IDs.");

        Codec<IdentityValue> rejectingCodec = Codecs.INT.FlatXMap(
            _ => DataResult<IdentityValue>.Error("rejected palette entry"),
            value => DataResult<int>.Success(value.Id));
        Codec<PalettedContainer<IdentityValue>> fallbackCodec =
            PalettedContainer<IdentityValue>.CodecRW(rejectingCodec, strategy, values[0]);
        System.Text.Json.JsonElement invalidPalette = ops.CreateMap(new[]
        {
            new KeyValuePair<System.Text.Json.JsonElement, System.Text.Json.JsonElement>(
                ops.CreateString("palette"),
                ops.CreateList(new[] { ops.CreateInt(17) }))
        });
        DataResult<PalettedContainer<IdentityValue>> fallbackResult =
            fallbackCodec.Parse(ops, invalidPalette);
        Require(
            fallbackResult.IsError &&
            fallbackResult.TryGetPartial(out PalettedContainer<IdentityValue> fallback) &&
            ReferenceEquals(fallback.Get(0, 0, 0), values[0]) &&
            fallbackResult.ErrorMessage!.Contains(
                "rejected palette entry -> using default",
                StringComparison.Ordinal),
            "An invalid palette element must retain its decode error while providing the configured default as a partial container.");
    }

    private static void CheckMalformedPackedData()
    {
        PalettedContainerFactory factory = PalettedContainerFactory.CreateBuiltIn();
        PalettedContainer<BlockState> source = factory.CreateForBlockStates();
        source.Set(0, 0, 0, Blocks.STONE.DefaultState);
        PalettedContainerPackedData<BlockState> packed = source.Pack();

        var wrongBits = new PalettedContainerPackedData<BlockState>(
            packed.PaletteEntries, packed.Storage, packed.BitsPerEntry - 1);
        DataResult<PalettedContainer<BlockState>> wrongBitsResult =
            PalettedContainer<BlockState>.Unpack(factory.BlockStatesStrategy, wrongBits);
        Require(
            wrongBitsResult.IsError &&
            wrongBitsResult.ErrorMessage == "Invalid bit count, calculated 4, but container declared 3",
            "Unpack must reject a declared storage bit count that differs from the strategy-derived count.");

        var missingStorage = new PalettedContainerPackedData<BlockState>(
            packed.PaletteEntries, null, packed.BitsPerEntry);
        DataResult<PalettedContainer<BlockState>> missingStorageResult =
            PalettedContainer<BlockState>.Unpack(factory.BlockStatesStrategy, missingStorage);
        Require(
            missingStorageResult.IsError &&
            missingStorageResult.ErrorMessage == "Missing values for non-zero storage",
            "Unpack must reject absent storage when the selected palette requires packed values.");

        var invalidLength = new PalettedContainerPackedData<BlockState>(
            packed.PaletteEntries, new ulong[1], packed.BitsPerEntry);
        DataResult<PalettedContainer<BlockState>> invalidLengthResult =
            PalettedContainer<BlockState>.Unpack(factory.BlockStatesStrategy, invalidLength);
        Require(
            invalidLengthResult.IsError &&
            invalidLengthResult.ErrorMessage!.StartsWith(
                "Failed to read PalettedContainer: Invalid length given for storage",
                StringComparison.Ordinal),
            "Unpack must turn invalid packed-word counts into the reference initialization error result.");

        var missingPaletteEntryStorage = new PackedBitStorage(
            packed.BitsPerEntry, factory.BlockStatesStrategy.EntryCount);
        missingPaletteEntryStorage.Set(0, 15);
        var missingPaletteEntry = new PalettedContainerPackedData<BlockState>(
            packed.PaletteEntries, missingPaletteEntryStorage.GetRawCopy(), packed.BitsPerEntry);
        PalettedContainer<BlockState> corruptContainer =
            PalettedContainer<BlockState>.Unpack(
                factory.BlockStatesStrategy, missingPaletteEntry).GetOrThrow();
        try
        {
            _ = corruptContainer.Get(0, 0, 0);
        }
        catch (MissingPaletteEntryException)
        {
            return;
        }

        throw new InvalidOperationException(
            "Reading a packed cell that references an absent local-palette entry must raise MissingPaletteEntryException.");
    }

    private sealed class IdentityValue
    {
        public int Id { get; }

        public IdentityValue(int id) => Id = id;

        public override string ToString() => Id.ToString();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
