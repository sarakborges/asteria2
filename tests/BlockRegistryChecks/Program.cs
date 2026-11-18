using Asteria.Content.Blocks;
using Asteria.Content.Blocks.StateProperties;
using Asteria.Content.Chunks;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Content.Storage;

internal static class Program
{
    private enum TestAxis { X = 0, Y = 1, Z = 2 }

    private static int Main()
    {
        try
        {
            RunChecks();
            Console.WriteLine("Block registry, block-state, palette, and chunk-section checks passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void RunChecks()
    {
        Require(!Blocks.AIR.DefaultState.HasGlobalId, "State IDs must be assigned during bootstrap.");
        RegistryBootstrap.Initialize();

        Require(RegistryBootstrap.IsInitialized, "Bootstrap must complete.");
        Require(BuiltInRegistries.AreFrozen, "Built-in registries must be frozen.");
        Require(BuiltInRegistries.Block.Count == 2, "Two Asteria blocks should be registered.");
        Require(BuiltInRegistries.BlockStates.Count == 7, "Expected one air state and six test states.");

        CheckDefaultedBlockRegistry();
        CheckBlockStateDefinition();
        CheckBlockStateTransitions();
        CheckGlobalStateIds();
        CheckPropertyImplementations();
        CheckPackedBitStorage();
        CheckPalettedContainer();
        CheckPalettedContainerGrowthAndGlobalMode();
        CheckChunkSection();
        CheckFrozenRegistry();
    }

    private static void CheckDefaultedBlockRegistry()
    {
        ResourceLocation airId = ResourceLocation.Parse("asteria:air");
        ResourceLocation missingId = ResourceLocation.Parse("asteria:not_registered");

        Require(ReferenceEquals(BuiltInRegistries.Block.Get(airId), Blocks.AIR), "The air block must be registered.");
        Require(ReferenceEquals(BuiltInRegistries.Block.Get(missingId), Blocks.AIR), "Unknown block keys must resolve to air.");
        Require(!BuiltInRegistries.Block.TryGetEntry(missingId, out _), "Exact lookups must not hide missing keys.");
        Require(BuiltInRegistries.Block.GetId(new Block(BlockProperties.Default)) == 0, "Unknown objects must resolve to the default block ID.");
    }

    private static void CheckBlockStateDefinition()
    {
        StateDefinition definition = Blocks.TEST_BLOCK.StateDefinition;
        Require(definition.PossibleStates.Count == 6, "powered(2) x stage(3) must create six states.");
        Require(
            definition.Properties.Select(property => property.Name).SequenceEqual(new[] { "powered", "stage" }),
            "State properties must be sorted by name.");
        Require(ReferenceEquals(definition.GetProperty("powered"), Blocks.TEST_POWERED), "Property lookup must return the declared property.");

        BlockState defaultState = definition.DefaultState;
        Require(!defaultState.GetValue(Blocks.TEST_POWERED), "powered default must be false.");
        Require(defaultState.GetValue(Blocks.TEST_STAGE) == 1, "stage default must be one.");
        Require(ReferenceEquals(defaultState, Blocks.TEST_BLOCK.DefaultState), "The default state must be a stable precomputed instance.");
    }

    private static void CheckBlockStateTransitions()
    {
        BlockState defaultState = Blocks.TEST_BLOCK.DefaultState;
        BlockState changedState = defaultState
            .SetValue(Blocks.TEST_STAGE, 2)
            .SetValue(Blocks.TEST_POWERED, true);

        Require(changedState.GetValue(Blocks.TEST_POWERED), "powered must transition to true.");
        Require(changedState.GetValue(Blocks.TEST_STAGE) == 2, "stage must transition to two.");
        Require(
            ReferenceEquals(changedState, defaultState.SetValue(Blocks.TEST_POWERED, true).SetValue(Blocks.TEST_STAGE, 2)),
            "The same property combination must resolve to the same state object.");
        Require(ReferenceEquals(changedState, changedState.SetValue(Blocks.TEST_STAGE, 2)), "Setting an existing value must preserve identity.");
        Require(changedState.Cycle(Blocks.TEST_STAGE).GetValue(Blocks.TEST_STAGE) == 0, "Integer cycling must wrap.");
        Require(defaultState.Cycle(Blocks.TEST_POWERED).GetValue(Blocks.TEST_POWERED), "Boolean cycling must toggle false to true.");

        BooleanProperty foreignPowered = BooleanProperty.Create("powered");
        Require(ReferenceEquals(changedState, changedState.TrySetValue(foreignPowered, false)), "trySetValue with a foreign property instance must preserve the current state.");
        Require(!changedState.TryGetValue(foreignPowered, out _), "A separate property instance must not be accepted by name alone.");
        RequireThrows<ArgumentException>(
            () => changedState.SetValue(foreignPowered, false),
            "SetValue must reject a property not owned by the block definition.");
        RequireThrows<ArgumentOutOfRangeException>(
            () => changedState.TrySetValue(Blocks.TEST_STAGE, 9),
            "trySetValue must reject values outside the property's allowed set.");

        IntegerProperty singletonProperty = IntegerProperty.Create("only", 0, 0);
        RequireThrows<ArgumentException>(
            () => new Block(BlockProperties.Default, builder => builder.Add(singletonProperty)),
            "StateDefinition must reject properties with one or fewer possible values.");
    }

    private static void CheckGlobalStateIds()
    {
        BlockStateIdMap ids = BuiltInRegistries.BlockStates;
        for (int id = 0; id < ids.Count; id++)
        {
            BlockState state = ids.ById(id);
            Require(state.GlobalId == id, "State IDs must be contiguous and match insertion order.");
            Require(ReferenceEquals(ids.ById(state.GlobalId), state), "State ID reverse lookup must round-trip.");
            Require(ids.GetId(state) == id, "State-to-ID lookup must round-trip.");
        }

        Require(ids.GetId(Blocks.AIR.DefaultState) == 0, "The first block's singleton state must receive ID zero.");
        Require(!ids.TryGetById(-1, out _), "Invalid global state IDs must be reported as missing.");
        RequireThrows<ArgumentOutOfRangeException>(() => ids.ById(-1), "Strict state ID lookup must reject invalid IDs.");
    }

    private static void CheckPropertyImplementations()
    {
        BooleanProperty lit = BooleanProperty.Create("lit");
        Require(lit.PossibleValues.SequenceEqual(new[] { false, true }), "Boolean values must be false then true.");
        Require(lit.TryParse("true", out bool parsedTrue) && parsedTrue, "Boolean parsing must accept true.");
        Require(!lit.TryParse("TRUE", out _), "Boolean parsing must be case-sensitive.");

        IntegerProperty age = IntegerProperty.Create("age", 2, 4);
        Require(age.PossibleValues.SequenceEqual(new[] { 2, 3, 4 }), "Integer values must be inclusive and ascending.");
        Require(age.TryParse("3", out int parsedAge) && parsedAge == 3, "Integer parsing must accept in-range values.");
        Require(!age.TryParse("5", out _), "Integer parsing must reject out-of-range values.");

        EnumProperty<TestAxis> axis = EnumProperty<TestAxis>.Create(
            "axis",
            new[] { TestAxis.Z, TestAxis.X },
            value => value.ToString().ToLowerInvariant());

        Require(axis.PossibleValues.SequenceEqual(new[] { TestAxis.X, TestAxis.Z }), "Enum values must be ordered by underlying value.");
        Require(axis.TryParse("z", out TestAxis parsedAxis) && parsedAxis == TestAxis.Z, "Enum parsing must use the configured name.");
    }

    private static void CheckPackedBitStorage()
    {
        var storage = new PackedBitStorage(5, ChunkSection.BlockCount);
        Require(storage.LongCount == 342, "Five-bit storage must use 342 words for 4096 entries.");
        storage.Set(11, 31);
        storage.Set(12, 17);
        storage.Set(13, 9);
        storage.Set(4095, 23);

        Require(storage.Get(11) == 31, "Storage must preserve the last entry before a word boundary.");
        Require(storage.Get(12) == 17, "Storage must preserve the first entry after a word boundary.");
        Require(storage.Get(13) == 9, "Storage must preserve entries after a word boundary.");
        Require(storage.Get(4095) == 23, "Storage must preserve the final entry.");
        Require(storage.GetAndSet(11, 3) == 31, "getAndSet must return the previous value.");
        RequireThrows<ArgumentOutOfRangeException>(() => storage.Set(0, 32), "Storage must reject values that do not fit.");
    }

    private static void CheckPalettedContainer()
    {
        BlockState defaultState = Blocks.TEST_BLOCK.DefaultState;
        var strategy = PalettedContainerStrategy<BlockState>.CreateForBlockStates(BuiltInRegistries.BlockStates);
        var palette = new PalettedContainer<BlockState>(strategy, defaultState);

        Require(palette.Size == ChunkSection.BlockCount, "Block-state strategy must create 4096 entries.");
        Require(palette.Mode == PaletteMode.SingleValue, "A uniform section must use a single-value palette.");
        Require(palette.BitsPerEntry == 0, "A single-value palette must use zero storage bits.");
        Require(palette.StorageLongCount == 0, "A single-value palette must allocate no packed array.");
        Require(ReferenceEquals(palette.Get(2048), defaultState), "A single-value palette must return its only value for every index.");
        Require(!palette.MaybeHas(state => state.Block.Properties.IsAir), "Single-value maybeHas must inspect the only value.");

        BlockState alternate = defaultState.SetValue(Blocks.TEST_STAGE, 2);
        BlockState previous = palette.GetAndSet(10, alternate);
        Require(ReferenceEquals(previous, defaultState), "getAndSet must return the previous state.");
        Require(palette.Mode == PaletteMode.Local, "The second distinct value must create a local palette.");
        Require(palette.BitsPerEntry == 4, "Block-state local palettes start at four bits.");
        Require(palette.LocalPaletteImplementation?.StartsWith("LinearPalette", StringComparison.Ordinal) == true, "The four-bit palette must use linear lookup.");
        Require(ReferenceEquals(palette.Get(10), alternate), "Local palette lookup must round-trip.");
        Require(!palette.MaybeHas(state => state.Block.Properties.IsAir), "Local palette must report absent air.");
        Require(palette.MaybeHas(state => ReferenceEquals(state, alternate)), "Local palette must inspect its entries.");

        int counted = 0;
        palette.Count((_, count) => counted += count);
        Require(counted == ChunkSection.BlockCount, "Palette count must account for every position.");

        PalettedContainer<BlockState> copy = palette.Copy();
        Require(ReferenceEquals(copy.Get(10), alternate), "Copy must preserve stored values.");
        copy.Set(10, defaultState);
        Require(ReferenceEquals(palette.Get(10), alternate), "Mutating a copy must not mutate the original.");

        PalettedContainer<BlockState> recreated = palette.Recreate();
        Require(recreated.IsSingleValue, "Recreate must return a uniform container.");
        Require(ReferenceEquals(recreated.Get(0), defaultState), "Recreate must use palette entry zero.");

        palette.Acquire();
        try
        {
            RequireThrows<InvalidOperationException>(() => palette.Acquire(), "The writer detector must reject a second acquire.");
            palette.GetAndSetUnchecked(11, alternate);
        }
        finally
        {
            palette.Release();
        }
    }

    private static void CheckPalettedContainerGrowthAndGlobalMode()
    {
        IntegerProperty variant = IntegerProperty.Create("variant", 0, 256);
        var manyStateBlock = new Block(
            BlockProperties.Default,
            builder =>
            {
                builder.Add(variant);
                builder.SetDefault(variant, 0);
            });

        var ids = new BlockStateIdMap();
        foreach (BlockState state in manyStateBlock.StateDefinition.PossibleStates)
        {
            ids.Register(state);
        }
        ids.Freeze();

        var strategy = PalettedContainerStrategy<BlockState>.CreateForBlockStates(ids);
        var container = new PalettedContainer<BlockState>(strategy, manyStateBlock.DefaultState);
        IReadOnlyList<BlockState> states = manyStateBlock.StateDefinition.PossibleStates;

        for (int value = 1; value <= 16; value++)
        {
            container.Set(value - 1, states[value]);
        }

        Require(container.Mode == PaletteMode.Local, "A palette of 17 states must remain indirect.");
        Require(container.BitsPerEntry == 5, "A seventeenth value must grow a four-bit palette to five bits.");
        Require(container.LocalPaletteImplementation?.StartsWith("HashMapPalette", StringComparison.Ordinal) == true, "A widened palette must use hash-map lookup.");

        for (int value = 17; value <= 256; value++)
        {
            container.Set(value - 1, states[value]);
        }

        Require(container.Mode == PaletteMode.Global, "The 257th distinct state must promote the palette to global IDs.");
        Require(container.BitsPerEntry == 9, "A 257-entry global state map requires nine bits.");
        Require(container.PaletteSize == 257, "The global palette must expose the full state map.");
        Require(container.MaybeHas(state => ReferenceEquals(state, manyStateBlock.DefaultState)), "Global maybeHas must be conservative.");

        for (int value = 1; value <= 256; value++)
        {
            Require(ReferenceEquals(container.Get(value - 1), states[value]), "Palette promotion must preserve every stored value.");
        }

        Require(ReferenceEquals(container.Get(256), manyStateBlock.DefaultState), "Palette promotion must preserve untouched defaults.");
        RequireThrows<ArgumentException>(
            () => container.Set(300, Blocks.AIR.DefaultState),
            "Paletted containers must reject values absent from their ID map.");
    }

    private static void CheckChunkSection()
    {
        var section = new ChunkSection(BuiltInRegistries.BlockStates, Blocks.AIR.DefaultState);
        Require(section.HasOnlyAir, "A new section must be empty.");
        Require(section.NonAirBlockCount == 0, "A new section must contain zero non-air blocks.");
        Require(section.PaletteMode == PaletteMode.SingleValue, "An empty section must start in single-value mode.");

        Require(ChunkSection.GetIndex(15, 15, 15) == 4095, "The last coordinate must map to 4095.");
        Require(
            ChunkSection.GetIndex(1, 0, 0) == 1 &&
            ChunkSection.GetIndex(0, 0, 1) == 16 &&
            ChunkSection.GetIndex(0, 1, 0) == 256,
            "Section indices must use x | (z << 4) | (y << 8).");

        BlockState placedState = Blocks.TEST_BLOCK.DefaultState;
        BlockState previous = section.SetBlockState(15, 15, 15, placedState);
        Require(ReferenceEquals(previous, Blocks.AIR.DefaultState), "SetBlockState must return the previous state.");
        Require(ReferenceEquals(section.GetBlockState(15, 15, 15), placedState), "Reads must return the placed state.");
        Require(section.NonAirBlockCount == 1, "Placing a non-air block must increment the count.");
        Require(!section.HasOnlyAir, "A section with a non-air block must not be empty.");

        section.SetBlockState(15, 15, 15, placedState.SetValue(Blocks.TEST_STAGE, 2));
        Require(section.NonAirBlockCount == 1, "Replacing one non-air state with another must not change the count.");

        section.SetBlockState(15, 15, 15, Blocks.AIR.DefaultState);
        Require(section.NonAirBlockCount == 0, "Replacing non-air with air must decrement the count.");
        Require(section.HasOnlyAir, "The section must become empty after restoring air.");
        Require(section.MaybeHas(state => !state.Block.Properties.IsAir), "maybeHas may conservatively return true for an unused local entry.");

        var emptySection = new ChunkSection(BuiltInRegistries.BlockStates, Blocks.AIR.DefaultState);
        Require(!emptySection.MaybeHas(state => !state.Block.Properties.IsAir), "An untouched air-only palette must rule out non-air blocks.");

        RequireThrows<ArgumentOutOfRangeException>(() => section.GetBlockState(16, 0, 0), "X coordinates outside 0-15 must be rejected.");
        RequireThrows<ArgumentOutOfRangeException>(() => section.GetBlockState(-1, 0, 0), "Negative coordinates must be rejected.");
    }

    private static void CheckFrozenRegistry()
    {
        RequireThrows<InvalidOperationException>(
            () => BuiltInRegistries.Block.Register(
                ResourceLocation.Parse("asteria:late_block"),
                new Block(BlockProperties.Default)),
            "A frozen block registry must reject new entries.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}
