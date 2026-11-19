using Asteria.Content.Blocks;
using Asteria.Content.Blocks.StateProperties;
using Asteria.Content.Chunks;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Content.Storage;

internal static class Program
{
    private enum TestAxis { X = 0, Y = 1, Z = 2 }
    private enum TestDeclaredOrder { Z = 2, X = 0, Y = 1 }

    private static int Main()
    {
        try
        {
            RunChecks();
            Console.WriteLine("Block registry, holder, tag, block-state, palette, and chunk-section checks passed.");
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
        Require(
            BuiltInRegistries.BlockStates.GetId(Blocks.AIR.DefaultState) == -1,
            "Block states must remain absent from the global ID map until bootstrap.");
        RegistryBootstrap.Initialize();
        Require(RegistryBootstrap.IsInitialized, "Bootstrap must complete.");
        Require(BuiltInRegistries.AreFrozen, "Built-in registries must be frozen.");
        Require(BuiltInRegistries.Block.Count == 2, "Two Asteria blocks should be registered.");
        Require(BuiltInRegistries.BlockStates.Count == 7, "Expected one air state and six test states.");

        CheckResourceLocations();
        CheckDefaultedBlockRegistry();
        CheckBlockProperties();
        CheckRegistryHoldersAndTags();
        CheckBlockStateDefinition();
        CheckBlockStateTransitions();
        CheckGlobalStateIds();
        CheckPropertyImplementations();
        CheckPackedBitStorage();
        CheckPaletteExceptions();
        CheckPalettedContainer();
        CheckPalettedContainerGrowthAndGlobalMode();
        CheckChunkSection();
        CheckFrozenRegistry();
    }

    private static void CheckResourceLocations()
    {
        Require(
            ResourceLocation.Parse("stone") == ResourceLocation.Parse("minecraft:stone"),
            "Identifier.parse must apply the minecraft default namespace when none is supplied.");
        Require(
            ResourceLocation.Parse(":stone") == ResourceLocation.Parse("minecraft:stone"),
            "Identifier.parse must apply the default namespace when the separator is first.");
        Require(
            ResourceLocation.TryParse("stone", out ResourceLocation parsed) &&
            parsed == ResourceLocation.Parse("minecraft:stone"),
            "Identifier.tryParse must accept valid identifiers without an explicit namespace.");
        Require(
            !ResourceLocation.TryParse("..:stone", out _),
            "Identifier namespaces must reject the reserved '..' namespace.");
    }

    private static void CheckDefaultedBlockRegistry()
    {
        ResourceLocation airId = ResourceLocation.Parse("asteria:air");
        ResourceLocation testId = ResourceLocation.Parse("asteria:registry_test");
        ResourceLocation missingId = ResourceLocation.Parse("asteria:not_registered");

        Require(ReferenceEquals(BuiltInRegistries.Block.Get(airId), Blocks.AIR), "The air block must be registered.");
        Require(ReferenceEquals(BuiltInRegistries.Block.Get(missingId), Blocks.AIR), "Unknown block keys must resolve to air.");
        Require(ReferenceEquals(BuiltInRegistries.Block.GetValue(missingId), Blocks.AIR), "Defaulted GetValue lookups must resolve unknown identifiers to air.");
        Require(ReferenceEquals(BuiltInRegistries.Block.GetAny(), Blocks.AIR.BuiltInRegistryHolder), "DefaultedMappedRegistry.getAny must return its default holder.");
        Require(BuiltInRegistries.Block.GetHolder(missingId) is null, "Exact holder lookups must not hide missing keys.");
        Require(BuiltInRegistries.Block.GetId(new Block(BlockProperties.Default)) == 0, "Unknown objects must resolve to the default block ID.");
        Require(BuiltInRegistries.Block.GetId(null!) == 0, "Null reverse lookups in a defaulted registry must resolve to the default ID.");
        Require(BuiltInRegistries.Block.GetKey(null!) == airId, "Null key lookups in a defaulted registry must resolve to the default key.");
        Require(
            ReferenceEquals(
                BuiltInRegistries.Block.GetHolderById(999),
                Blocks.AIR.BuiltInRegistryHolder),
            "Unknown numeric IDs must resolve to the default holder.");
        Require(ReferenceEquals(BuiltInRegistries.Block.ById(999), Blocks.AIR), "DefaultedMappedRegistry.byId must resolve unknown IDs to its default value.");

        Holder<Block>.Reference registered = BuiltInRegistries.Block.GetHolderOrThrow(
            new RegistryKey<Block>(BuiltInRegistries.Block.Location, testId));
        Require(ReferenceEquals(registered, Blocks.TEST_BLOCK.BuiltInRegistryHolder), "Registration must bind the pre-created intrusive holder.");
        Require(registered.IsBound, "The built-in holder must be fully bound after freeze.");
        Require(ReferenceEquals(registered.Value, Blocks.TEST_BLOCK), "The holder must resolve to its registered block.");
        Require(registered.CanSerializeIn(BuiltInRegistries.Block), "A reference holder must serialize in its owning registry.");
        Require(ReferenceEquals(BuiltInRegistries.Block.WrapAsHolder(Blocks.TEST_BLOCK), registered), "wrapAsHolder must reuse registered references.");

        Block directValue = new(BlockProperties.Default);
        Holder<Block> direct = BuiltInRegistries.Block.WrapAsHolder(directValue);
        Require(direct.Kind == HolderKind.Direct, "Unregistered values must be wrapped as direct holders.");
        Require(direct.CanSerializeIn(BuiltInRegistries.Block), "Direct holders are serializable in any registry context.");
        Require(direct.Is(Holder<Block>.Direct(directValue)), "Direct holders compare by their wrapped value.");
        Require(direct.Equals(Holder<Block>.Direct(directValue)), "Direct holder record equality compares wrapped values.");
        Require(!registered.Is(direct), "A reference holder must not match a direct holder by value.");
        Require(direct.Is(registered) == false || ReferenceEquals(registered.Value, directValue), "Direct holder matching follows wrapped-value equality.");
    }

    private static void CheckBlockProperties()
    {
        BlockProperties defaults = BlockProperties.Default;
        Require(defaults.DestroyTime == 0.0f, "Vanilla block properties default destroy time to zero.");
        Require(defaults.ExplosionResistance == 0.0f, "Vanilla block properties default explosion resistance to zero.");
        Require(defaults.Friction == 0.6f, "Vanilla block properties default friction to 0.6.");
        Require(!defaults.RequiresCorrectToolForDrops && !defaults.IsAir, "Vanilla block properties default tool and air flags to false.");

        var custom = new BlockProperties(destroyTime: -2.0f, explosionResistance: -3.0f, friction: -0.25f);
        Require(custom.DestroyTime == -2.0f, "Vanilla destroyTime stores the supplied value without clamping.");
        Require(custom.ExplosionResistance == 0.0f, "Vanilla explosionResistance clamps negative values to zero.");
        Require(custom.Friction == -0.25f, "Vanilla friction stores the supplied value without clamping.");
    }

    private static void CheckRegistryHoldersAndTags()
    {
        ResourceLocation registryId = ResourceLocation.Parse("asteria:test_registry");
        ResourceLocation entryId = ResourceLocation.Parse("asteria:test_entry");
        ResourceLocation placeholderId = ResourceLocation.Parse("asteria:missing_placeholder");
        ResourceLocation tagId = ResourceLocation.Parse("asteria:test_tag");

        var registry = new Registry<Block>(registryId);
        Require(registry.IsEmpty(), "A registry with no keys must be empty.");
        Require(registry.GetAny() is null, "MappedRegistry.getAny must be empty before its first registered value.");
        Require(registry.KeySet().Count == 0 && registry.RegistryKeySet().Count == 0 && !registry.EntrySet().Any(), "Empty registry key and entry sets must be empty.");
        Require(registry.GetId(null!) == -1, "A non-defaulted registry must return -1 for a null reverse lookup.");
        Require(registry.GetKey(null!) is null, "A non-defaulted registry must return null for a null key lookup.");
        Require(registry.ById(-1) is null, "MappedRegistry.byId must return null for an invalid numeric ID.");
        var entryKey = new RegistryKey<Block>(registryId, entryId);
        var placeholderKey = new RegistryKey<Block>(registryId, placeholderId);
        var tagKey = new TagKey<Block>(registryId, tagId);
        HolderGetter<Block> lookup = registry.CreateRegistrationLookup();

        Holder<Block>.Reference placeholder = lookup.GetOrThrow(placeholderKey);
        Require(!registry.IsEmpty(), "A standalone key placeholder makes the registry non-empty.");
        Require(registry.GetAny() is null, "Standalone key placeholders do not create registered numeric IDs.");
        Require(registry.RegistryKeySet().Contains(placeholderKey), "registryKeySet must include standalone key placeholders.");
        Require(registry.KeySet().Count == 0, "keySet must contain only registered identifier lookups.");
        Require(registry.Count == 1, "Registry size must include standalone key placeholders, matching MappedRegistry.size().");
        Require(!placeholder.IsBound, "A registration lookup creates an unbound standalone holder.");
        RequireThrows<InvalidOperationException>(() => _ = placeholder.Value, "An unbound holder value must not be readable.");

        Block value = new(BlockProperties.Default);
        Holder<Block>.Reference registered = lookup.GetOrThrow(entryKey);
        Require(registry.Count == 2, "Each distinct standalone key must contribute to registry size before registration.");
        Holder<Block>.Reference entry = registry.Register(entryId, value);
        Require(ReferenceEquals(entry, registered), "Registration must reuse the standalone holder.");
        Require(registry.Count == 2, "Registering a reserved key must not change registry size.");
        Require(!registered.IsBound, "Standalone holder values are bound during registry freeze.");

        Block placeholderValue = new(BlockProperties.Default);
        Holder<Block>.Reference placeholderEntry = registry.Register(placeholderId, placeholderValue);
        Require(ReferenceEquals(placeholderEntry, placeholder), "Registering a placeholder key must bind the existing holder.");

        HolderSet<Block>.Named tag = lookup.GetOrThrow(tagKey);
        Require(!tag.IsBound, "A registration-time tag starts unbound.");
        RequireThrows<InvalidOperationException>(
            () => registered.Is(tagKey),
            "Holder.Reference tag membership must reject tag queries before tags are bound.");
        RequireThrows<InvalidOperationException>(
            () => registered.Tags.ToArray(),
            "Holder.Reference tag enumeration must reject access before tags are bound.");
        registry.BindTags(new[]
        {
            new KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>(
                tagKey,
                new Holder<Block>[] { registered, registered })
        });
        Require(tag.IsBound, "Binding tags must bind the named holder set.");
        Require(tag.Count == 2, "Named holder sets must preserve the supplied contents, including duplicates.");
        Require(tag.Contains(registered), "A named holder set must recognize a holder through its tag membership.");
        Require(!tag.Contains(Holder<Block>.Direct(value)), "A named holder set must not match direct holders.");
        RequireThrows<InvalidOperationException>(() => registry.GetTag(tagKey), "Tag access must fail before registry freeze.");

        registry.Freeze();
        Require(ReferenceEquals(registry.GetAny(), registered), "MappedRegistry.getAny must return the first registered holder.");
        Require(registry.KeySet().Count == 2, "keySet must contain registered entry identifiers.");
        Require(registry.RegistryKeySet().Count == 2, "registryKeySet must contain registered and reserved keys.");
        Require(registry.EntrySet().Count() == 2, "entrySet must expose registered keys and their values.");
        Require(registered.IsBound, "Freeze must bind registered standalone values.");
        Require(ReferenceEquals(registered.Value, value), "The frozen holder must resolve to the registered value.");
        Require(registered.Is(tagKey), "Freeze must associate tags with their holders.");
        Require(ReferenceEquals(registry.GetTag(tagKey), tag), "The frozen registry must expose its registered tag set.");
        Require(registry.GetTagOrEmpty(new TagKey<Block>(registryId, ResourceLocation.Parse("asteria:empty"))).Count == 0, "Missing tags must resolve to an empty holder set.");

        HolderSet<Block> directSet = new(new Holder<Block>[] { registered, registered });
        Require(directSet.Count == 2, "Direct holder sets must preserve order and duplicate entries.");
        Require(directSet.CanSerializeIn(registry), "Direct holder sets are serializable regardless of the contained holder owner.");
        Require(directSet.Contains(registered), "Direct holder set membership must use holder equality.");

        var parallelRegistry = new Registry<Block>(registryId);
        Holder<Block>.Reference parallelReference =
            parallelRegistry.Register(entryId, new Block(BlockProperties.Default));
        parallelRegistry.Freeze();
        Require(!registered.Equals(parallelReference), "Reference holders from separate registry instances must retain object identity.");
        Require(registered.Is(parallelReference), "Reference holder matching must follow the resource key, not holder instance identity.");
        Require(parallelReference.Is(registered), "Reference holder key matching must work in both directions.");
        Require(!registered.Is(Holder<Block>.Direct(value)), "Reference holder matching must reject direct holders.");

        RequireThrows<InvalidOperationException>(
            () => registry.BindTags(Array.Empty<KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>>()),
            "Direct tag binding must be rejected after freeze.");

        registry.PrepareTagReload(Array.Empty<KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>>()).Apply();
        Require(!registered.Is(tagKey), "A tag reload must replace holder tag membership.");
        Require(registry.GetTag(tagKey) is null, "Tags omitted from a reload must no longer be present.");

        var invalidRegistry = new Registry<Block>(ResourceLocation.Parse("asteria:invalid_registry"));
        invalidRegistry.CreateRegistrationLookup().GetOrThrow(
            new RegistryKey<Block>(invalidRegistry.Location, ResourceLocation.Parse("asteria:never_registered")));
        RequireThrows<InvalidOperationException>(invalidRegistry.Freeze, "Freeze must reject unbound key placeholders.");

        var intrusiveRegistry = new Registry<Block>(ResourceLocation.Parse("asteria:intrusive_registry"), allowIntrusiveHolders: true);
        RequireThrows<InvalidOperationException>(
            () => intrusiveRegistry.Register(entryId, new Block(BlockProperties.Default)),
            "Intrusive registries must reject values without a pre-created intrusive holder.");
    }

    private static void CheckBlockStateDefinition()
    {
        StateDefinition definition = Blocks.TEST_BLOCK.StateDefinition;
        Require(definition.PossibleStates.Count == 6, "powered(2) x stage(3) must create six states.");
        Require(
            definition.Properties.Select(property => property.Name).SequenceEqual(new[] { "powered", "stage" }),
            "State properties must be sorted by name.");
        Require(ReferenceEquals(definition.GetProperty("powered"), Blocks.TEST_POWERED), "Property lookup must return the declared property.");
        Require(
            definition.Any().GetValue(Blocks.TEST_POWERED),
            "The first state must follow Minecraft's BooleanProperty order: true before false.");

        BlockState defaultState = Blocks.TEST_BLOCK.DefaultState;
        Require(!defaultState.GetValue(Blocks.TEST_POWERED), "powered default must be false.");
        Require(defaultState.GetValue(Blocks.TEST_STAGE) == 1, "stage default must be one.");
        Require(ReferenceEquals(defaultState, Blocks.TEST_BLOCK.DefaultState), "The default state must be a stable precomputed instance.");
    }

    private static void CheckBlockStateTransitions()
    {
        BlockState defaultState = Blocks.TEST_BLOCK.DefaultState;
        BlockState changedState = defaultState.SetValue(Blocks.TEST_STAGE, 2).SetValue(Blocks.TEST_POWERED, true);
        Require(changedState.GetValue(Blocks.TEST_POWERED), "powered must transition to true.");
        Require(changedState.GetValue(Blocks.TEST_STAGE) == 2, "stage must transition to two.");
        Require(ReferenceEquals(changedState, defaultState.SetValue(Blocks.TEST_POWERED, true).SetValue(Blocks.TEST_STAGE, 2)), "The same property combination must resolve to the same state object.");
        Require(ReferenceEquals(changedState, changedState.SetValue(Blocks.TEST_STAGE, 2)), "Setting an existing value must preserve identity.");
        Require(changedState.Cycle(Blocks.TEST_STAGE).GetValue(Blocks.TEST_STAGE) == 0, "Integer cycling must wrap.");
        Require(defaultState.Cycle(Blocks.TEST_POWERED).GetValue(Blocks.TEST_POWERED), "Boolean cycling must toggle false to true.");

        BooleanProperty foreignPowered = BooleanProperty.Create("powered");
        Require(ReferenceEquals(changedState, changedState.TrySetValue(foreignPowered, false)), "trySetValue with a foreign property instance must preserve the current state.");
        Require(!changedState.TryGetValue(foreignPowered, out _), "A separate property instance must not be accepted by name alone.");
        RequireThrows<ArgumentException>(() => changedState.SetValue(foreignPowered, false), "SetValue must reject a property not owned by the block definition.");
        RequireThrows<ArgumentOutOfRangeException>(() => changedState.TrySetValue(Blocks.TEST_STAGE, 9), "trySetValue must reject values outside the property's allowed set.");

        RequireThrows<ArgumentOutOfRangeException>(
            () => IntegerProperty.Create("only", 0, 0),
            "Minecraft integer properties require at least two possible values.");

        RequireThrows<ArgumentOutOfRangeException>(
            () => IntegerProperty.Create("negative", -1, 1),
            "Minecraft integer properties must not include negative values.");

        BooleanProperty invalidName = BooleanProperty.Create("Invalid Name");
        RequireThrows<ArgumentException>(
            () => new Block(BlockProperties.Default, builder => builder.Add(invalidName)),
            "Property names must be validated when the state definition is built.");
    }

    private static void CheckGlobalStateIds()
    {
        BlockStateIdMap ids = BuiltInRegistries.BlockStates;
        Require(ids.GetId(null!) == -1, "The block-state ID map must return -1 for a null reverse lookup.");
        for (int id = 0; id < ids.Count; id++)
        {
            BlockState state = ids.ById(id)!;
            Require(ReferenceEquals(ids.ById(id), state), "State ID reverse lookup must round-trip.");
            Require(ids.GetId(state) == id, "State-to-ID lookup must round-trip.");
        }
        Require(ids.GetId(Blocks.AIR.DefaultState) == 0, "The first block's singleton state must receive ID zero.");
        Require(ids.ById(-1) is null, "Nullable ID lookup must return null for invalid global state IDs.");
        Require(!ids.TryGetById(-1, out _), "TryGetById must report invalid global state IDs as missing.");
        RequireThrows<ArgumentOutOfRangeException>(() => ids.GetById(-1), "Strict state ID lookup must reject invalid IDs.");
        RequireThrows<ArgumentException>(() => ((IIdMap<BlockState>)ids).ByIdOrThrow(-1), "IdMap.byIdOrThrow must reject invalid global state IDs.");
    }

    private static void CheckPropertyImplementations()
    {
        BooleanProperty lit = BooleanProperty.Create("lit");
        Require(lit.PossibleValues.SequenceEqual(new[] { true, false }), "Boolean values must be true then false, matching Minecraft.");
        Require(lit.TryParse("true", out bool parsedTrue) && parsedTrue, "Boolean parsing must accept true.");
        Require(!lit.TryParse("TRUE", out _), "Boolean parsing must be case-sensitive.");

        IntegerProperty age = IntegerProperty.Create("age", 2, 4);
        Require(age.PossibleValues.SequenceEqual(new[] { 2, 3, 4 }), "Integer values must be inclusive and ascending.");
        Require(age.TryParse("3", out int parsedAge) && parsedAge == 3, "Integer parsing must accept in-range values.");
        Require(!age.TryParse("5", out _), "Integer parsing must reject out-of-range values.");
        Require(!age.TryParse(" 3", out _), "Integer parsing must reject surrounding whitespace.");
        Require(age.GetName(5) == "5", "Integer serialization must return the numeric name without range validation.");

        EnumProperty<TestAxis> axis = EnumProperty<TestAxis>.Create("axis", new[] { TestAxis.Z, TestAxis.X }, value => value.ToString().ToLowerInvariant());
        Require(axis.PossibleValues.SequenceEqual(new[] { TestAxis.Z, TestAxis.X }), "Enum properties must preserve the supplied value order.");
        Require(axis.TryParse("z", out TestAxis parsedAxis) && parsedAxis == TestAxis.Z, "Enum parsing must use the configured name.");

        EnumProperty<TestDeclaredOrder> declared = EnumProperty<TestDeclaredOrder>.Create("declared");
        Require(
            declared.PossibleValues.SequenceEqual(new[] { TestDeclaredOrder.Z, TestDeclaredOrder.X, TestDeclaredOrder.Y }),
            "Default enum properties must preserve enum declaration order rather than numeric sorting.");
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

        var thirtyTwoBitStorage = new PackedBitStorage(32, 3);
        Require(thirtyTwoBitStorage.LongCount == 2, "Thirty-two-bit storage must pack two entries per 64-bit word.");
        thirtyTwoBitStorage.Set(0, int.MaxValue);
        thirtyTwoBitStorage.Set(1, 1);
        thirtyTwoBitStorage.Set(2, 7);
        Require(
            thirtyTwoBitStorage.Get(0) == int.MaxValue &&
            thirtyTwoBitStorage.Get(1) == 1 &&
            thirtyTwoBitStorage.Get(2) == 7,
            "Thirty-two-bit storage must preserve values across word and partial-word boundaries.");

        var partialWordStorage = new PackedBitStorage(5, 17);
        partialWordStorage.Fill(19);
        Require(
            Enumerable.Range(0, partialWordStorage.Size).All(index => partialWordStorage.Get(index) == 19),
            "Fill must repeat the value through every complete and partial storage word.");

        partialWordStorage.Set(8, 7);
        var traversedValues = new List<int>();
        partialWordStorage.GetAll(traversedValues.Add);
        Require(
            traversedValues.Count == 17 &&
            traversedValues[8] == 7 &&
            traversedValues.Where((_, index) => index != 8).All(value => value == 19),
            "GetAll must visit entries in index order and stop at the logical size, not padded word capacity.");
    }

    private static void CheckPaletteExceptions()
    {
        MissingPaletteEntryException exception = new(37);
        Require(exception.Message == "Missing Palette entry for index 37.", "Missing palette entries must use the vanilla exception type and message.");
        Require(exception.Index == 37, "Missing palette entry exceptions must preserve the missing index.");
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
        finally { palette.Release(); }
    }

    private static void CheckPalettedContainerGrowthAndGlobalMode()
    {
        IntegerProperty variant = IntegerProperty.Create("variant", 0, 256);
        var manyStateBlock = new Block(
            BlockProperties.Default,
            builder => builder.Add(variant));

        var ids = new BlockStateIdMap();
        foreach (BlockState state in manyStateBlock.StateDefinition.PossibleStates) ids.Register(state);
        ids.Freeze();

        var strategy = PalettedContainerStrategy<BlockState>.CreateForBlockStates(ids);
        var container = new PalettedContainer<BlockState>(strategy, manyStateBlock.DefaultState);
        IReadOnlyList<BlockState> states = manyStateBlock.StateDefinition.PossibleStates;

        for (int value = 1; value <= 16; value++) container.Set(value - 1, states[value]);
        Require(container.Mode == PaletteMode.Local, "A palette of 17 states must remain indirect.");
        Require(container.BitsPerEntry == 5, "A seventeenth value must grow a four-bit palette to five bits.");
        Require(container.LocalPaletteImplementation?.StartsWith("HashMapPalette", StringComparison.Ordinal) == true, "A widened palette must use hash-map lookup.");

        for (int value = 17; value <= 256; value++) container.Set(value - 1, states[value]);
        Require(container.Mode == PaletteMode.Global, "The 257th distinct state must promote the palette to global IDs.");
        Require(container.BitsPerEntry == 9, "A 257-entry global map uses nine bits in memory.");
        Require(container.BitsInStorage == 9, "The requested global storage width is retained separately.");
        Require(container.AlwaysRepack, "Global configurations must always repack.");
        Require(container.PaletteSize == 257, "The global palette must expose the full state map.");

        CheckGlobalPaletteMemoryAndStorageWidths();
        Require(container.MaybeHas(state => ReferenceEquals(state, manyStateBlock.DefaultState)), "Global maybeHas must be conservative.");

        for (int value = 1; value <= 256; value++)
            Require(ReferenceEquals(container.Get(value - 1), states[value]), "Palette promotion must preserve every stored value.");
        Require(ReferenceEquals(container.Get(256), manyStateBlock.DefaultState), "Palette promotion must preserve untouched defaults.");
        container.Set(300, Blocks.AIR.DefaultState);
        Require(
            ReferenceEquals(container.Get(300), states[0]),
            "The vanilla global palette must map values without a global ID to ID zero.");
    }

    private static void CheckGlobalPaletteMemoryAndStorageWidths()
    {
        IntegerProperty variant = IntegerProperty.Create("variant", 0, 1023);
        var block = new Block(BlockProperties.Default, builder => builder.Add(variant));
        var ids = new BlockStateIdMap();
        foreach (BlockState state in block.StateDefinition.PossibleStates)
            ids.Register(state);
        ids.Freeze();

        var strategy = PalettedContainerStrategy<BlockState>.CreateForBlockStates(ids);
        var container = new PalettedContainer<BlockState>(strategy, block.DefaultState);
        IReadOnlyList<BlockState> states = block.StateDefinition.PossibleStates;
        for (int value = 1; value <= 256; value++)
            container.Set(value - 1, states[value]);

        Require(container.Mode == PaletteMode.Global, "The 257th distinct value must select the global palette.");
        Require(container.BitsPerEntry == 10, "The full 1024-state ID map needs ten resident memory bits.");
        Require(container.BitsInStorage == 9, "The ninth-bit resize request is retained for packed storage.");
        Require(container.AlwaysRepack, "Global palette configurations must always repack.");
        Require(container.Get(255) == states[256], "Global storage must preserve the final inserted state.");
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

        // LevelChunkSection exposes its state container and can rebuild its cached counters.
        section.GetStates().Set(0, placedState);
        Require(section.NonAirBlockCount == 0, "Direct container writes do not implicitly update section counters.");
        section.RecalculateBlockCounts();
        Require(section.NonAirBlockCount == 1, "RecalculateBlockCounts must rebuild the non-air count from palette contents.");

        ChunkSection sectionCopy = section.Copy();
        Require(sectionCopy.NonAirBlockCount == 1, "A copied section must preserve its cached counters.");
        Require(ReferenceEquals(sectionCopy.GetBlockState(0), placedState), "A copied section must preserve block states.");
        sectionCopy.SetBlockState(0, 0, 0, Blocks.AIR.DefaultState);
        Require(sectionCopy.NonAirBlockCount == 0, "Mutating a copied section must update its own counters.");
        Require(section.NonAirBlockCount == 1, "Mutating a copied section must not affect the original.");

        var emptySection = new ChunkSection(BuiltInRegistries.BlockStates, Blocks.AIR.DefaultState);
        Require(!emptySection.MaybeHas(state => !state.Block.Properties.IsAir), "An untouched air-only palette must rule out non-air blocks.");
        Require(ChunkSection.GetIndex(16, 0, 0) == 16, "Section indices must use vanilla bit shifts without per-axis bounds checks.");
        Require(
            ReferenceEquals(section.GetBlockState(16, 0, 0), section.GetBlockState(0, 0, 1)),
            "Coordinates are flattened in vanilla X/Z/Y order before the final storage index is validated.");
        RequireThrows<ArgumentOutOfRangeException>(() => section.GetBlockState(-1, 0, 0), "A negative flattened index must be rejected by packed storage.");
    }

    private static void CheckFrozenRegistry()
    {
        RequireThrows<InvalidOperationException>(
            () => BuiltInRegistries.Block.Register(ResourceLocation.Parse("asteria:late_block"), new Block(BlockProperties.Default)),
            "A frozen block registry must reject new entries.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(Action action, string message) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException(message);
    }
}
