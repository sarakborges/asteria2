using System.Text.Json;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

internal static class HolderSetCodecChecks
{
    private sealed class TestEntry
    {
        public TestEntry(string id) => Id = id;
        public string Id { get; }
        public override string ToString() => Id;
    }

    public static void Run()
    {
        CheckDirectValuesUseCompactForm();
        CheckNamedTagsUseRegistryContext();
        CheckMissingRegistryAndTagsFail();
        CheckAlwaysUseList();
    }

    private static void CheckDirectValuesUseCompactForm()
    {
        (Registry<TestEntry> registry, Holder<TestEntry>.Reference first,
            Holder<TestEntry>.Reference second, RegistryOps<JsonElement> ops, _) = CreateFixture();

        Codec<HolderSet<TestEntry>> codec = RegistryCodecs.HolderSet<TestEntry>(registry.Key);
        HolderSet<TestEntry> single = HolderSet<TestEntry>.Direct(first);
        JsonElement singleEncoded = codec.EncodeStart(ops, single).GetOrThrow();

        Require(singleEncoded.ValueKind == JsonValueKind.String &&
                singleEncoded.GetString() == "asteria:first",
            "A one-holder direct set must use the compact single-element representation.");

        HolderSet<TestEntry> singleDecoded = codec.Parse(ops, singleEncoded).GetOrThrow();
        Require(singleDecoded.Count == 1 && ReferenceEquals(singleDecoded[0], first),
            "A compact single direct holder must decode to the registered holder.");

        HolderSet<TestEntry> multiple = HolderSet<TestEntry>.Direct(first, second);
        JsonElement multipleEncoded = codec.EncodeStart(ops, multiple).GetOrThrow();
        Require(multipleEncoded.ValueKind == JsonValueKind.Array &&
                multipleEncoded.GetArrayLength() == 2,
            "A direct set with multiple holders must use the list representation.");

        HolderSet<TestEntry> multipleDecoded = codec.Parse(ops, multipleEncoded).GetOrThrow();
        Require(multipleDecoded.Count == 2 &&
                ReferenceEquals(multipleDecoded[0], first) &&
                ReferenceEquals(multipleDecoded[1], second),
            "A direct holder list must preserve holder identity and declaration order.");

        JsonElement emptyEncoded = codec.EncodeStart(ops, HolderSet<TestEntry>.Empty).GetOrThrow();
        Require(emptyEncoded.ValueKind == JsonValueKind.Array &&
                emptyEncoded.GetArrayLength() == 0,
            "An empty direct set must encode as an empty list.");
    }

    private static void CheckNamedTagsUseRegistryContext()
    {
        (Registry<TestEntry> registry, _, _, RegistryOps<JsonElement> ops, TagKey<TestEntry> tag) =
            CreateFixture();
        Codec<HolderSet<TestEntry>> codec = RegistryCodecs.HolderSet<TestEntry>(registry.Key);
        HolderSet<TestEntry>.Named named = registry.GetTag(tag)!;

        JsonElement encoded = codec.EncodeStart(ops, named).GetOrThrow();
        Require(encoded.ValueKind == JsonValueKind.String &&
                encoded.GetString() == "#asteria:group",
            "A named holder set must encode its hashed tag key when registry context is present.");

        HolderSet<TestEntry> decoded = codec.Parse(ops, encoded).GetOrThrow();
        Require(ReferenceEquals(decoded, named),
            "A named holder set must resolve to the registry's canonical tag object.");

        HolderSet<TestEntry>.Named unboundOwner = CreateForeignNamedSet(registry, tag);
        Require(codec.EncodeStart(ops, unboundOwner).IsError,
            "A named holder set from another registry owner must not serialize in the current registry context.");
    }

    private static void CheckMissingRegistryAndTagsFail()
    {
        (Registry<TestEntry> registry, _, _, RegistryOps<JsonElement> ops, _) = CreateFixture();
        Codec<HolderSet<TestEntry>> codec = RegistryCodecs.HolderSet<TestEntry>(registry.Key);

        Require(codec.Parse(JsonOps.INSTANCE, JsonOps.INSTANCE.CreateString("#asteria:group")).ErrorMessage ==
                "Registries are not available in this context",
            "A hashed tag key must require registry context.");

        Require(codec.Parse(ops, ops.CreateString("#asteria:missing")).IsError,
            "An unknown tag key must fail instead of producing an empty holder set.");

        Require(
            ReferenceEquals(
                codec.Parse(ops, ops.CreateString("asteria:first")).GetOrThrow()[0],
                registry.GetValue(ResourceLocation.Parse("asteria:first")) is TestEntry value
                    ? registry.GetHolder(value)
                    : null),
            "A compact direct holder reference must resolve to its registered holder.");
    }

    private static void CheckAlwaysUseList()
    {
        (Registry<TestEntry> registry, Holder<TestEntry>.Reference first, _, RegistryOps<JsonElement> ops, _) =
            CreateFixture();
        Codec<HolderSet<TestEntry>> codec = RegistryCodecs.HolderSet<TestEntry>(registry.Key, alwaysUseList: true);

        JsonElement encoded = codec.EncodeStart(ops, HolderSet<TestEntry>.Direct(first)).GetOrThrow();
        Require(encoded.ValueKind == JsonValueKind.Array && encoded.GetArrayLength() == 1,
            "The alwaysUseList option must preserve list encoding for a single holder.");
    }

    private static HolderSet<TestEntry>.Named CreateForeignNamedSet(
        Registry<TestEntry> registry,
        TagKey<TestEntry> tag)
    {
        var foreign = new Registry<TestEntry>(registry.Location);
        var value = new TestEntry("foreign");
        RegistryKey<TestEntry> key = RegistryKey<TestEntry>.Create(
            foreign.Key,
            ResourceLocation.Parse("asteria:foreign"));
        Holder<TestEntry>.Reference holder = foreign.Register(key, value, RegistrationInfo.BuiltIn);
        foreign.BindTags(new[]
        {
            new KeyValuePair<TagKey<TestEntry>, IEnumerable<Holder<TestEntry>>>(
                tag, new Holder<TestEntry>[] { holder })
        });
        foreign.Freeze();
        return foreign.GetTag(tag)!;
    }

    private static (
        Registry<TestEntry> Registry,
        Holder<TestEntry>.Reference First,
        Holder<TestEntry>.Reference Second,
        RegistryOps<JsonElement> Ops,
        TagKey<TestEntry> Tag) CreateFixture()
    {
        var registry = new Registry<TestEntry>(ResourceLocation.Parse("asteria:holder_set_codec_test"));
        TestEntry firstValue = new("first");
        TestEntry secondValue = new("second");
        Holder<TestEntry>.Reference first = registry.Register(
            RegistryKey<TestEntry>.Create(registry.Key, ResourceLocation.Parse("asteria:first")),
            firstValue,
            RegistrationInfo.BuiltIn);
        Holder<TestEntry>.Reference second = registry.Register(
            RegistryKey<TestEntry>.Create(registry.Key, ResourceLocation.Parse("asteria:second")),
            secondValue,
            RegistrationInfo.BuiltIn);

        TagKey<TestEntry> tag = TagKey<TestEntry>.Create(
            registry.Key,
            ResourceLocation.Parse("asteria:group"));
        registry.BindTags(new[]
        {
            new KeyValuePair<TagKey<TestEntry>, IEnumerable<Holder<TestEntry>>>(
                tag, new Holder<TestEntry>[] { first, second })
        });
        registry.Freeze();

        HolderLookupProvider lookup = HolderLookupProvider.Create(new IRegistryLookup[] { registry });
        RegistryOps<JsonElement> ops = RegistryOps<JsonElement>.Create(JsonOps.INSTANCE, lookup);
        return (registry, first, second, ops, tag);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
