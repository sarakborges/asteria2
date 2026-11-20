using Asteria.Content.Blocks;
using Asteria.Content.Blocks.StateProperties;
using Asteria.Content.Chunks;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Content.Storage;

internal static class Program
{
    private enum TestAxis { X = 0, Y = 1, Z = 2 }

    private sealed class SelectiveHolderOwner<T> : HolderOwner<T> where T : class
    {
        private readonly HolderOwner<T> _acceptedOwner;

        public SelectiveHolderOwner(HolderOwner<T> acceptedOwner) => _acceptedOwner = acceptedOwner;

        public bool CanSerializeIn(HolderOwner<T> owner) => ReferenceEquals(owner, _acceptedOwner);
    }

    private sealed class TestBlockDefinition : Block
    {
        private StateDefinitionBuilder? _capturedBuilder;

        public TestBlockDefinition(
            BlockProperties properties,
            Action<StateDefinitionBuilder>? configureState = null)
            : base(
                properties,
                createIntrusiveRegistryHolder: false,
                configureState: configureState)
        {
        }

        protected override void CreateBlockStateDefinition(StateDefinitionBuilder builder) =>
            _capturedBuilder = builder;

        public void AddPropertyAfterConstruction<T>(StateProperty<T> property)
            where T : notnull, IComparable =>
            (_capturedBuilder ?? throw new InvalidOperationException("No state builder was captured."))
                .Add(property);

        public void SetDefaultState(BlockState state) => RegisterDefaultState(state);
    }

    private enum TestDeclaredOrder { Z = 2, X = 0, Y = 1 }

    private sealed class ThrowingPaletteResize<T> : IPaletteResize<T> where T : class
    {
        public int OnResize(int bits, T lastAddedValue) =>
            throw new InvalidOperationException($"Unexpected resize to {bits} bits.");
    }

    private sealed class IdentityTestValue
    {
        public int Value { get; }

        public IdentityTestValue(int value) => Value = value;

        public override bool Equals(object? obj) =>
            obj is IdentityTestValue other && Value == other.Value;

        public override int GetHashCode() => Value;
    }

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
        CheckHolderReferenceValueBinding();
        CheckIntrusiveRegistrationFailureDiagnostics();
        CheckRegistryLifecycleAndRegistrationInfo();
        CheckRegistryValueLookups();
        CheckBlockStateDefinition();
        CheckBlockStateTransitions();
        CheckGlobalStateIds();
        CheckPropertyImplementations();
        CheckPackedBitStorage();
        CheckPackedBitStorageWidths();
        CheckZeroBitStorage();
        CheckThreadingDetector();
        CheckThreadingDetectorInterruptHandling();
        CheckIdentityIdMap();
        CheckIdentityIdMapSparseMappings();
        CheckPaletteExceptions();
        CheckNoResizePaletteResizeFailure();
        CheckLinearPaletteOverflow();
        CheckPalettedContainerInvalidIndexOrder();
        CheckPalettedContainer();
        CheckPalettedContainerStrategyConfiguration();
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
        Require(
            ResourceLocation.Parse("").Namespace == "minecraft" &&
            ResourceLocation.Parse("").Path == "",
            "Identifier.parse must preserve the vanilla-valid empty path.");
        Require(
            ResourceLocation.Parse("asteria:").Path == "" &&
            ResourceLocation.Parse(":").Namespace == "minecraft",
            "Identifier.parse must accept empty paths with explicit and default namespaces.");
        Require(
            ResourceLocation.TryParse("", out ResourceLocation emptyIdentifier) &&
            emptyIdentifier == ResourceLocation.Parse(""),
            "Identifier.tryParse must accept the empty identifier because an empty path is valid in vanilla.");
        Require(
            ResourceLocation.FromNamespaceAndPath("", "").IsValid,
            "Identifier namespace and path validation must permit empty strings when they contain no invalid characters.");
        Require(
            ResourceLocation.TryBuild("", "") is ResourceLocation emptyBuilt && emptyBuilt.IsValid,
            "Identifier.tryBuild must return an identifier for valid empty namespace and path values.");
        Require(
            ResourceLocation.TryBuild("..", "stone") is null,
            "Identifier.tryBuild must return no value for invalid namespaces.");
        Require(
            ResourceLocation.IsValidNamespace("") &&
            ResourceLocation.IsValidPath("") &&
            !ResourceLocation.IsValidPath(":"),
            "Identifier public validators must retain vanilla character rules.");
        Require(
            ResourceLocation.IsAllowedInIdentifier(':') &&
            ResourceLocation.IsAllowedInIdentifier('/') &&
            !ResourceLocation.IsAllowedInIdentifier(' '),
            "Identifier.isAllowedInIdentifier must match command token characters.");
        Require(
            ResourceLocation.NamespaceSeparator == ':' &&
            ResourceLocation.DefaultNamespace == "minecraft" &&
            ResourceLocation.RealmsNamespace == "realms" &&
            ResourceLocation.AllowedNamespaceCharacters == "[a-z0-9_.-]",
            "Identifier public constants must match vanilla.");
        ResourceLocation languageId = ResourceLocation.Parse("stone/slab");
        Require(languageId.WithPrefix("double_").Path == "double_stone/slab", "Identifier.withPrefix must modify only the path.");
        Require(languageId.WithSuffix("_top").Path == "stone/slab_top", "Identifier.withSuffix must modify only the path.");
        Require(languageId.WithPath(path => $"blocks/{path}").Path == "blocks/stone/slab", "Identifier.withPath must apply a path modifier.");
        Require(languageId.ToDebugFileName() == "minecraft_stone_slab", "Identifier.toDebugFileName must replace slash and namespace separators.");
        Require(languageId.ToLanguageKey() == "minecraft.stone/slab", "Identifier.toLanguageKey must include namespace and path.");
        Require(languageId.ToShortLanguageKey() == "stone/slab", "Identifier.toShortLanguageKey must omit the default namespace.");
        Require(languageId.ToShortString() == "stone/slab", "Identifier.toShortString must omit the default namespace.");
        Require(languageId.ToLanguageKey("block", "name") == "block.minecraft.stone/slab.name", "Identifier language-key prefix and suffix overload must match vanilla.");
        Require(
            ResourceLocation.Parse("zeta:alpha").CompareTo(ResourceLocation.Parse("alpha:zeta")) < 0,
            "Identifier ordering must compare path before namespace.");
    }

    private static void CheckDefaultedBlockRegistry()
    {
        ResourceLocation airId = ResourceLocation.Parse("asteria:air");
        ResourceLocation testId = ResourceLocation.Parse("asteria:registry_test");
        ResourceLocation missingId = ResourceLocation.Parse("asteria:not_registered");

        Require(ReferenceEquals(BuiltInRegistries.Block.GetValue(airId), Blocks.AIR), "The air block must be registered.");
        Require(
            ReferenceEquals(BuiltInRegistries.Block.GetValue(missingId), Blocks.AIR),
            "Defaulted GetValue(identifier) lookups must resolve unknown identifiers to air.");
        Require(
            ReferenceEquals(BuiltInRegistries.Block.GetValue((ResourceLocation?)null), Blocks.AIR),
            "Defaulted GetValue(null identifier) must resolve to the default value.");
        Require(
            BuiltInRegistries.Block.GetOptional((ResourceLocation?)null) is null,
            "Defaulted getOptional(null identifier) must remain empty instead of using the default value.");

        RegistryKey<Block>? nullKey = null;
        Require(
            BuiltInRegistries.Block.GetValue(nullKey) is null &&
            BuiltInRegistries.Block.GetOptional(nullKey) is null,
            "A null resource key must remain an exact empty lookup even in a defaulted registry.");

        RegistryKey<Block> missingKey = new(BuiltInRegistries.Block.Location, missingId);
        RegistryKey<Block> testKey = new(BuiltInRegistries.Block.Location, testId);
        Require(
            BuiltInRegistries.Block.GetValue(missingKey) is null &&
            BuiltInRegistries.Block.GetOptional(missingKey) is null &&
            BuiltInRegistries.Block.GetOptional(missingId) is null &&
            BuiltInRegistries.Block.GetHolder(missingKey) is null,
            "Defaulted registries must keep resource-key, optional, and holder lookups exact rather than falling back.");
        Require(
            ReferenceEquals(BuiltInRegistries.Block.GetValue(testKey), Blocks.TEST_BLOCK) &&
            ReferenceEquals(BuiltInRegistries.Block.GetOptional(testKey), Blocks.TEST_BLOCK),
            "Exact resource-key lookups must still return registered values.");
        Require(ReferenceEquals(BuiltInRegistries.Block.GetAny(), Blocks.AIR.BuiltInRegistryHolder), "DefaultedMappedRegistry.getAny must return its default holder.");
        Holder<Block>.Reference? randomDefaultedHolder = BuiltInRegistries.Block.GetRandom(new Random(0));
        Require(
            randomDefaultedHolder is not null &&
            (ReferenceEquals(randomDefaultedHolder, Blocks.AIR.BuiltInRegistryHolder) ||
             ReferenceEquals(randomDefaultedHolder, Blocks.TEST_BLOCK.BuiltInRegistryHolder)),
            "DefaultedMappedRegistry.getRandom must use the registry values and retain the default holder fallback.");
        Require(BuiltInRegistries.Block.GetHolder(missingId) is null, "Exact holder lookups must not hide missing keys.");
        Require(BuiltInRegistries.Block.GetId(new TestBlockDefinition(BlockProperties.Of())) == 0, "Unknown objects must resolve to the default block ID.");
        Require(BuiltInRegistries.Block.GetId(null!) == 0, "Null reverse lookups in a defaulted registry must resolve to the default ID.");
        Require(BuiltInRegistries.Block.GetKey(null!) == airId, "Null key lookups in a defaulted registry must resolve to the default key.");
        Require(
            BuiltInRegistries.Block.GetHolderById(999) is null,
            "DefaultedMappedRegistry.get(id) must remain an exact holder lookup for unknown numeric IDs.");
        Require(ReferenceEquals(BuiltInRegistries.Block.ById(999), Blocks.AIR), "DefaultedMappedRegistry.byId must resolve unknown IDs to its default value.");

        InvalidOperationException missingValueException = Capture<InvalidOperationException>(
            () => BuiltInRegistries.Block.GetValueOrThrow(missingKey),
            "Defaulted getValueOrThrow(resource key) must reject an absent key instead of falling back.");
        Require(
            missingValueException.Message == $"Missing key in {BuiltInRegistries.Block.Key}: {missingKey}",
            "Defaulted getValueOrThrow(resource key) must preserve the vanilla exact-key diagnostic.");

        Holder<Block>.Reference registered = BuiltInRegistries.Block.GetHolderOrThrow(testKey);
        Require(ReferenceEquals(registered, Blocks.TEST_BLOCK.BuiltInRegistryHolder), "Registration must bind the pre-created intrusive holder.");
        Require(registered.IsBound, "The built-in holder must be fully bound after freeze.");
        Require(ReferenceEquals(registered.Value, Blocks.TEST_BLOCK), "The holder must resolve to its registered block.");
        HolderUnwrap<Block> referenceUnwrap = registered.Unwrap();
        Require(referenceUnwrap.IsKey && referenceUnwrap.Key == new RegistryKey<Block>(BuiltInRegistries.Block.Location, testId) && referenceUnwrap.Value is null, "Holder.Reference.unwrap must return its registry key.");
        Require(registered.UnwrapKey() == new RegistryKey<Block>(BuiltInRegistries.Block.Location, testId), "Holder.Reference.unwrapKey must return its registry key.");
        Require(registered.GetRegisteredNameIfPresent() == testId.ToString(), "Holder.Reference registered name must use its identifier.");
        Require(registered.GetRegisteredName() == testId.ToString(), "Holder.Reference registered name must be present after binding.");
        Require(registered.CanSerializeIn(BuiltInRegistries.Block), "A reference holder must serialize in its owning registry.");
        Require(ReferenceEquals(BuiltInRegistries.Block.WrapAsHolder(Blocks.TEST_BLOCK), registered), "wrapAsHolder must reuse registered references.");

        Block directValue = new TestBlockDefinition(BlockProperties.Of());
        Holder<Block> direct = BuiltInRegistries.Block.WrapAsHolder(directValue);
        Require(direct.Kind == HolderKind.Direct, "Unregistered values must be wrapped as direct holders.");
        HolderUnwrap<Block> directUnwrap = direct.Unwrap();
        Require(!directUnwrap.IsKey && ReferenceEquals(directUnwrap.Value, directValue) && directUnwrap.Key is null, "Holder.Direct.unwrap must return the direct value.");
        Require(direct.UnwrapKey() is null, "Holder.Direct.unwrapKey must be empty.");
        Require(direct.GetRegisteredNameIfPresent() is null, "Holder.Direct must not have a registered name.");
        Require(direct.GetRegisteredName() == "[unregistered]", "Holder.Direct registered name fallback must match vanilla.");
        Require(direct.CanSerializeIn(BuiltInRegistries.Block), "Direct holders are serializable in any registry context.");
        Require(direct.Is(Holder<Block>.Direct(directValue)), "Direct holders compare by their wrapped value.");
        Require(direct.Equals(Holder<Block>.Direct(directValue)), "Direct holder record equality compares wrapped values.");
        Require(!registered.Is(direct), "A reference holder must not match a direct holder by value.");
        Require(direct.Is(registered) == false || ReferenceEquals(registered.Value, directValue), "Direct holder matching follows wrapped-value equality.");
    }

    private static void CheckBlockProperties()
    {
        BlockProperties defaults = BlockProperties.Of();
        Require(defaults.DestroyTimeValue == 0.0f, "Vanilla block properties default destroy time to zero.");
        Require(defaults.ExplosionResistanceValue == 0.0f, "Vanilla block properties default explosion resistance to zero.");
        Require(defaults.FrictionValue == 0.6f, "Vanilla block properties default friction to 0.6.");
        Require(!defaults.RequiresCorrectToolForDropsValue && !defaults.IsAir, "Vanilla block properties default tool and air flags to false.");

        var custom = BlockProperties.Of()
            .DestroyTime(-2.0f)
            .ExplosionResistance(-3.0f)
            .Friction(-0.25f);
        Require(custom.DestroyTimeValue == -2.0f, "Vanilla destroyTime stores the supplied value without clamping.");
        Require(custom.ExplosionResistanceValue == 0.0f, "Vanilla explosionResistance clamps negative values to zero.");
        Require(custom.FrictionValue == -0.25f, "Vanilla friction stores the supplied value without clamping.");

        BlockProperties strength = BlockProperties.Of().Strength(1.5f);
        Require(
            strength.DestroyTimeValue == 1.5f && strength.ExplosionResistanceValue == 1.5f,
            "Vanilla strength(float) must assign destroy time and explosion resistance together.");

        BlockProperties instant = BlockProperties.Of().Instabreak();
        Require(
            instant.DestroyTimeValue == 0.0f && instant.ExplosionResistanceValue == 0.0f,
            "Vanilla instabreak must use strength(0).");

        BlockProperties semantic = BlockProperties.Of()
            .RequiresCorrectToolForDrops()
            .Air();
        Require(
            semantic.RequiresCorrectToolForDropsValue && semantic.IsAir,
            "Vanilla property flags must be set through the fluent builder.");

        BlockProperties mutableProperties = BlockProperties.Of()
            .Strength(2.5f, 8.0f)
            .RequiresCorrectToolForDrops();
        var snapshotBlock = new TestBlockDefinition(mutableProperties);
        BlockState snapshotState = snapshotBlock.DefaultState;
        Require(
            !snapshotState.IsAir &&
            snapshotState.DestroySpeed == 2.5f &&
            snapshotState.RequiresCorrectToolForDrops,
            "Block states must cache air, destroy speed, and tool requirements at construction.");
        Require(
            snapshotBlock.ExplosionResistance == 8.0f && snapshotBlock.Friction == 0.6f,
            "BlockBehaviour must cache explosion resistance and friction during construction.");

        mutableProperties.Air().DestroyTime(9.0f).ExplosionResistance(20.0f).Friction(0.2f);
        Require(
            !snapshotState.IsAir && snapshotState.DestroySpeed == 2.5f,
            "Mutating BlockBehaviour.Properties after state creation must not alter cached BlockStateBase values.");
        Require(
            snapshotBlock.ExplosionResistance == 8.0f && snapshotBlock.Friction == 0.6f,
            "Mutating BlockBehaviour.Properties after block construction must not alter cached behavior fields.");
        Require(
            mutableProperties.IsAir && mutableProperties.DestroyTimeValue == 9.0f &&
            mutableProperties.ExplosionResistanceValue == 20.0f && mutableProperties.FrictionValue == 0.2f,
            "The BlockBehaviour.Properties builder itself remains mutable after block construction.");
    }

    private static void CheckRegistryHoldersAndTags()
    {
        ResourceLocation registryId = ResourceLocation.Parse("asteria:test_registry");
        ResourceLocation entryId = ResourceLocation.Parse("asteria:test_entry");
        ResourceLocation placeholderId = ResourceLocation.Parse("asteria:missing_placeholder");
        ResourceLocation tagId = ResourceLocation.Parse("asteria:test_tag");

        var registry = new Registry<Block>(registryId);
        Require(
            registry.Key == new RegistryKey<Registry<Block>>(ResourceLocation.Parse("root"), registryId),
            "MappedRegistry.key must identify the registry through the root registry.");
        Require(
            registry.ToString() == $"Registry[{registry.Key} (Stable)]",
            "MappedRegistry.toString must include its key and lifecycle.");
        Require(registry.IsEmpty(), "A registry with no keys must be empty.");
        Require(registry.GetAny() is null, "MappedRegistry.getAny must be empty before its first registered value.");
        Require(registry.GetRandom(new Random(0)) is null, "MappedRegistry.getRandom must be empty when no values are registered.");
        Require(registry.KeySet().Count == 0 && registry.RegistryKeySet().Count == 0 && !registry.EntrySet().Any(), "Empty registry key and entry sets must be empty.");
        Require(registry.GetId(null!) == -1, "A non-defaulted registry must return -1 for a null reverse lookup.");
        Require(registry.GetKey(null!) is null, "A non-defaulted registry must return null for a null key lookup.");
        Require(registry.ById(-1) is null, "MappedRegistry.byId must return null for an invalid numeric ID.");
        var entryKey = new RegistryKey<Block>(registryId, entryId);
        var placeholderKey = new RegistryKey<Block>(registryId, placeholderId);
        var tagKey = new TagKey<Block>(registryId, tagId);
        var rootRegistryKey = new RegistryKey<Registry<Block>>(ResourceLocation.Parse("root"), registryId);
        Require(entryKey.IsFor(rootRegistryKey), "ResourceKey.isFor must compare its registry identifier.");
        Require(entryKey.GetRegistryKey() == rootRegistryKey, "ResourceKey.registryKey must return the root registry key.");
        RegistryKey<string>? castEntryKey = entryKey.Cast(
            new RegistryKey<Registry<string>>(ResourceLocation.Parse("root"), registryId));
        Require(
            castEntryKey is RegistryKey<string> castKey && castKey.Location == entryId,
            "ResourceKey.cast must preserve the identifier when the registry matches.");
        Require(
            entryKey.Cast(new RegistryKey<Registry<string>>(
                ResourceLocation.Parse("root"),
                ResourceLocation.Parse("asteria:other_registry"))) is null,
            "ResourceKey.cast must return an empty result for a different registry.");
        Require(
            entryKey.Dependent(rootRegistryKey, "_metadata").Location == entryId.WithSuffix("_metadata"),
            "ResourceKey.dependent(suffix) must append to its identifier.");
        Require(
            entryKey.Dependent(
                new RegistryKey<Registry<Block>>(ResourceLocation.Parse("root"), ResourceLocation.Parse("asteria:other_registry")),
                path => $"metadata/{path}").Location.Path == "metadata/test_entry",
            "ResourceKey.dependent(decoration) must transform the identifier path.");
        Require(tagKey.IsFor(rootRegistryKey), "TagKey.isFor must compare its registry identifier.");
        TagKey<string>? castTagKey = tagKey.Cast(
            new RegistryKey<Registry<string>>(ResourceLocation.Parse("root"), registryId));
        Require(
            castTagKey is TagKey<string> castTag && castTag.Location == tagId,
            "TagKey.cast must preserve the tag location when the registry matches.");
        Require(
            tagKey.Cast(new RegistryKey<Registry<string>>(
                ResourceLocation.Parse("root"),
                ResourceLocation.Parse("asteria:other_registry"))) is null,
            "TagKey.cast must return an empty result for a different registry.");
        HolderGetter<Block> lookup = registry.CreateRegistrationLookup();

        Holder<Block>.Reference placeholder = lookup.GetOrThrow(placeholderKey);
        Require(
            ReferenceEquals(lookup.Get(placeholderKey), placeholder),
            "HolderGetter.get must return the same standalone placeholder as getOrThrow.");
        Require(lookup.CanSerializeIn(registry), "A registration lookup must serialize in its owning registry context.");
        Require(!lookup.CanSerializeIn(new Registry<Block>(ResourceLocation.Parse("asteria:foreign_owner"))), "A registration lookup must reject serialization in a foreign owner context.");
        Require(!registry.IsEmpty(), "A standalone key placeholder makes the registry non-empty.");
        Require(registry.GetAny() is null, "Standalone key placeholders do not create registered numeric IDs.");
        Require(registry.RegistryKeySet().Contains(placeholderKey), "registryKeySet must include standalone key placeholders.");
        Require(registry.KeySet().Count == 0, "keySet must contain only registered identifier lookups.");
        Require(registry.Count == 1, "Registry size must include standalone key placeholders, matching MappedRegistry.size().");
        Require(!placeholder.IsBound, "A registration lookup creates an unbound standalone holder.");
        InvalidOperationException unboundValueException = RequireThrows<InvalidOperationException>(
            () => _ = placeholder.Value,
            "An unbound standalone holder must reject value access.");
        Require(
            unboundValueException.Message == $"Trying to access unbound value '{placeholderKey}' from registry {registry}",
            "Holder.Reference.value must include its key and owner in the vanilla error message.");
        Require(
            placeholder.ToString() == $"Reference{{{placeholderKey}=null}}",
            "Holder.Reference.toString must print null key/value fields like vanilla.");
        HolderUnwrap<Block> placeholderUnwrap = placeholder.Unwrap();
        Require(placeholderUnwrap.IsKey && placeholderUnwrap.Key == placeholderKey && placeholderUnwrap.Value is null, "An unbound standalone holder must unwrap to its pre-existing key.");
        Require(placeholder.GetRegisteredNameIfPresent() == placeholderId.ToString(), "A standalone holder has a registered name before its value is bound.");
        RequireThrows<InvalidOperationException>(() => _ = placeholder.Value, "An unbound holder value must not be readable.");

        Block value = new TestBlockDefinition(BlockProperties.Of());
        Holder<Block>.Reference registered = lookup.GetOrThrow(entryKey);
        Require(registry.Count == 2, "Each distinct standalone key must contribute to registry size before registration.");
        Holder<Block>.Reference entry = registry.Register(entryId, value);
        InvalidOperationException duplicateKeyException = Capture<InvalidOperationException>(
            () => registry.Register(entryKey, new TestBlockDefinition(BlockProperties.Of()), RegistrationInfo.BuiltIn),
            "MappedRegistry.register must reject duplicate identifiers.");
        Require(
            duplicateKeyException.Message == $"Adding duplicate key '{entryKey}' to registry",
            "MappedRegistry.register must use the vanilla duplicate-key error message.");
        InvalidOperationException duplicateValueException = Capture<InvalidOperationException>(
            () => registry.Register(new RegistryKey<Block>(registryId, ResourceLocation.Parse("asteria:duplicate_value")), value, RegistrationInfo.BuiltIn),
            "MappedRegistry.register must reject duplicate values.");
        Require(
            duplicateValueException.Message == $"Adding duplicate value '{value}' to registry",
            "MappedRegistry.register must use the vanilla duplicate-value error message.");
        Require(ReferenceEquals(entry, registered), "Registration must reuse the standalone holder.");
        Require(registry.Count == 2, "Registering a reserved key must not change registry size.");
        Require(!registered.IsBound, "Standalone holder values are bound during registry freeze.");

        Block placeholderValue = new TestBlockDefinition(BlockProperties.Of());
        Holder<Block>.Reference placeholderEntry = registry.Register(placeholderId, placeholderValue);
        Require(ReferenceEquals(placeholderEntry, placeholder), "Registering a placeholder key must bind the existing holder.");

        HolderSet<Block>.Named tag = lookup.GetOrThrow(tagKey);
        Require(
            ReferenceEquals(lookup.Get(tagKey), tag),
            "HolderGetter.get(tag) must return the same named placeholder as getOrThrow(tag).");
        Require(!tag.IsBound, "A registration-time tag starts unbound.");
        Require(
            tag.ToString() == $"NamedSet({tagKey})[null]",
            "Unbound HolderSet.Named.toString must print its null contents.");
        InvalidOperationException unboundTagException = RequireThrows<InvalidOperationException>(
            () => _ = tag.Contents,
            "An unbound named holder set must reject content access.");
        Require(
            unboundTagException.Message == $"Trying to access unbound tag '{tagKey}' from registry {registry}",
            "HolderSet.Named.contents must include its owner in the vanilla error message.");
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
        Require(
            ReferenceEquals(lookup.GetRandomElementOf(tagKey, new Random(0)), registered),
            "HolderGetter.getRandomElementOf must choose from the resolved named holder set.");
        Require(tag.Count == 2, "Named holder sets must preserve the supplied contents, including duplicates.");
        RequireThrows<InvalidOperationException>(
            () => registered.Is(tagKey),
            "Binding a named set must not bind holder tags before registry freeze.");
        RequireThrows<InvalidOperationException>(
            () => tag.Contains(registered),
            "Named holder membership must follow holder tag bindings and remain unavailable before freeze.");
        Require(!tag.Contains(Holder<Block>.Direct(value)), "A named holder set must not match direct holders.");
        InvalidOperationException unboundTagLookupException = Capture<InvalidOperationException>(
            () => registry.GetTag(tagKey),
            "Tag access must fail before registry freeze.");
        Require(
            unboundTagLookupException.Message == $"Tags not bound, trying to access {tagKey}",
            "MappedRegistry.get(tag) must preserve the vanilla unbound tag lookup message.");

        registry.Freeze();
        var selectiveSerializationContext = new SelectiveHolderOwner<Block>(registry);
        Require(
            registered.CanSerializeIn(selectiveSerializationContext),
            "Holder.Reference.canSerializeIn must ask the target context to serialize its original owner.");
        Require(
            tag.CanSerializeIn(selectiveSerializationContext),
            "HolderSet.Named.canSerializeIn must ask the target context to serialize its original owner.");
        Require(ReferenceEquals(registry.GetAny(), registered), "MappedRegistry.getAny must return the first registered holder.");

        HolderLookup<Block>.RegistryLookup lookupView = registry;
        Require(
            lookupView.Key == new RegistryKey<Registry<Block>>(ResourceLocation.Parse("root"), registryId),
            "HolderLookup.RegistryLookup.key must expose the root registry key.");
        Require(
            ReferenceEquals(lookupView.RegistryLifecycle, registry.RegistryLifecycle),
            "HolderLookup.RegistryLookup must expose the registry lifecycle.");
        Require(
            lookupView.ListElementIds().SequenceEqual(new[] { entryKey, placeholderKey }) &&
            lookupView.ListTagIds().SequenceEqual(new[] { tagKey }),
            "HolderLookup must enumerate element and tag keys in lookup order.");
        Require(
            ReferenceEquals(lookupView.Get(entryKey), registered) &&
            ReferenceEquals(lookupView.Get(tagKey), tag),
            "HolderLookup must resolve registered element and tag holders.");
        HolderLookup<Block>.RegistryLookup filteredLookup =
            lookupView.FilterElements(block => ReferenceEquals(block, value));
        Require(
            filteredLookup.ListElements().SequenceEqual(new[] { registered }) &&
            ReferenceEquals(filteredLookup.Get(entryKey), registered) &&
            filteredLookup.Get(placeholderKey) is null,
            "HolderLookup.filterElements must filter direct element lookup and enumeration.");
        Require(
            ReferenceEquals(filteredLookup.Get(tagKey), tag) &&
            filteredLookup.ListTags().SequenceEqual(lookupView.ListTags()),
            "HolderLookup.filterElements must preserve the parent tag view.");

IIdMap<Holder<Block>> holderIdMap = registry.AsHolderIdMap();
        Require(holderIdMap.Count == registry.Count, "Registry.asHolderIdMap must report the registry size.");
        Require(
            ReferenceEquals(holderIdMap.ById(0), registered) &&
            ReferenceEquals(holderIdMap.ById(1), placeholderEntry),
            "Registry.asHolderIdMap.byId must return the registered holders by numeric ID.");
        Require(
            holderIdMap.GetId(registered) == registry.GetId(value) &&
            holderIdMap.GetId(Holder<Block>.Direct(value)) == registry.GetId(value),
            "Registry.asHolderIdMap.getId must resolve the wrapped value, regardless of holder kind.");
        Require(
            holderIdMap.GetId(Holder<Block>.Direct(new TestBlockDefinition(BlockProperties.Of()))) == -1,
            "Registry.asHolderIdMap.getId must return -1 for values not registered in the registry.");
        Require(
            holderIdMap.SequenceEqual(new Holder<Block>[] { registered, placeholderEntry }),
            "Registry.asHolderIdMap must enumerate registered holders in numeric ID order.");
        Holder<Block>.Reference? randomHolder = registry.GetRandom(new Random(0));
        Require(
            randomHolder is not null &&
            (ReferenceEquals(randomHolder, registered) || ReferenceEquals(randomHolder, placeholderEntry)),
            "MappedRegistry.getRandom must return one of the registered holders.");
        Require(registry.KeySet().Count == 2, "keySet must contain registered entry identifiers.");
        Require(registry.RegistryKeySet().Count == 2, "registryKeySet must contain registered and reserved keys.");
        Require(registry.EntrySet().Count() == 2, "entrySet must expose registered keys and their values.");
        Require(
            registry.ListElements() is not List<Holder<Block>.Reference>,
            "listElements must not expose the registry's mutable backing list.");
        Require(registered.IsBound, "Freeze must bind registered standalone values.");
        Require(ReferenceEquals(registered.Value, value), "The frozen holder must resolve to the registered value.");
        Require(registered.Is(tagKey), "Freeze must associate tags with their holders.");
        Require(tag.Contains(registered), "Named holder membership must work after registry freeze binds holder tags.");
        Require(
            tag.ToString() == $"NamedSet({tagKey})[[{registered}, {registered}]]",
            "Bound HolderSet.Named.toString must render its contents list.");
        TagKey<Block> lateTagKey = new(registryId, ResourceLocation.Parse("asteria:late_registration_tag"));
        HolderSet<Block>.Named lateTag = lookup.GetOrThrow(lateTagKey);
        Require(
            !lateTag.IsBound && registry.GetTag(lateTagKey) is null,
            "A registration lookup retained past freeze may create a tag placeholder without publishing it into the frozen tag set.");

        HolderSetUnwrap<Block> namedUnwrap = tag.Unwrap();
        Require(
            namedUnwrap.IsTag &&
            namedUnwrap.Tag == tagKey &&
            namedUnwrap.Values is null,
            "Named HolderSet.unwrap must return its tag key.");
        Require(tag.UnwrapKey() == tagKey, "Named HolderSet.unwrapKey must return its tag key.");
        Require(tag.Stream().Count() == 2, "HolderSet.stream must enumerate its bound entries.");
        Require(ReferenceEquals(registry.GetTag(tagKey), tag), "The frozen registry must expose its registered tag set.");
        Require(registry.GetTagOrEmpty(new TagKey<Block>(registryId, ResourceLocation.Parse("asteria:empty"))).Count == 0, "Missing tags must resolve to an empty holder set.");

        HolderSet<Block> directSet = new(new Holder<Block>[] { registered, registered });
        Require(directSet.Count == 2, "Direct holder sets must preserve order and duplicate entries.");

        HolderSet<Block> directFactory = HolderSet<Block>.Direct(registered, registered);
        Require(
            directFactory.SequenceEqual(new Holder<Block>[] { registered, registered }),
            "HolderSet.direct(varargs) must preserve order and duplicate entries.");
        HolderSet<Block> mappedDirectFactory = HolderSet<Block>.Direct(
            static block => Holder<Block>.Direct(block),
            new[] { value, placeholderValue });
        Require(
            mappedDirectFactory.Count == 2 &&
            ReferenceEquals(mappedDirectFactory[0].Value, value) &&
            ReferenceEquals(mappedDirectFactory[1].Value, placeholderValue),
            "HolderSet.direct(holderGetter, elements) must map source values to direct holders in order.");
        Require(
            directSet.ToString() == $"DirectSet[[{registered}, {registered}]]",
            "HolderSet.Direct.toString must render its contents list.");
        Require(directSet.CanSerializeIn(registry), "Direct holder sets are serializable regardless of the contained holder owner.");
        Require(directSet.Contains(registered), "Direct holder set membership must use holder equality.");
        HolderSetUnwrap<Block> directUnwrap = directSet.Unwrap();
        Require(
            !directUnwrap.IsTag &&
            directUnwrap.Values?.SequenceEqual(new Holder<Block>[] { registered, registered }) == true,
            "Direct HolderSet.unwrap must return its holder list.");
        Require(directSet.UnwrapKey() is null, "Direct HolderSet.unwrapKey must be empty.");
        Require(ReferenceEquals(directSet.Get(0), registered), "HolderSet.get must return an indexed holder.");
        Require(ReferenceEquals(directSet.GetRandomElement(new Random(0)), registered), "Direct HolderSet.getRandomElement must select an existing holder.");
        Require(
            HolderSet<Block>.Empty.GetRandomElement(new Random(0)) is null,
            "HolderSet.getRandomElement must return an empty result when its contents are empty.");

        var parallelRegistry = new Registry<Block>(registryId);
        Holder<Block>.Reference parallelReference =
            parallelRegistry.Register(entryId, new TestBlockDefinition(BlockProperties.Of()));
        parallelRegistry.Freeze();
        Require(!registered.Equals(parallelReference), "Reference holders from separate registry instances must retain object identity.");
        Require(registered.Is(parallelReference), "Reference holder matching must follow the resource key, not holder instance identity.");
        Require(parallelReference.Is(registered), "Reference holder key matching must work in both directions.");
        Require(!registered.Is(Holder<Block>.Direct(value)), "Reference holder matching must reject direct holders.");

        RequireThrows<InvalidOperationException>(
            () => registry.BindTags(Array.Empty<KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>>()),
            "Direct tag binding must be rejected after freeze.");

        PendingTagReload<Block> lateTagReload = registry.PrepareTagReload(new[]
        {
            new KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>(
                lateTagKey,
                new Holder<Block>[] { registered })
        });
        Require(
            lateTagReload.Key == registry.Key &&
            lateTagReload.Size == 1 &&
            ReferenceEquals(lateTagReload.Lookup.Get(lateTagKey), lateTag) &&
            lateTagReload.Lookup.ListTags().SequenceEqual(new[] { lateTag }),
            "PendingTags must expose the registry key, size, and a lookup over the pending tag sets.");
        Require(!lateTag.IsBound, "A newly pending tag must remain unbound until the reload is applied.");
        lateTagReload.Apply();
        Require(
            ReferenceEquals(registry.GetTag(lateTagKey), lateTag) && lateTag.IsBound,
            "Tag reload must reuse a named tag created through the registry's retained registration lookup.");

        TagKey<Block> reloadOnlyTagKey = new(
            registryId,
            ResourceLocation.Parse("asteria:reload_only_tag"));
        KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>[] reloadOnlyDefinition =
        {
            new(
                reloadOnlyTagKey,
                new Holder<Block>[] { registered })
        };
        PendingTagReload<Block> firstPendingReload = registry.PrepareTagReload(reloadOnlyDefinition);
        HolderSet<Block>.Named firstPendingTag = firstPendingReload.Lookup.Get(reloadOnlyTagKey)!;
        Require(
            firstPendingReload.Size == 1 && !firstPendingTag.IsBound,
            "A pending lookup must expose new reload-only tags before their contents are bound.");
        firstPendingReload.Apply();
        HolderSet<Block>.Named firstReloadOnlyTag = registry.GetTag(reloadOnlyTagKey)!;

        PendingTagReload<Block> secondPendingReload = registry.PrepareTagReload(reloadOnlyDefinition);
        HolderSet<Block>.Named secondPendingTag = secondPendingReload.Lookup.Get(reloadOnlyTagKey)!;
        Require(
            !ReferenceEquals(firstReloadOnlyTag, secondPendingTag),
            "Tags introduced only by a reload must be recreated on a later reload unless they were registered before freeze.");
        secondPendingReload.Apply();
        HolderSet<Block>.Named secondReloadOnlyTag = registry.GetTag(reloadOnlyTagKey)!;
        Require(ReferenceEquals(secondPendingTag, secondReloadOnlyTag), "Applying a pending reload must publish its lookup's named tag instances.");

        registry.PrepareTagReload(Array.Empty<KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>>()).Apply();
        Require(!registered.Is(tagKey), "A tag reload must replace holder tag membership.");
        Require(registry.GetTag(tagKey) is null, "Tags omitted from a reload must no longer be present.");

        var crossRegistry = new Registry<Block>(ResourceLocation.Parse("asteria:cross_registry"));
        var otherRegistry = new Registry<Block>(ResourceLocation.Parse("asteria:other_registry"));
        ResourceLocation foreignEntryLocation = ResourceLocation.Parse("asteria:foreign_entry");
        RegistryKey<Block> foreignEntryKey = new(otherRegistry.Location, foreignEntryLocation);
        HolderGetter<Block> crossLookup = crossRegistry.CreateRegistrationLookup();
        Holder<Block>.Reference foreignPlaceholder = crossLookup.GetOrThrow(foreignEntryKey);
        Block foreignValue = new TestBlockDefinition(BlockProperties.Of());
        Require(
            ReferenceEquals(
                crossRegistry.Register(foreignEntryKey, foreignValue, RegistrationInfo.BuiltIn),
                foreignPlaceholder),
            "MappedRegistry.register must retain a supplied ResourceKey without revalidating its registry identifier.");

        TagKey<Block> foreignTagKey = new(otherRegistry.Location, ResourceLocation.Parse("asteria:foreign_tag"));
        HolderSet<Block>.Named foreignTag = crossLookup.GetOrThrow(foreignTagKey);
        crossRegistry.BindTags(new[]
        {
            new KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>(
                foreignTagKey,
                new Holder<Block>[] { foreignPlaceholder })
        });
        crossRegistry.Freeze();
        Require(
            foreignTag.IsBound &&
            foreignPlaceholder.Is(foreignTagKey) &&
            ReferenceEquals(crossRegistry.GetTag(foreignTagKey), foreignTag),
            "MappedRegistry tag registration and binding must preserve TagKeys without adding a registry-identity validation.");

        var invalidRegistry = new Registry<Block>(ResourceLocation.Parse("asteria:invalid_registry"));
        ResourceLocation unboundEntryLocation = ResourceLocation.Parse("asteria:never_registered");
        RegistryKey<Block> unboundEntryKey = new(invalidRegistry.Location, unboundEntryLocation);
        invalidRegistry.CreateRegistrationLookup().GetOrThrow(unboundEntryKey);
        InvalidOperationException unboundValuesException = Capture<InvalidOperationException>(
            invalidRegistry.Freeze,
            "Freeze must reject unbound key placeholders.");
        Require(
            unboundValuesException.Message == $"Unbound values in registry {invalidRegistry.Key}: [{unboundEntryLocation}]",
            "MappedRegistry.freeze must sort and list unbound entry identifiers in its vanilla diagnostic.");

        var invalidTagRegistry = new Registry<Block>(ResourceLocation.Parse("asteria:invalid_tag_registry"));
        TagKey<Block> invalidTagKey = new(
            invalidTagRegistry.Location,
            ResourceLocation.Parse("asteria:invalid"));
        HolderSet<Block>.Named invalidNamedTag =
            invalidTagRegistry.CreateRegistrationLookup().GetOrThrow(invalidTagKey);
        invalidTagRegistry.BindTags(new[]
        {
            new KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>(
                invalidTagKey,
                new Holder<Block>[] { Holder<Block>.Direct(value) })
        });
        Require(invalidNamedTag.IsBound, "MappedRegistry.bindTags binds contents without validating tag holders immediately.");
        InvalidOperationException directHolderTagException = Capture<InvalidOperationException>(
            invalidTagRegistry.Freeze,
            "MappedRegistry validates direct holders while refreshing tag membership during freeze.");
        Require(
            directHolderTagException.Message == $"Found direct holder {Holder<Block>.Direct(value)} value in tag {invalidTagKey}",
            "MappedRegistry must distinguish direct holders from foreign holders in tag diagnostics.");

        var unboundTagRegistry = new Registry<Block>(ResourceLocation.Parse("asteria:unbound_tag_registry"));
        TagKey<Block> unboundTagKey = new(
            unboundTagRegistry.Location,
            ResourceLocation.Parse("asteria:never_bound"));
        unboundTagRegistry.CreateRegistrationLookup().GetOrThrow(unboundTagKey);
        InvalidOperationException unboundTagsException = Capture<InvalidOperationException>(
            unboundTagRegistry.Freeze,
            "Freeze must reject unbound named tags.");
        Require(
            unboundTagsException.Message == $"Unbound tags in registry {unboundTagRegistry.Key}: [{unboundTagKey.Location}]",
            "MappedRegistry.freeze must sort and list unbound tag identifiers in its vanilla diagnostic.");

        var intrusiveRegistry = new Registry<Block>(ResourceLocation.Parse("asteria:intrusive_registry"), allowIntrusiveHolders: true);
        Block intrusiveValue = new TestBlockDefinition(BlockProperties.Of());
        Holder<Block>.Reference intrusiveHolder = intrusiveRegistry.CreateIntrusiveHolder(intrusiveValue);
        InvalidOperationException unboundKeyException = RequireThrows<InvalidOperationException>(
            () => _ = intrusiveHolder.Key,
            "An intrusive holder without a registry key must reject key access.");
        Require(
            unboundKeyException.Message == $"Trying to access unbound value '{intrusiveValue}' from registry {intrusiveRegistry}",
            "Holder.Reference.key must include its value and owner in the vanilla error message.");
        RegistryKey<Block> intrusiveKey = new(intrusiveRegistry.Location, entryId);
        RequireThrows<InvalidOperationException>(
            () => intrusiveHolder.Is(intrusiveKey),
            "Holder.Reference.is(resource key) must access the key and fail before an intrusive holder is registered.");
        RequireThrows<InvalidOperationException>(
            () => intrusiveHolder.Is(entryId),
            "Holder.Reference.is(identifier) must fail before an intrusive holder has a key.");
        RequireThrows<InvalidOperationException>(
            () => intrusiveHolder.Is(_ => true),
            "Holder.Reference.is(predicate) must fail before an intrusive holder has a key.");
        RequireThrows<InvalidOperationException>(
            () => intrusiveHolder.Unwrap(),
            "Holder.Reference.unwrap must fail before an intrusive holder has a key.");
        RequireThrows<InvalidOperationException>(
            () => intrusiveHolder.GetRegisteredNameIfPresent(),
            "Holder.Reference.unwrapKey must fail before an intrusive holder has a key.");

        RequireThrows<InvalidOperationException>(
            () => intrusiveRegistry.Register(entryId, new TestBlockDefinition(BlockProperties.Of())),
            "Intrusive registries must reject values without a pre-created intrusive holder.");

        Holder<Block>.Reference registeredIntrusiveHolder = intrusiveRegistry.Register(entryId, intrusiveValue);
        Require(ReferenceEquals(registeredIntrusiveHolder, intrusiveHolder), "Intrusive registration must reuse its pre-created holder.");
        Require(intrusiveHolder.Is(intrusiveKey), "An intrusive holder must match its key after registration binds it.");
        Require(intrusiveHolder.Is(entryId), "An intrusive holder must match its identifier after registration binds its key.");
        Require(intrusiveHolder.Is(key => key.Equals(intrusiveKey)), "An intrusive holder must expose its key to predicates after registration.");
        Require(intrusiveHolder.UnwrapKey() == intrusiveKey, "An intrusive holder must unwrap to its key after registration.");

        Holder<Block>.Reference secondIntrusiveHolder =
            intrusiveRegistry.CreateIntrusiveHolder(intrusiveValue);
        Require(
            !ReferenceEquals(secondIntrusiveHolder, intrusiveHolder),
            "MappedRegistry.createIntrusiveHolder must create a new holder when the registered value is no longer in the unregistered holder map.");
        InvalidOperationException orphanIntrusiveHolderException = Capture<InvalidOperationException>(
            intrusiveRegistry.Freeze,
            "A second intrusive holder created after registration must remain unregistered and prevent freeze.");
        Require(
            orphanIntrusiveHolderException.Message == $"Some intrusive holders were not registered: [{secondIntrusiveHolder}]",
            "MappedRegistry.freeze must list unregistered intrusive holders in its vanilla diagnostic.");

        var frozenIntrusiveRegistry = new Registry<Block>(
            ResourceLocation.Parse("asteria:frozen_intrusive_registry"),
            allowIntrusiveHolders: true);
        Block frozenIntrusiveValue = new TestBlockDefinition(BlockProperties.Of());
        RegistryKey<Block> frozenIntrusiveKey = new(
            frozenIntrusiveRegistry.Location,
            ResourceLocation.Parse("asteria:frozen_intrusive_entry"));
        HolderGetter<Block> staleIntrusiveLookup = frozenIntrusiveRegistry.CreateRegistrationLookup();
        Holder<Block>.Reference frozenIntrusiveHolder =
            frozenIntrusiveRegistry.CreateIntrusiveHolder(frozenIntrusiveValue);
        Require(
            ReferenceEquals(
                frozenIntrusiveRegistry.Register(frozenIntrusiveKey, frozenIntrusiveValue, RegistrationInfo.BuiltIn),
                frozenIntrusiveHolder),
            "An intrusive registration must bind the pre-created holder.");
        frozenIntrusiveRegistry.Freeze();
        Require(
            ReferenceEquals(staleIntrusiveLookup.Get(frozenIntrusiveKey), frozenIntrusiveHolder),
            "A stale registration lookup must still return an already registered holder after freeze.");
        RequireThrows<InvalidOperationException>(
            () => staleIntrusiveLookup.Get(new RegistryKey<Block>(
                frozenIntrusiveRegistry.Location,
                ResourceLocation.Parse("asteria:late_intrusive_entry"))),
            "A stale registration lookup must not create a new holder after freeze.");
        RequireThrows<InvalidOperationException>(
            () => frozenIntrusiveRegistry.CreateIntrusiveHolder(
                new TestBlockDefinition(BlockProperties.Of())),
            "MappedRegistry drops its intrusive-holder map after a successful freeze.");
    }

    private static void CheckHolderReferenceValueBinding()
    {
        System.Reflection.MethodInfo bindValue =
            typeof(Holder<Block>.Reference).GetMethod(
                "BindValue",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Holder.Reference.BindValue was not found.");

        var standaloneRegistry = new Registry<Block>(ResourceLocation.Parse("asteria:standalone_binding"));
        RegistryKey<Block> standaloneKey = new(
            standaloneRegistry.Location,
            ResourceLocation.Parse("asteria:standalone_entry"));
        Holder<Block>.Reference standalone =
            standaloneRegistry.CreateRegistrationLookup().GetOrThrow(standaloneKey);
        Block firstValue = new TestBlockDefinition(BlockProperties.Of());
        Block replacementValue = new TestBlockDefinition(BlockProperties.Of());

        _ = bindValue.Invoke(standalone, new object[] { firstValue });
        _ = bindValue.Invoke(standalone, new object[] { replacementValue });
        Require(
            ReferenceEquals(standalone.Value, replacementValue),
            "Vanilla stand-alone holder references may replace their bound value; only intrusive holders enforce identity.");

        var intrusiveRegistry = new Registry<Block>(
            ResourceLocation.Parse("asteria:intrusive_binding"),
            allowIntrusiveHolders: true);
        Block intrusiveValue = new TestBlockDefinition(BlockProperties.Of());
        Block differentValue = new TestBlockDefinition(BlockProperties.Of());
        Holder<Block>.Reference intrusive =
            intrusiveRegistry.CreateIntrusiveHolder(intrusiveValue);

        _ = bindValue.Invoke(intrusive, new object[] { intrusiveValue });
        System.Reflection.TargetInvocationException invocationException =
            Capture<System.Reflection.TargetInvocationException>(
                () => bindValue.Invoke(intrusive, new object[] { differentValue }),
                "Vanilla intrusive holders must reject replacing their original value.");
        Require(
            invocationException.InnerException?.GetType() == typeof(InvalidOperationException),
            "An intrusive holder value mismatch must remain an illegal-state failure.");
        Require(
            invocationException.InnerException?.Message ==
                $"Can't change holder null value: existing={intrusiveValue}, new={differentValue}",
            "Intrusive holder value mismatches must use the vanilla diagnostic.");
        Require(
            ReferenceEquals(intrusive.Value, intrusiveValue),
            "A failed intrusive holder rebind must preserve its original value.");
    }

    private static void CheckIntrusiveRegistrationFailureDiagnostics()
    {
        var registry = new Registry<Block>(
            ResourceLocation.Parse("asteria:missing_intrusive_registration"),
            allowIntrusiveHolders: true);
        Block unregisteredValue = new TestBlockDefinition(BlockProperties.Of());
        RegistryKey<Block> unregisteredKey = new(
            registry.Location,
            ResourceLocation.Parse("asteria:missing_holder_entry"));

        InvalidOperationException missingHolderException = Capture<InvalidOperationException>(
            () => registry.Register(unregisteredKey, unregisteredValue, RegistrationInfo.BuiltIn),
            "Intrusive registration must reject values without a pre-created holder.");
        Require(
            missingHolderException.Message ==
                $"Missing intrusive holder for {unregisteredKey}:{unregisteredValue}",
            "Missing intrusive holder failures must preserve the vanilla diagnostic.");

        RegistryKey<Block> placeholderKey = new(
            registry.Location,
            ResourceLocation.Parse("asteria:intrusive_placeholder"));
        InvalidOperationException placeholderException = Capture<InvalidOperationException>(
            () => registry.CreateRegistrationLookup().GetOrThrow(placeholderKey),
            "Intrusive registries must reject standalone holder placeholders.");
        Require(
            placeholderException.Message == "This registry can't create new holders without value",
            "Intrusive placeholder lookup must preserve the vanilla no-value diagnostic.");
    }

    private static void CheckRegistryLifecycleAndRegistrationInfo()
    {
        ResourceLocation registryId = ResourceLocation.Parse("asteria:lifecycle_registry");
        ResourceLocation firstId = ResourceLocation.Parse("asteria:first");
        ResourceLocation secondId = ResourceLocation.Parse("asteria:second");
        ResourceLocation thirdId = ResourceLocation.Parse("asteria:third");
        ResourceLocation fourthId = ResourceLocation.Parse("asteria:fourth");

        var registry = new Registry<Block>(registryId);
        Require(ReferenceEquals(registry.RegistryLifecycle, Lifecycle.Stable()), "A mapped registry must start with its supplied stable lifecycle.");
        Require(registry.GetRegistrationInfo(new RegistryKey<Block>(registryId, firstId)) is null, "Unknown registry keys must not have registration metadata.");

        var firstKey = new RegistryKey<Block>(registryId, firstId);
        var secondKey = new RegistryKey<Block>(registryId, secondId);
        var thirdKey = new RegistryKey<Block>(registryId, thirdId);
        var fourthKey = new RegistryKey<Block>(registryId, fourthId);

        RegistrationInfo firstInfo = new(Lifecycle.Deprecated(10));
        registry.Register(firstKey, new TestBlockDefinition(BlockProperties.Of()), firstInfo);
        Require(ReferenceEquals(registry.GetRegistrationInfo(firstKey), firstInfo), "Registration metadata must be retained per registered key.");
        Require(registry.RegistryLifecycle is Lifecycle.DeprecatedLifecycle firstLifecycle && firstLifecycle.Since == 10, "A deprecated registration must make the registry lifecycle deprecated.");

        RegistrationInfo earlierDeprecatedInfo = new(Lifecycle.Deprecated(5));
        registry.Register(secondKey, new TestBlockDefinition(BlockProperties.Of()), earlierDeprecatedInfo);
        Require(
            registry.RegistryLifecycle is Lifecycle.DeprecatedLifecycle secondLifecycle && secondLifecycle.Since == 5,
            "Aggregated deprecated lifecycles must retain the earliest deprecation version.");

        RegistrationInfo stableInfo = RegistrationInfo.BuiltIn;
        registry.Register(thirdKey, new TestBlockDefinition(BlockProperties.Of()), stableInfo);
        Require(
            registry.RegistryLifecycle is Lifecycle.DeprecatedLifecycle thirdLifecycle && thirdLifecycle.Since == 5,
            "A stable registration must not erase an existing deprecated lifecycle.");

        RegistrationInfo experimentalInfo = new(Lifecycle.Experimental());
        registry.Register(fourthKey, new TestBlockDefinition(BlockProperties.Of()), experimentalInfo);
        Require(ReferenceEquals(registry.RegistryLifecycle, Lifecycle.Experimental()), "An experimental registration must make the aggregate lifecycle experimental.");

        var builtInRegistry = new Registry<Block>(ResourceLocation.Parse("asteria:built_in_registration"));
        var builtInKey = new RegistryKey<Block>(
            builtInRegistry.Location,
            ResourceLocation.Parse("asteria:built_in"));
        builtInRegistry.Register(builtInKey.Location, new TestBlockDefinition(BlockProperties.Of()));
        Require(
            Equals(builtInRegistry.GetRegistrationInfo(builtInKey), RegistrationInfo.BuiltIn),
            "The ordinary register overload must attach RegistrationInfo.BUILT_IN.");
        Require(ReferenceEquals(builtInRegistry.RegistryLifecycle, Lifecycle.Stable()), "Built-in registration must preserve stable lifecycle.");

        var defaultedRegistry = new DefaultedRegistry<Block>(
            ResourceLocation.Parse("asteria:defaulted_lifecycle"),
            ResourceLocation.Parse("asteria:default"));
        Block defaultBlock = new TestBlockDefinition(BlockProperties.Of());
        RegistryKey<Block> defaultKey = new(
            defaultedRegistry.Location,
            ResourceLocation.Parse("asteria:default"));
        defaultedRegistry.Register(defaultKey, defaultBlock, RegistrationInfo.BuiltIn);
        Require(
            ReferenceEquals(defaultedRegistry.GetAny(), defaultedRegistry.GetHolder(defaultKey)),
            "Registering through the key/metadata overload must bind DefaultedRegistry's default holder.");
        defaultedRegistry.Freeze();
        Require(
            ReferenceEquals(defaultedRegistry.GetAny()?.Value, defaultBlock),
            "The default holder value must become accessible after registry freeze.");
    }

    private static void CheckRegistryValueLookups()
    {
        ResourceLocation registryId = ResourceLocation.Parse("asteria:value_lookup_registry");
        ResourceLocation entryId = ResourceLocation.Parse("asteria:registered");
        ResourceLocation missingId = ResourceLocation.Parse("asteria:missing");
        var registry = new Registry<Block>(registryId);
        Block registeredValue = new TestBlockDefinition(BlockProperties.Of());
        var registeredKey = new RegistryKey<Block>(registryId, entryId);
        registry.Register(registeredKey, registeredValue, RegistrationInfo.BuiltIn);

        Require(
            registry.GetResourceKey(registeredValue) == registeredKey,
            "Registry.getResourceKey must recover the resource key for a registered value.");
        Require(
            registry.GetResourceKey(new TestBlockDefinition(BlockProperties.Of())) is null,
            "Registry.getResourceKey must return no key for an unregistered value.");

        registry.Freeze();

        Require(
            ReferenceEquals(registry.GetOptional(entryId), registeredValue),
            "Registry.getOptional(identifier) must return a registered value.");
        Require(
            ReferenceEquals(registry.GetOptional(registeredKey), registeredValue),
            "Registry.getOptional(resource key) must return a registered value.");
        Require(
            registry.GetOptional(missingId) is null &&
            registry.GetOptional(new RegistryKey<Block>(registryId, missingId)) is null,
            "Optional lookups must preserve missing-entry semantics.");
        Require(
            ReferenceEquals(registry.GetValueOrThrow(registeredKey), registeredValue),
            "getValueOrThrow must return the value registered for a resource key.");
        RegistryKey<Block> missingKey = new(registryId, missingId);
        InvalidOperationException missingValueException = Capture<InvalidOperationException>(
            () => registry.GetValueOrThrow(missingKey),
            "getValueOrThrow must reject a missing resource key.");
        Require(
            missingValueException.Message == $"Missing key in {registry.Key}: {missingKey}",
            "Registry.getValueOrThrow must use the vanilla missing-key error message.");

        HolderLookup<Block>.RegistryLookup filteredLookup =
            ((HolderLookup<Block>.RegistryLookup)registry).FilterElements(_ => false);
        InvalidOperationException missingElementException = Capture<InvalidOperationException>(
            () => filteredLookup.GetOrThrow(missingKey),
            "HolderGetter.getOrThrow must reject a missing filtered entry.");
        Require(
            missingElementException.Message == $"Missing element {missingKey}",
            "HolderGetter.getOrThrow(element) must use the vanilla missing-element error message.");
        TagKey<Block> missingTagKey = new(registryId, missingId);
        InvalidOperationException missingTagException = Capture<InvalidOperationException>(
            () => filteredLookup.GetOrThrow(missingTagKey),
            "HolderGetter.getOrThrow must reject a missing tag.");
        Require(
            missingTagException.Message == $"Missing tag {missingTagKey}",
            "HolderGetter.getOrThrow(tag) must use the vanilla missing-tag error message.");

        var missingDefault = new DefaultedRegistry<Block>(
            ResourceLocation.Parse("asteria:missing_default_registry"),
            ResourceLocation.Parse("asteria:not_registered"));
        Require(
            missingDefault.GetKey(new TestBlockDefinition(BlockProperties.Of())) == missingDefault.DefaultKey,
            "DefaultedMappedRegistry.getKey must return defaultKey even before its default holder is registered.");
        missingDefault.Freeze();
        RequireThrows<InvalidOperationException>(
            () => missingDefault.GetValue(missingId),
            "A defaulted registry without a default entry must still freeze and fail only when fallback lookup is required.");

        var defaulted = new DefaultedRegistry<Block>(
            ResourceLocation.Parse("asteria:defaulted_value_lookup"),
            ResourceLocation.Parse("asteria:air"));
        Block air = new TestBlockDefinition(BlockProperties.Of());
        defaulted.Register(ResourceLocation.Parse("asteria:air"), air);
        defaulted.Freeze();
        Require(
            ReferenceEquals(defaulted.GetValue(missingId), air),
            "DefaultedMappedRegistry.getValue(identifier) must return the default value.");
        Require(
            defaulted.GetOptional(missingId) is null,
            "DefaultedMappedRegistry.getOptional(identifier) must remain exact and must not apply the default.");
        Require(
            defaulted.GetOptional(new RegistryKey<Block>(defaulted.Location, missingId)) is null,
            "DefaultedMappedRegistry optional resource-key lookup must remain exact.");
    }

    private static void CheckBlockStateDefinition()
    {
        StateDefinition definition = Blocks.TEST_BLOCK.StateDefinition;
        Require(definition.PossibleStates.Count == 6, "powered(2) x stage(3) must create six states.");
        Require(!definition.IsSingletonState, "StateDefinition.isSingletonState must be false when properties are declared.");
        Require(
            definition.ToString() == $"StateDefinition{{block={Blocks.TEST_BLOCK}, properties=[powered, stage]}}",
            "StateDefinition.toString must expose the owner and sorted property names.");
        Require(
            Blocks.AIR.StateDefinition.IsSingletonState && Blocks.AIR.DefaultState.IsSingletonState,
            "An empty StateDefinition and its StateHolder must both report singleton state.");
        Require(
            !Blocks.TEST_BLOCK.DefaultState.IsSingletonState,
            "A state definition with properties must not report singleton states.");

        var alternateDefaultOwner = new TestBlockDefinition(BlockProperties.Of());
        BooleanProperty addedAfterConstruction = BooleanProperty.Create("added_after_construction");
        alternateDefaultOwner.AddPropertyAfterConstruction(addedAfterConstruction);
        Require(
            !alternateDefaultOwner.StateDefinition.HasProperty(addedAfterConstruction),
            "StateDefinition.Builder additions after create must not mutate the already materialized definition.");
        alternateDefaultOwner.SetDefaultState(Blocks.AIR.DefaultState);
        Require(
            ReferenceEquals(alternateDefaultOwner.DefaultState, Blocks.AIR.DefaultState),
            "registerDefaultState must store the supplied state directly, matching vanilla.");
        Require(
            definition.Properties.Select(property => property.Name).SequenceEqual(new[] { "powered", "stage" }),
            "State properties must be sorted by name.");
        Require(ReferenceEquals(definition.GetProperty("powered"), Blocks.TEST_POWERED), "Property lookup must return the declared property.");
        Require(
            definition.GetProperty("") is null && definition.GetProperty(" ") is null &&
            definition.GetProperty("missing") is null,
            "StateDefinition.getProperty must return no result for empty, whitespace, and unknown property names.");
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

        InvalidOperationException repeatedInitialization = Capture<InvalidOperationException>(
            () => defaultState.InitializeNeighbors(Array.Empty<BlockState[]>()),
            "State-holder neighbor tables must only be initialized once.");
        Require(
            repeatedInitialization.Message == string.Empty,
            "StateHolder.initializeNeighbors must throw the vanilla empty-message IllegalStateException.");

        BooleanProperty foreignPowered = BooleanProperty.Create("powered");
        Require(Blocks.TEST_POWERED.Equals(foreignPowered), "Boolean properties with the same value type and name must compare equal.");
        Require(Blocks.TEST_POWERED.GetHashCode() == foreignPowered.GetHashCode(), "Equal Boolean properties must have equal hash codes.");
        Require(ReferenceEquals(changedState, changedState.TrySetValue(foreignPowered, false)), "trySetValue with a foreign property instance must preserve the current state.");
        Require(!changedState.TryGetValue(foreignPowered, out _), "A separate property instance must not be accepted by name alone.");
        Require(
            foreignPowered.ToString() ==
                "BooleanProperty{name=powered, clazz=class java.lang.Boolean, values=[true, false]}",
            "Property.toString must expose the concrete type, boxed Java value class, name, and ordered values.");

        ArgumentException missingGetProperty = Capture<ArgumentException>(
            () => changedState.GetValue(foreignPowered),
            "getValue must reject a property not owned by the block definition.");
        Require(
            missingGetProperty.Message ==
                $"Cannot get property {foreignPowered} as it does not exist in {changedState.Block}",
            "getValue must use the vanilla missing-property diagnostic.");

        ArgumentException missingSetProperty = Capture<ArgumentException>(
            () => changedState.SetValue(foreignPowered, false),
            "setValue must reject a property not owned by the block definition.");
        Require(
            missingSetProperty.Message ==
                $"Cannot set property {foreignPowered} as it does not exist in {changedState.Block}",
            "setValue must use the vanilla missing-property diagnostic.");

        ArgumentException invalidSetValue = Capture<ArgumentException>(
            () => changedState.SetValue(Blocks.TEST_STAGE, 9),
            "setValue must throw IllegalArgumentException for values outside the property's allowed set.");
        string invalidSetMessage =
            $"Cannot set property {Blocks.TEST_STAGE} to 9 on {changedState.Block}, it is not an allowed value";
        Require(
            invalidSetValue.Message == invalidSetMessage,
            "setValue must use the vanilla invalid-value diagnostic without a parameter-name suffix.");

        ArgumentException invalidTrySetValue = Capture<ArgumentException>(
            () => changedState.TrySetValue(Blocks.TEST_STAGE, 9),
            "trySetValue must throw IllegalArgumentException for values outside the property's allowed set.");
        Require(
            invalidTrySetValue.Message == invalidSetMessage,
            "trySetValue must use the same vanilla invalid-value diagnostic as setValue.");

        ArgumentException invalidMaximum = Capture<ArgumentException>(
            () => IntegerProperty.Create("only", 0, 0),
            "IntegerProperty must throw IllegalArgumentException when max is not greater than min.");
        Require(
            invalidMaximum.Message == "Max value of only must be greater than min (0)",
            "IntegerProperty must use the vanilla maximum validation diagnostic.");

        ArgumentException invalidMinimum = Capture<ArgumentException>(
            () => IntegerProperty.Create("negative", -1, 1),
            "IntegerProperty must throw IllegalArgumentException when min is negative.");
        Require(
            invalidMinimum.Message == "Min value of negative must be 0 or greater",
            "IntegerProperty must use the vanilla minimum validation diagnostic.");

        BooleanProperty invalidName = BooleanProperty.Create("Invalid Name");
        ArgumentException invalidNameException = Capture<ArgumentException>(
            () => new TestBlockDefinition(BlockProperties.Of(), builder => builder.Add(invalidName)),
            "Property names must be validated when the state definition is built.");
        Require(
            invalidNameException.Message == "Block{[unregistered]} has invalidly named property: Invalid Name",
            "StateDefinition.Builder must include the owner and vanilla invalid-name diagnostic.");

        BooleanProperty duplicateFirst = BooleanProperty.Create("duplicate");
        BooleanProperty duplicateSecond = BooleanProperty.Create("duplicate");
        ArgumentException duplicatePropertyException = Capture<ArgumentException>(
            () => new TestBlockDefinition(
                BlockProperties.Of(),
                builder =>
                {
                    builder.Add(duplicateFirst);
                    builder.Add(duplicateSecond);
                }),
            "StateDefinition.Builder must reject duplicate property names.");
        Require(
            duplicatePropertyException.Message == "Block{[unregistered]} has duplicate property: duplicate",
            "StateDefinition.Builder must use the vanilla duplicate-property diagnostic.");
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
        ArgumentException missingState = Capture<ArgumentException>(
            () => ((IIdMap<BlockState>)ids).ByIdOrThrow(-1),
            "IdMap.byIdOrThrow must reject invalid global state IDs.");
        Require(
            missingState.Message == "No value with id -1",
            "IdMap.byIdOrThrow must use the vanilla missing-ID error message.");
        ArgumentException missingId = Capture<ArgumentException>(
            () => ((IIdMap<BlockState>)ids).GetIdOrThrow(null),
            "IdMap.getIdOrThrow must reject values absent from the ID map.");
        Require(
            missingId.Message == $"Can't find id for '' in map {ids}",
            "IdMap.getIdOrThrow must use the vanilla missing-value error message.");
    }

    private static void CheckPropertyImplementations()
    {
        BooleanProperty lit = BooleanProperty.Create("lit");
        Require(lit.PossibleValues.SequenceEqual(new[] { true, false }), "Boolean values must be true then false, matching Minecraft.");
        Require(lit.IndexOf(true) == 0 && lit.IndexOf(false) == 1, "BooleanProperty.getInternalIndex must use its fixed true/false indices.");
        Require(lit.TryParse("true", out bool parsedTrue) && parsedTrue, "Boolean parsing must accept true.");
        Require(!lit.TryParse("TRUE", out _), "Boolean parsing must be case-sensitive.");

        StatePropertyValue<bool>[] booleanValues = lit.GetAllValues().ToArray();
        Require(
            booleanValues.Select(value => value.Value).SequenceEqual(new[] { true, false }) &&
            booleanValues.Select(value => value.ToString()).SequenceEqual(new[] { "lit=true", "lit=false" }),
            "Property.getAllValues must expose values in the property's declared order and format them as name=value.");
        Require(
            Blocks.TEST_POWERED.Value(Blocks.TEST_BLOCK.DefaultState).Value == false,
            "Property.value(StateHolder) must wrap the value currently stored in the state.");
        Require(
            lit.Value(true) == booleanValues[0] &&
            booleanValues[0].ValueName == "true",
            "Property.Value must retain the owning property, typed value, and serialized value name.");

        IStatePropertyValue[] stateValues = Blocks.TEST_BLOCK.DefaultState.GetValues().ToArray();
        Require(
            stateValues.Select(value => value.Property.Name).SequenceEqual(new[] { "powered", "stage" }) &&
            stateValues.Select(value => value.ValueName).SequenceEqual(new[] { "false", "1" }) &&
            stateValues.Select(value => value.ToString()).SequenceEqual(new[] { "powered=false", "stage=1" }),
            "StateHolder.getValues must expose all property values in state-definition order.");

        ArgumentException invalidPropertyValue = Capture<ArgumentException>(
            () => new StatePropertyValue<int>(Blocks.TEST_STAGE, 9),
            "Property.Value must reject values outside the property's possible values.");
        Require(
            invalidPropertyValue.Message ==
                $"Value 9 does not belong to property {Blocks.TEST_STAGE}",
            "Property.Value must use the vanilla invalid-value diagnostic.");

        IntegerProperty age = IntegerProperty.Create("age", 2, 4);
        Require(age.Equals(IntegerProperty.Create("age", 2, 4)), "Integer properties must compare by name and possible values.");
        Require(
            age.ToString() ==
                "IntegerProperty{name=age, clazz=class java.lang.Integer, values=[2, 3, 4]}",
            "IntegerProperty.toString must match the vanilla property diagnostic format.");
        Require(!age.Equals(IntegerProperty.Create("age", 1, 4)), "Integer properties with different ranges must not compare equal.");
        Require(age.GetHashCode() == IntegerProperty.Create("age", 2, 4).GetHashCode(), "Equal integer properties must have equal hash codes.");
        Require(age.PossibleValues.SequenceEqual(new[] { 2, 3, 4 }), "Integer values must be inclusive and ascending.");
        Require(age.IndexOf(2) == 0 && age.IndexOf(4) == 2, "IntegerProperty.getInternalIndex must use min-offset indexing.");
        Require(age.IndexOf(1) == -1 && age.IndexOf(5) == -1, "IntegerProperty.getInternalIndex must reject values outside the range.");
        Require(age.TryParse("3", out int parsedAge) && parsedAge == 3, "Integer parsing must accept in-range values.");
        Require(!age.TryParse("5", out _), "Integer parsing must reject out-of-range values.");
        Require(!age.TryParse(" 3", out _), "Integer parsing must reject surrounding whitespace.");
        Require(age.GetName(5) == "5", "Integer serialization must return the numeric name without range validation.");

        EnumProperty<TestAxis> axis = EnumProperty<TestAxis>.Create("axis", new[] { TestAxis.Z, TestAxis.X }, value => value.ToString().ToLowerInvariant());
        Require(axis.PossibleValues.SequenceEqual(new[] { TestAxis.Z, TestAxis.X }), "Enum properties must preserve the supplied value order.");
        Require(axis.IndexOf(TestAxis.Z) == 0 && axis.IndexOf(TestAxis.X) == 1, "EnumProperty.getInternalIndex must use its precomputed value-to-index mapping.");
        Require(axis.IndexOf(TestAxis.Y) == -1, "EnumProperty.getInternalIndex must reject values excluded from its possible-value list.");
        Require(axis.TryParse("z", out TestAxis parsedAxis) && parsedAxis == TestAxis.Z, "Enum parsing must use the configured name.");
        Require(
            axis.ToString() ==
                "EnumProperty{name=axis, clazz=class Program$TestAxis, values=[Z, X]}",
            "EnumProperty.toString must retain the enum's Java binary class name and declared values.");
        Require(axis.GetName(TestAxis.Y) == "y", "EnumProperty.getName must serialize enum constants outside the filtered possible-value set.");
        Require(!axis.TryParse("y", out _), "EnumProperty parsing must still reject values outside its possible-value set.");

        EnumProperty<TestAxis> invalidSerialized = EnumProperty<TestAxis>.Create(
            "invalid_serialized",
            new[] { TestAxis.Z, TestAxis.X },
            value => value == TestAxis.Z ? "Invalid Name" : "x");
        Require(invalidSerialized.GetName(TestAxis.Z) == "Invalid Name", "EnumProperty construction must retain enum serialized names before state-definition validation.");
        ArgumentException invalidSerializedException = Capture<ArgumentException>(
            () => new TestBlockDefinition(BlockProperties.Of(), builder => builder.Add(invalidSerialized)),
            "StateDefinition.Builder must validate serialized enum value names when the property is added.");
        Require(
            invalidSerializedException.Message ==
                "Block{[unregistered]} has property: invalid_serialized with invalidly named value: Invalid Name",
            "StateDefinition.Builder must use the vanilla invalid-value-name diagnostic.");

        EnumProperty<TestDeclaredOrder> declared = EnumProperty<TestDeclaredOrder>.Create("declared");
        EnumProperty<TestDeclaredOrder> singleton = EnumProperty<TestDeclaredOrder>.Create(
            "singleton",
            new[] { TestDeclaredOrder.Y });
        Require(
            singleton.PossibleValues.SequenceEqual(new[] { TestDeclaredOrder.Y }),
            "EnumProperty may represent a single possible value before it is added to a state definition.");
        ArgumentException singletonException = Capture<ArgumentException>(
            () => new TestBlockDefinition(BlockProperties.Of(), builder => builder.Add(singleton)),
            "StateDefinition.Builder must reject properties with one or fewer possible values.");
        Require(
            singletonException.Message ==
                "Block{[unregistered]} attempted use property singleton with <= 1 possible values",
            "StateDefinition.Builder must use the vanilla single-value property diagnostic.");
        Require(
            declared.Equals(EnumProperty<TestDeclaredOrder>.Create("declared")),
            "Enum properties with the same name, ordered values, and serialized names must compare equal.");
        Require(
            declared.GetHashCode() == EnumProperty<TestDeclaredOrder>.Create("declared").GetHashCode(),
            "Equal enum properties must have equal hash codes.");
        EnumProperty<TestDeclaredOrder> renamed = EnumProperty<TestDeclaredOrder>.Create(
            "declared",
            new[] { TestDeclaredOrder.Z, TestDeclaredOrder.X, TestDeclaredOrder.Y },
            value => value.ToString());
        Require(
            declared.Equals(renamed) && declared.GetHashCode() == renamed.GetHashCode(),
            "EnumProperty equality must depend on the ordered enum values, not on a per-instance name selector.");
        ArgumentException emptyEnumException = Capture<ArgumentException>(
            () => EnumProperty<TestAxis>.Create("empty", Array.Empty<TestAxis>()),
            "EnumProperty must reject an empty set of possible values.");
        Require(
            emptyEnumException.Message == "Trying to make empty EnumProperty 'empty'",
            "EnumProperty must use the vanilla empty-property diagnostic.");
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
        Require(
            ReferenceEquals(storage.GetRaw(), storage.GetRaw()),
            "SimpleBitStorage.getRaw must expose its backing array.");
        int[] unpackedStorage = new int[ChunkSection.BlockCount];
        storage.Unpack(unpackedStorage);
        Require(
            unpackedStorage[11] == 3 &&
            unpackedStorage[12] == 17 &&
            unpackedStorage[13] == 9 &&
            unpackedStorage[4095] == 23,
            "SimpleBitStorage.unpack must write values in logical index order.");
        var fromValues = new PackedBitStorage(5, 4, new[] { 1, 2, 3, 4 });
        Require(
            Enumerable.Range(0, 4).Select(fromValues.Get).SequenceEqual(new[] { 1, 2, 3, 4 }),
            "SimpleBitStorage must pack an initial integer array in logical index order.");
        RequireThrows<ArgumentOutOfRangeException>(() => storage.Set(0, 32), "Storage must reject values that do not fit.");

        PackedBitStorage.InitializationException invalidRawLength = RequireThrows<PackedBitStorage.InitializationException>(
            () => _ = new PackedBitStorage(5, 13, new ulong[1]),
            "Raw packed storage must reject a word array with the wrong length using the vanilla initialization exception.");
        Require(
            invalidRawLength.Message == "Invalid length given for storage, got: 1 but expected: 2",
            "SimpleBitStorage initialization failures must preserve the vanilla exception message.");

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

    private static void CheckPackedBitStorageWidths()
    {
        for (int bits = 1; bits <= 32; bits++)
        {
            int valuesPerLong = 64 / bits;
            int size = valuesPerLong * 2 + Math.Max(1, valuesPerLong / 3);
            int maximumValue = bits == 32
                ? int.MaxValue
                : (int)((1UL << bits) - 1UL);
            int[] expected = Enumerable.Range(0, size)
                .Select(index => (index * 17 + bits * 11) & maximumValue)
                .ToArray();

            var storage = new PackedBitStorage(bits, size, expected);
            Require(
                storage.LongCount == (size + valuesPerLong - 1) / valuesPerLong,
                $"Bit width {bits} must allocate the exact number of non-spanning words.");

            for (int index = 0; index < size; index++)
            {
                Require(
                    storage.Get(index) == expected[index],
                    $"Bit width {bits} must round-trip initial values at index {index}.");
            }

            var traversed = new List<int>();
            storage.GetAll(traversed.Add);
            Require(
                traversed.SequenceEqual(expected),
                $"Bit width {bits} getAll must preserve logical order and ignore padded capacity.");

            int[] unpacked = Enumerable.Repeat(-1, size + 2).ToArray();
            storage.Unpack(unpacked);
            Require(
                unpacked.Take(size).SequenceEqual(expected) &&
                unpacked[size] == -1 &&
                unpacked[size + 1] == -1,
                $"Bit width {bits} unpack must preserve the destination tail.");

            int boundaryIndex = valuesPerLong - 1;
            int nextWordIndex = valuesPerLong;
            int previousBoundary = storage.GetAndSet(boundaryIndex, maximumValue);
            Require(
                previousBoundary == expected[boundaryIndex] &&
                storage.Get(boundaryIndex) == maximumValue,
                $"Bit width {bits} getAndSet must preserve the final entry of a word.");
            storage.Set(nextWordIndex, maximumValue);
            Require(
                storage.Get(nextWordIndex) == maximumValue,
                $"Bit width {bits} set must preserve the first entry of the next word.");

            storage.Fill(maximumValue);
            Require(
                Enumerable.Range(0, size).All(index => storage.Get(index) == maximumValue),
                $"Bit width {bits} fill must cover complete and partial words.");

            PackedBitStorage copy = storage.Copy();
            Require(
                Enumerable.Range(0, size).All(index => copy.Get(index) == maximumValue),
                $"Bit width {bits} copy must preserve every logical entry.");
        }
    }

    private static void CheckZeroBitStorage()
    {
        var storage = new ZeroBitStorage(4);
        Require(storage.BitsPerEntry == 0, "ZeroBitStorage must use zero bits per entry.");
        Require(storage.Size == 4 && storage.LongCount == 0, "ZeroBitStorage must retain its logical size without allocating words.");
        Require(storage.Get(0) == 0 && storage.Get(3) == 0, "Every valid zero-bit entry must be zero.");
        Require(storage.GetAndSet(2, 0) == 0, "ZeroBitStorage getAndSet must return zero.");
        storage.Set(1, 0);
        storage.Fill(0);

        var values = new List<int>();
        storage.GetAll(values.Add);
        Require(values.SequenceEqual(new[] { 0, 0, 0, 0 }), "ZeroBitStorage getAll must emit one zero per logical entry.");

        int[] unpacked = { 9, 9, 9, 9, 9, 9 };
        storage.Unpack(unpacked);
        Require(unpacked.SequenceEqual(new[] { 0, 0, 0, 0, 9, 9 }), "ZeroBitStorage unpack must zero only its logical range.");
        Require(ReferenceEquals(storage, storage.Copy()), "ZeroBitStorage copy must return the same immutable storage instance.");
        RequireThrows<ArgumentOutOfRangeException>(() => storage.Set(0, 1), "ZeroBitStorage must reject non-zero values.");
        RequireThrows<ArgumentOutOfRangeException>(() => storage.Get(4), "ZeroBitStorage must validate indexes.");
    }

    private static void CheckThreadingDetector()
    {
        var detector = new ThreadingDetector("BlockRegistryChecks");
        detector.CheckAndLock();

        using var contenderStarted = new ManualResetEventSlim();
        Exception? contenderException = null;
        var contender = new Thread(() =>
        {
            contenderStarted.Set();
            try
            {
                detector.CheckAndLock();
            }
            catch (Exception exception)
            {
                contenderException = exception;
            }
        })
        {
            Name = "BlockRegistryChecks contender"
        };

        contender.Start();
        Require(contenderStarted.Wait(TimeSpan.FromSeconds(2)), "The competing thread must start.");
        Require(
            SpinWait.SpinUntil(
                () => (contender.ThreadState & ThreadState.WaitSleepJoin) != 0,
                TimeSpan.FromSeconds(2)),
            "The competing thread must be blocked on the detector semaphore before the owner unlocks.");

        Exception? ownerException = null;
        try
        {
            detector.CheckAndUnlock();
        }
        catch (Exception exception)
        {
            ownerException = exception;
        }

        Require(contender.Join(TimeSpan.FromSeconds(2)), "The contending thread must be released by the owner.");
        Require(
            ownerException is InvalidOperationException &&
            ReferenceEquals(ownerException, contenderException),
            "ThreadingDetector must report concurrent access to both participating threads.");
        Require(
            ownerException is not null &&
            ownerException.Message.Contains("Thread dumps:", StringComparison.Ordinal) &&
            ownerException.Message.Contains("BlockRegistryChecks contender", StringComparison.Ordinal),
            "The threading exception must include stack dumps for the current and failed-to-acquire threads.");
        Require(
            ownerException.InnerException?.Message == "Accessing BlockRegistryChecks from multiple threads",
            "The threading exception must retain the vanilla report's underlying concurrency error.");

        var unboundedReleaseDetector = new ThreadingDetector("UnboundedSemaphore");
        unboundedReleaseDetector.CheckAndUnlock();
        unboundedReleaseDetector.CheckAndUnlock();
    }

    private static void CheckThreadingDetectorInterruptHandling()
    {
        const string detectorName = "InterruptedBlockRegistryChecks";
        var detector = new ThreadingDetector(detectorName);
        detector.CheckAndLock();

        Thread.CurrentThread.Interrupt();
        InvalidOperationException acquireException = Capture<InvalidOperationException>(
            () => detector.CheckAndLock(),
            "An interrupted semaphore waiter must still receive the threading detector exception.");
        Require(
            acquireException.InnerException?.Message == $"Accessing {detectorName} from multiple threads",
            "An interrupted waiter must not leak ThreadInterruptedException instead of the concurrency violation.");

        bool interruptRestored = false;
        try
        {
            Thread.Sleep(1);
        }
        catch (ThreadInterruptedException)
        {
            interruptRestored = true;
        }

        Require(
            interruptRestored,
            "ThreadingDetector must restore the interrupted status after its semaphore wait is interrupted.");

        InvalidOperationException releaseException = RequireThrows<InvalidOperationException>(
            () => detector.CheckAndUnlock(),
            "The original owner must still report the concurrent-access violation when it releases.");
        Require(
            releaseException.InnerException?.Message == $"Accessing {detectorName} from multiple threads",
            "The owner must retain the same concurrency error after an interrupted waiter.");
    }

    private static void CheckIdentityIdMap()
    {
        var map = CrudeIncrementalIntIdentityHashBiMap<IdentityTestValue>.Create(2);
        IdentityTestValue[] values = Enumerable.Range(0, 12)
            .Select(index => new IdentityTestValue(index < 2 ? 1 : index))
            .ToArray();

        int[] ids = values.Select(map.Add).ToArray();
        Require(
            ids.SequenceEqual(Enumerable.Range(0, values.Length)),
            "The incremental identity map must assign the next available numeric ID.");
        Require(
            map.GetId(values[0]) == 0 && map.GetId(values[1]) == 1,
            "The incremental map must use object identity rather than value equality.");
        Require(
            ReferenceEquals(map.ById(0), values[0]) &&
            ReferenceEquals(map.ById(1), values[1]),
            "The incremental map must preserve identity in reverse ID lookup.");
        Require(map.Count == values.Length, "The incremental map must maintain its entry count across growth.");
        Require(
            map.GetEntries().SequenceEqual(values),
            "The incremental map must enumerate values in numeric ID order.");

        IIdMap<IdentityTestValue> idMap = map;
        Require(
            idMap.Count == values.Length &&
            idMap.GetId(values[3]) == 3 &&
            ReferenceEquals(idMap.ById(3), values[3]),
            "CrudeIncrementalIntIdentityHashBiMap must expose the common vanilla IdMap contract.");
        Require(
            idMap.ByIdOrThrow(3) == values[3],
            "IdMap.byIdOrThrow must work through the identity map interface.");
        ArgumentException missingId = Capture<ArgumentException>(
            () => idMap.ByIdOrThrow(99),
            "IdMap.byIdOrThrow must reject missing identity-map IDs.");
        Require(
            missingId.Message == "No value with id 99",
            "Identity-map byIdOrThrow must inherit the vanilla missing-ID message.");
        IdentityTestValue unknown = new(999);
        ArgumentException missingValue = Capture<ArgumentException>(
            () => idMap.GetIdOrThrow(unknown),
            "IdMap.getIdOrThrow must reject values absent from the identity map.");
        Require(
            missingValue.Message == $"Can't find id for '{unknown}' in map {idMap}",
            "Identity-map getIdOrThrow must inherit the vanilla missing-value message.");

        CrudeIncrementalIntIdentityHashBiMap<IdentityTestValue> copy = map.Copy();
        map.Clear();
        Require(map.Count == 0 && map.GetId(values[0]) == -1, "Clear must empty the identity map.");
        Require(
            copy.Count == values.Length &&
            ReferenceEquals(copy.ById(11), values[11]) &&
            copy.GetId(values[11]) == 11,
            "Copy must preserve hash entries, ID mapping, and insertion cursor.");
    }

    private static void CheckIdentityIdMapSparseMappings()
    {
        var map = CrudeIncrementalIntIdentityHashBiMap<IdentityTestValue>.Create(4);
        IdentityTestValue highId = new(70);
        IdentityTestValue firstFreeId = new(0);
        IdentityTestValue secondFreeId = new(1);
        IdentityTestValue thirdFreeId = new(2);
        IdentityTestValue nextMappedId = new(4);

        map.AddMapping(highId, 7);
        Require(
            map.Count == 1 && map.GetId(highId) == 7 && ReferenceEquals(map.ById(7), highId),
            "Explicit sparse mappings must preserve identity in both lookup directions.");

        Require(map.Add(firstFreeId) == 0, "Add must use the first available ID below an existing sparse mapping.");
        Require(map.Add(secondFreeId) == 1, "Add must advance through available IDs in order.");
        Require(map.Add(thirdFreeId) == 2, "Add must continue filling gaps before a sparse ID.");
        map.AddMapping(nextMappedId, 4);

        Require(
            map.Count == 5 &&
            map.GetId(nextMappedId) == 4 &&
            ReferenceEquals(map.ById(4), nextMappedId) &&
            !map.Contains(3) &&
            map.Contains(7),
            "Explicit mappings must retain holes without disturbing existing sparse IDs.");

        IdentityTestValue finalFreeId = new(3);
        Require(map.Add(finalFreeId) == 3, "The insertion cursor must continue at the first remaining hole.");
        Require(
            ReferenceEquals(map.ById(3), finalFreeId) &&
            ReferenceEquals(map.ById(7), highId) &&
            map.Count == 6,
            "Filling a hole must not overwrite higher explicit mappings after capacity growth.");
    }

    private static void CheckPaletteExceptions()
    {
        MissingPaletteEntryException exception = new(37);
        Require(exception.Message == "Missing Palette entry for index 37.", "Missing palette entries must use the vanilla exception type and message.");
        Require(exception.Index == 37, "Missing palette entry exceptions must preserve the missing index.");
    }

    private static void CheckNoResizePaletteResizeFailure()
    {
        Type openType = typeof(PalettedContainer<>).Assembly.GetType(
            "Asteria.Content.Storage.NoResizePaletteResize\u00601",
            throwOnError: true)!;
        Type closedType = openType.MakeGenericType(typeof(string));
        object instance = closedType.GetProperty("Instance")!.GetValue(null)!;
        System.Reflection.MethodInfo onResize = closedType.GetMethod("OnResize")!;

        System.Reflection.TargetInvocationException invocationException =
            Capture<System.Reflection.TargetInvocationException>(
                () => onResize.Invoke(instance, new object[] { 2, "sentinel" }),
                "The no-resize palette handler must reject unexpected growth.");

        Require(
            invocationException.InnerException?.GetType() == typeof(ArgumentException),
            "The no-resize handler must throw the vanilla IllegalArgumentException equivalent.");
        Require(
            invocationException.InnerException?.Message ==
                "Unexpected palette resize, bits = 2, added value = sentinel",
            "The no-resize handler must preserve vanilla's diagnostic message.");
    }

    private static void CheckLinearPaletteOverflow()
    {
        BlockState first = Blocks.AIR.DefaultState;
        BlockState second = Blocks.TEST_BLOCK.DefaultState;
        BlockState third = second.SetValue(Blocks.TEST_STAGE, 2);
        var palette = new LinearPalette<BlockState>(1);
        var resize = new ThrowingPaletteResize<BlockState>();

        Require(palette.IdFor(first, resize) == 0, "Linear palettes must assign their first value to ID zero.");
        Require(palette.IdFor(second, resize) == 1, "A one-bit linear palette must accept its second value.");

        bool resizeFailed = false;
        try
        {
            palette.IdFor(third, resize);
        }
        catch (InvalidOperationException exception) when (exception.Message == "Unexpected resize to 2 bits.")
        {
            resizeFailed = true;
        }

        Require(resizeFailed, "A full linear palette must request the next bit width.");
        Require(
            palette.Count == 2,
            "A failed resize must not increment the original linear palette size.");
        Require(
            ReferenceEquals(palette.ValueFor(0), first) &&
            ReferenceEquals(palette.ValueFor(1), second),
            "A failed resize must preserve every existing linear palette entry.");
    }

    private static void CheckPalettedContainerInvalidIndexOrder()
    {
        BlockState placedState = Blocks.TEST_BLOCK.DefaultState;

        var setContainer = CreateBlockStateContainer(Blocks.AIR.DefaultState);
        bool setFailed = false;
        try
        {
            setContainer.Set(-1, 0, 0, placedState);
        }
        catch (ArgumentOutOfRangeException)
        {
            setFailed = true;
        }

        Require(setFailed, "An invalid Set index must still be rejected by the backing storage.");
        Require(
            setContainer.Mode == PaletteMode.Local && setContainer.PaletteSize == 2,
            "Set must resolve and publish the palette entry before backing storage validates the index, matching vanilla.");

        var getAndSetContainer = CreateBlockStateContainer(Blocks.AIR.DefaultState);
        bool getAndSetFailed = false;
        try
        {
            getAndSetContainer.GetAndSet(-1, 0, 0, placedState);
        }
        catch (ArgumentOutOfRangeException)
        {
            getAndSetFailed = true;
        }

        Require(getAndSetFailed, "An invalid GetAndSet index must be rejected by the backing storage.");
        Require(
            getAndSetContainer.Mode == PaletteMode.Local && getAndSetContainer.PaletteSize == 2,
            "GetAndSet must resolve and publish the palette entry before backing storage validates the index, matching vanilla.");
    }

    private static PalettedContainer<BlockState> CreateBlockStateContainer(BlockState defaultState)
    {
        PalettedContainerStrategy<BlockState> strategy =
            PalettedContainerStrategy<BlockState>.CreateForBlockStates(BuiltInRegistries.BlockStates);
        return new PalettedContainer<BlockState>(defaultState, strategy);
    }

    private static (int X, int Y, int Z) BlockCoordinates(int index)
    {
        if ((uint)index >= ChunkSection.BlockCount)
            throw new ArgumentOutOfRangeException(nameof(index));

        return (index & 15, (index >> 8) & 15, (index >> 4) & 15);
    }

    private static T GetAt<T>(PalettedContainer<T> container, int index) where T : class
    {
        var (x, y, z) = BlockCoordinates(index);
        return container.Get(x, y, z);
    }

    private static void SetAt<T>(PalettedContainer<T> container, int index, T value) where T : class
    {
        var (x, y, z) = BlockCoordinates(index);
        container.Set(x, y, z, value);
    }

    private static T GetAndSetAt<T>(PalettedContainer<T> container, int index, T value) where T : class
    {
        var (x, y, z) = BlockCoordinates(index);
        return container.GetAndSet(x, y, z, value);
    }

    private static T GetAndSetUncheckedAt<T>(PalettedContainer<T> container, int index, T value) where T : class
    {
        var (x, y, z) = BlockCoordinates(index);
        return container.GetAndSetUnchecked(x, y, z, value);
    }

    private static void CheckPalettedContainer()
    {
        BlockState defaultState = Blocks.TEST_BLOCK.DefaultState;
        var strategy = PalettedContainerStrategy<BlockState>.CreateForBlockStates(BuiltInRegistries.BlockStates);
        var palette = new PalettedContainer<BlockState>(defaultState, strategy);
        Require(palette.Size == ChunkSection.BlockCount, "Block-state strategy must create 4096 entries.");
        Require(palette.Mode == PaletteMode.SingleValue, "A uniform section must use a single-value palette.");
        Require(palette.BitsPerEntry == 0, "A single-value palette must use zero storage bits.");
        Require(palette.StorageLongCount == 0, "A single-value palette must allocate no packed array.");
        Require(ReferenceEquals(GetAt(palette, 2048), defaultState), "A single-value palette must return its only value for every index.");
        Require(!palette.MaybeHas(state => state.IsAir), "Single-value maybeHas must inspect the only value.");

        BlockState alternate = defaultState.SetValue(Blocks.TEST_STAGE, 2);
        BlockState previous = GetAndSetAt(palette, 10, alternate);
        Require(ReferenceEquals(previous, defaultState), "getAndSet must return the previous state.");
        Require(palette.Mode == PaletteMode.Local, "The second distinct value must create a local palette.");
        Require(palette.BitsPerEntry == 4, "Block-state local palettes start at four bits.");
        Require(palette.LocalPaletteImplementation?.StartsWith("LinearPalette", StringComparison.Ordinal) == true, "The four-bit palette must use linear lookup.");
        Require(ReferenceEquals(GetAt(palette, 10), alternate), "Local palette lookup must round-trip.");
        Require(!palette.MaybeHas(state => state.IsAir), "Local palette must report absent air.");
        Require(palette.MaybeHas(state => ReferenceEquals(state, alternate)), "Local palette must inspect its entries.");

        var presentValues = new List<BlockState>();
        palette.GetAll(presentValues.Add);
        Require(
            presentValues.Count == 2 &&
            ReferenceEquals(presentValues[0], defaultState) &&
            ReferenceEquals(presentValues[1], alternate),
            "GetAll must preserve the first-occurrence order of palette IDs.");

        var coordinateContainer = new PalettedContainer<BlockState>(defaultState, strategy);
        coordinateContainer.Set(2, 3, 4, alternate);
        Require(
            ReferenceEquals(coordinateContainer.Get(2, 3, 4), alternate),
            "Coordinate Set must write directly through the vanilla palette path.");
        Require(
            ReferenceEquals(coordinateContainer.GetAndSet(2, 3, 4, defaultState), alternate),
            "Coordinate GetAndSet must return the previous state.");

        int counted = 0;
        palette.Count((_, count) => counted += count);
        Require(counted == ChunkSection.BlockCount, "Palette count must account for every position.");
        PalettedContainer<BlockState> copy = palette.Copy();
        Require(ReferenceEquals(GetAt(copy, 10), alternate), "Copy must preserve stored values.");
        SetAt(copy, 10, defaultState);
        Require(ReferenceEquals(GetAt(palette, 10), alternate), "Mutating a copy must not mutate the original.");
        PalettedContainer<BlockState> recreated = palette.Recreate();
        Require(recreated.IsSingleValue, "Recreate must return a uniform container.");
        Require(ReferenceEquals(GetAt(recreated, 0), defaultState), "Recreate must use palette entry zero.");

        palette.Acquire();
        try
        {
            GetAndSetUncheckedAt(palette, 11, alternate);
        }
        finally { palette.Release(); }
    }

    private static void CheckPalettedContainerStrategyConfiguration()
    {
        var blockStrategy =
            PalettedContainerStrategy<BlockState>.CreateForBlockStates(BuiltInRegistries.BlockStates);
        var biomeStrategy =
            PalettedContainerStrategy<BlockState>.CreateForBiomes(BuiltInRegistries.BlockStates);

        Require(
            blockStrategy.BitsPerAxis == 4 && blockStrategy.EntryCount == 4096,
            "The block-state palette strategy must use a 16-cubed section.");
        Require(
            biomeStrategy.BitsPerAxis == 2 && biomeStrategy.EntryCount == 64,
            "The biome sizing strategy must use a 4-cubed container.");
        Require(
            blockStrategy.GetIndex(3, 5, 7) == 1395,
            "Block-state coordinates must flatten with X least significant, then Z, then Y.");
        Require(
            biomeStrategy.GetIndex(1, 2, 3) == 45,
            "Biome strategy coordinates must use its own two-bit axis width.");

        Require(
            blockStrategy.GetConfigurationForBitCount(0) ==
                new PaletteConfiguration(PaletteMode.SingleValue, 0, 0, false),
            "Zero block-state bits must select single-value storage.");
        Require(
            blockStrategy.GetConfigurationForBitCount(1) ==
                new PaletteConfiguration(PaletteMode.Local, 4, 4, false) &&
            blockStrategy.GetConfigurationForBitCount(4) ==
                new PaletteConfiguration(PaletteMode.Local, 4, 4, false),
            "Block-state palettes through four requested bits must use four-bit linear storage.");
        Require(
            blockStrategy.GetConfigurationForBitCount(5) ==
                new PaletteConfiguration(PaletteMode.Local, 5, 5, false) &&
            blockStrategy.GetConfigurationForBitCount(8) ==
                new PaletteConfiguration(PaletteMode.Local, 8, 8, false),
            "Block-state palettes from five through eight bits must use matching local storage widths.");
        Require(
            blockStrategy.GetConfigurationForBitCount(9) ==
                new PaletteConfiguration(PaletteMode.Global, 3, 9, true),
            "Block-state global palettes must separate the global map's resident width from the requested storage width.");

        Require(
            biomeStrategy.GetConfigurationForBitCount(1) ==
                new PaletteConfiguration(PaletteMode.Local, 1, 1, false) &&
            biomeStrategy.GetConfigurationForBitCount(3) ==
                new PaletteConfiguration(PaletteMode.Local, 3, 3, false),
            "Biome sizing must retain local widths from one through three bits.");
        Require(
            biomeStrategy.GetConfigurationForBitCount(4) ==
                new PaletteConfiguration(PaletteMode.Global, 3, 4, true),
            "Biome global sizing must retain the global map width separately from its requested storage width.");

        Require(
            blockStrategy.GetConfigurationForPaletteSize(17) ==
                new PaletteConfiguration(PaletteMode.Local, 5, 5, false) &&
            blockStrategy.GetConfigurationForPaletteSize(257) ==
                new PaletteConfiguration(PaletteMode.Global, 3, 9, true),
            "Palette-size configuration must use ceiling log2 at local and global transition boundaries.");
        Require(
            biomeStrategy.GetConfigurationForPaletteSize(8) ==
                new PaletteConfiguration(PaletteMode.Local, 3, 3, false) &&
            biomeStrategy.GetConfigurationForPaletteSize(9) ==
                new PaletteConfiguration(PaletteMode.Global, 3, 4, true),
            "Biome palette-size configuration must transition after eight values.");
    }

    private static void CheckPalettedContainerGrowthAndGlobalMode()
    {
        IntegerProperty variant = IntegerProperty.Create("variant", 0, 256);
        var manyStateBlock = new TestBlockDefinition(
            BlockProperties.Of(),
            builder => builder.Add(variant));

        var ids = new BlockStateIdMap();
        foreach (BlockState state in manyStateBlock.StateDefinition.PossibleStates) ids.Register(state);

        var strategy = PalettedContainerStrategy<BlockState>.CreateForBlockStates(ids);
        var container = new PalettedContainer<BlockState>(manyStateBlock.DefaultState, strategy);
        IReadOnlyList<BlockState> states = manyStateBlock.StateDefinition.PossibleStates;

        for (int value = 1; value <= 16; value++) SetAt(container, value - 1, states[value]);
        Require(container.Mode == PaletteMode.Local, "A palette of 17 states must remain indirect.");
        Require(container.BitsPerEntry == 5, "A seventeenth value must grow a four-bit palette to five bits.");
        Require(container.LocalPaletteImplementation?.StartsWith("HashMapPalette", StringComparison.Ordinal) == true, "A widened palette must use hash-map lookup.");

        for (int value = 17; value <= 256; value++) SetAt(container, value - 1, states[value]);
        Require(container.Mode == PaletteMode.Global, "The 257th distinct state must promote the palette to global IDs.");
        Require(container.BitsPerEntry == 9, "A 257-entry global map uses nine bits in memory.");
        Require(container.BitsInStorage == 9, "The requested global storage width is retained separately.");
        Require(container.AlwaysRepack, "Global configurations must always repack.");
        Require(container.PaletteSize == 257, "The global palette must expose the full state map.");

        CheckGlobalPaletteMemoryAndStorageWidths();
        Require(container.MaybeHas(state => ReferenceEquals(state, manyStateBlock.DefaultState)), "Global maybeHas must be conservative.");

        for (int value = 1; value <= 256; value++)
            Require(ReferenceEquals(GetAt(container, value - 1), states[value]), "Palette promotion must preserve every stored value.");
        Require(ReferenceEquals(GetAt(container, 256), manyStateBlock.DefaultState), "Palette promotion must preserve untouched defaults.");
        SetAt(container, 300, Blocks.AIR.DefaultState);
        Require(
            ReferenceEquals(GetAt(container, 300), states[0]),
            "The vanilla global palette must map values without a global ID to ID zero.");
    }

    private static void CheckGlobalPaletteMemoryAndStorageWidths()
    {
        IntegerProperty variant = IntegerProperty.Create("variant", 0, 1023);
        var block = new TestBlockDefinition(BlockProperties.Of(), builder => builder.Add(variant));
        var ids = new BlockStateIdMap();
        foreach (BlockState state in block.StateDefinition.PossibleStates)
            ids.Register(state);

        var strategy = PalettedContainerStrategy<BlockState>.CreateForBlockStates(ids);
        var container = new PalettedContainer<BlockState>(block.DefaultState, strategy);
        IReadOnlyList<BlockState> states = block.StateDefinition.PossibleStates;
        for (int value = 1; value <= 256; value++)
            SetAt(container, value - 1, states[value]);

        Require(container.Mode == PaletteMode.Global, "The 257th distinct value must select the global palette.");
        Require(container.BitsPerEntry == 10, "The full 1024-state ID map needs ten resident memory bits.");
        Require(container.BitsInStorage == 9, "The ninth-bit resize request is retained for packed storage.");
        Require(container.AlwaysRepack, "Global palette configurations must always repack.");
        Require(GetAt(container, 255) == states[256], "Global storage must preserve the final inserted state.");
    }

    private static void CheckChunkSection()
    {
        var smallContainerStrategy =
            PalettedContainerStrategy<BlockState>.CreateForBiomes(BuiltInRegistries.BlockStates);
        var sectionWithSuppliedStrategy = new ChunkSection(
            new PalettedContainer<BlockState>(Blocks.AIR.DefaultState, smallContainerStrategy));
        Require(
            ReferenceEquals(sectionWithSuppliedStrategy.GetBlockState(3, 3, 3), Blocks.AIR.DefaultState),
            "LevelChunkSection must accept the supplied block-state container without imposing a section-size validation.");

        var section = new ChunkSection(CreateBlockStateContainer(Blocks.AIR.DefaultState));
        Require(section.HasOnlyAir, "A new section must be empty.");
        Require(section.NonAirBlockCount == 0, "A new section must contain zero non-air blocks.");
        Require(section.PaletteMode == PaletteMode.SingleValue, "An empty section must start in single-value mode.");

        PalettedContainer<BlockState> prefilledStates = CreateBlockStateContainer(Blocks.AIR.DefaultState);
        prefilledStates.Set(1, 0, 0, Blocks.TEST_BLOCK.DefaultState);
        ChunkSection prefilledSection = new(prefilledStates);
        Require(
            prefilledSection.NonAirBlockCount == 1,
            "The LevelChunkSection container constructor must recalculate cached counts from the supplied state container.");
        BlockState xAxisState = Blocks.TEST_BLOCK.DefaultState;
        BlockState zAxisState = xAxisState.SetValue(Blocks.TEST_STAGE, 2);
        BlockState yAxisState = xAxisState.SetValue(Blocks.TEST_POWERED, true);
        section.SetBlockState(1, 0, 0, xAxisState);
        section.SetBlockState(0, 0, 1, zAxisState);
        section.SetBlockState(0, 1, 0, yAxisState);
        Require(ReferenceEquals(section.GetBlockState(1, 0, 0), xAxisState), "X contributes the least-significant section index bits.");
        Require(ReferenceEquals(section.GetBlockState(0, 0, 1), zAxisState), "Z contributes section index bits starting at bit four.");
        Require(ReferenceEquals(section.GetBlockState(0, 1, 0), yAxisState), "Y contributes section index bits starting at bit eight.");
        section.SetBlockState(1, 0, 0, Blocks.AIR.DefaultState);
        section.SetBlockState(0, 0, 1, Blocks.AIR.DefaultState);
        section.SetBlockState(0, 1, 0, Blocks.AIR.DefaultState);

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
        Require(section.MaybeHas(state => !state.IsAir), "maybeHas may conservatively return true for an unused local entry.");

        // LevelChunkSection exposes its state container and can rebuild its cached counters.
        section.GetStates().Set(0, 0, 0, placedState);
        Require(section.NonAirBlockCount == 0, "Direct container writes do not implicitly update section counters.");

        var staleCounterSection = new ChunkSection(CreateBlockStateContainer(Blocks.AIR.DefaultState));
        staleCounterSection.GetStates().Set(0, 0, 0, placedState);
        staleCounterSection.SetBlockState(0, 0, 0, Blocks.AIR.DefaultState);
        Require(
            staleCounterSection.NonAirBlockCount == -1 && !staleCounterSection.HasOnlyAir,
            "LevelChunkSection must preserve vanilla signed-short counter behavior when direct container writes leave the cached count stale.");

        section.RecalculateBlockCounts();
        Require(section.NonAirBlockCount == 1, "RecalculateBlockCounts must rebuild the non-air count from palette contents.");

        ChunkSection sectionCopy = section.Copy();
        Require(sectionCopy.NonAirBlockCount == 1, "A copied section must preserve its cached counters.");
        Require(ReferenceEquals(sectionCopy.GetBlockState(0, 0, 0), placedState), "A copied section must preserve block states.");
        sectionCopy.SetBlockState(0, 0, 0, Blocks.AIR.DefaultState);
        Require(sectionCopy.NonAirBlockCount == 0, "Mutating a copied section must update its own counters.");
        Require(section.NonAirBlockCount == 1, "Mutating a copied section must not affect the original.");

        var emptySection = new ChunkSection(CreateBlockStateContainer(Blocks.AIR.DefaultState));
        Require(!emptySection.MaybeHas(state => !state.IsAir), "An untouched air-only palette must rule out non-air blocks.");
        Require(
            ReferenceEquals(section.GetBlockState(16, 0, 0), section.GetBlockState(0, 0, 1)),
            "Coordinates are flattened in vanilla X/Z/Y order before the final storage index is validated.");
        RequireThrows<ArgumentOutOfRangeException>(() => section.GetBlockState(-1, 0, 0), "A negative flattened index must be rejected by packed storage.");
    }

    private static void CheckFrozenRegistry()
    {
        RegistryKey<Block> lateKey = new(
            BuiltInRegistries.Block.Location,
            ResourceLocation.Parse("asteria:late_block"));
        InvalidOperationException frozenRegistrationException = Capture<InvalidOperationException>(
            () => BuiltInRegistries.Block.Register(lateKey, new TestBlockDefinition(BlockProperties.Of()), RegistrationInfo.BuiltIn),
            "A frozen block registry must reject new entries.");
        Require(
            frozenRegistrationException.Message == $"Registry is already frozen (trying to add key {lateKey})",
            "MappedRegistry.validateWrite(key) must include the attempted key in its frozen-registry error.");
        InvalidOperationException intrusiveDisabledException = Capture<InvalidOperationException>(
            () => new Registry<Block>(ResourceLocation.Parse("asteria:no_intrusive_holders"))
                .CreateIntrusiveHolder(new TestBlockDefinition(BlockProperties.Of())),
            "A registry without intrusive holders must reject intrusive holder creation.");
        Require(
            intrusiveDisabledException.Message == "This registry can't create intrusive holders",
            "MappedRegistry.createIntrusiveHolder must use the vanilla disabled-registry error message.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static TException Capture<TException>(Action action, string message) where TException : Exception
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

    private static void RequireThrowsExact<TException>(Action action, string message) where TException : Exception
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception.GetType() == typeof(TException))
        {
            return;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(message, exception);
        }

        throw new InvalidOperationException(message);
    }

    private static TException RequireThrows<TException>(Action action, string message) where TException : Exception
    {
        try { action(); }
        catch (TException exception) { return exception; }
        throw new InvalidOperationException(message);
    }
}
