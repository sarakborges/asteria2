using System.Text.Json;
using Asteria.Content.Biomes;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.LevelGen.Structure;
using Asteria.World.LevelGen.Structure.Placement;

internal static class StructureCodecChecks
{
    private static readonly ResourceLocation TestTypeLocation =
        ResourceLocation.FromNamespaceAndPath("asteria", "test_structure");

    private static readonly MapCodec<TestStructure> TypedTestCodec =
        Structure.SimpleCodec<TestStructure>(static settings => new TestStructure(settings));

    private static readonly MapCodec<Structure> AdaptedTestCodec =
        Structure.AdaptCodec(TypedTestCodec);

    private static Holder<Structure>.Reference? _registeredStructure;

    public static void RegisterTestContent()
    {
        RegistryKey<MapCodec<Structure>> typeKey =
            RegistryKey<MapCodec<Structure>>.Create(
                BuiltInRegistries.StructureType.Key,
                TestTypeLocation);
        BuiltInRegistries.StructureType.Register(
            typeKey,
            AdaptedTestCodec,
            RegistrationInfo.BuiltIn);

        RegistryKey<Structure> structureKey =
            RegistryKey<Structure>.Create(
                BuiltInRegistries.Structure.Key,
                TestTypeLocation);
        _registeredStructure = BuiltInRegistries.Structure.Register(
            structureKey,
            new TestStructure(StructureSettings.DEFAULT),
            RegistrationInfo.BuiltIn);
    }

    public static void Run()
    {
        CheckStructureTypeRegistryAndDirectCodec();
        CheckStructureHolderCodec();
        CheckStructureSetDirectAndHolderCodecs();
        CheckSelectionEntryWeightValidation();
    }

    private static void CheckStructureTypeRegistryAndDirectCodec()
    {
        Require(
            BuiltInRegistries.StructureType.Count == 1 &&
            BuiltInRegistries.Structure.Count == 1,
            "The harness must register only its own structure type and structure instance.");

        Structure structure = RegisteredStructure.Value;
        RegistryKey<MapCodec<Structure>>? registeredCodecKey =
            BuiltInRegistries.StructureType.GetResourceKey(structure.Codec());

        Require(
            registeredCodecKey.HasValue &&
            registeredCodecKey.Value.Location.Equals(TestTypeLocation),
            "A structure instance must resolve its exact registered subtype codec.");

        RegistryOps<JsonElement> ops = CreateRegistryOps();
        JsonElement encoded = Structure.DIRECT_CODEC.EncodeStart(ops, structure).GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:test_structure" &&
            ops.Get(encoded, "biomes").GetOrThrow().ValueKind == JsonValueKind.Array &&
            ops.Get(encoded, "step").GetOrThrow().GetString() == "surface_structures",
            "Structure.DIRECT_CODEC must dispatch to the subtype map codec and preserve common settings fields.");

        Structure decoded = Structure.DIRECT_CODEC.Parse(ops, encoded).GetOrThrow();
        Require(
            decoded is TestStructure &&
            ReferenceEquals(decoded.Codec(), AdaptedTestCodec) &&
            ReferenceEquals(decoded.TerrainAdaptation(), TerrainAdjustment.NONE),
            "Structure.DIRECT_CODEC must reconstruct the registered subtype with the same codec singleton.");

        JsonElement unknownType = ParseJson(
            """{"type":"minecraft:concentric_rings"}""");
        Require(
            Structure.DIRECT_CODEC.Parse(ops, unknownType).IsError,
            "Structure.DIRECT_CODEC must reject structure types with no registered codec.");

        JsonElement malformedType = ParseJson(
            """{"type":"not a namespaced id"}""");
        Require(
            Structure.DIRECT_CODEC.Parse(ops, malformedType).IsError,
            "Structure.DIRECT_CODEC must reject malformed structure type identifiers.");

        Require(
            Structure.DIRECT_CODEC.EncodeStart(ops, new UnsupportedStructure(
                StructureSettings.DEFAULT)).IsError,
            "A Structure subclass without a registered type codec must fail encoding explicitly.");
    }

    private static void CheckStructureHolderCodec()
    {
        RegistryOps<JsonElement> ops = CreateRegistryOps();
        Holder<Structure>.Reference holder = RegisteredStructure;

        JsonElement encoded = Structure.CODEC.EncodeStart(ops, holder).GetOrThrow();
        Require(
            encoded.ValueKind == JsonValueKind.String &&
            encoded.GetString() == "asteria:test_structure",
            "Structure.CODEC must encode a registered holder as its resource location.");

        Holder<Structure> decoded = Structure.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(
            decoded is Holder<Structure>.Reference reference &&
            reference.Key.Location.Equals(TestTypeLocation) &&
            ReferenceEquals(reference.Value, holder.Value),
            "Structure.CODEC must resolve the registered structure reference through RegistryOps.");

        JsonElement missingReference = JsonOps.INSTANCE.CreateString("asteria:missing_structure");
        Require(
            Structure.CODEC.Parse(ops, missingReference).IsError,
            "Structure.CODEC must reject a structure reference absent from the active registry lookup.");
    }

    private static void CheckStructureSetDirectAndHolderCodecs()
    {
        RegistryOps<JsonElement> ops = CreateRegistryOps();
        Holder<Structure> structureHolder = RegisteredStructure;

        var expected = new StructureSet(
            new[] { StructureSet.Entry(structureHolder, 3) },
            DimensionOriginStructurePlacement.INSTANCE);

        JsonElement encodedDirect = StructureSet.DIRECT_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        JsonElement encodedEntry = ops.Get(encodedDirect, "structures").GetOrThrow()[0];
        Require(
            ops.Get(encodedEntry, "structure").GetOrThrow().GetString() == "asteria:test_structure" &&
            ops.Get(encodedEntry, "weight").GetOrThrow().GetInt32() == 3 &&
            ops.Get(ops.Get(encodedDirect, "placement").GetOrThrow(), "type").GetOrThrow().GetString()
                == "minecraft:dimension_origin",
            "StructureSet.DIRECT_CODEC must encode weighted structure holders and the placement codec under their vanilla field names.");

        StructureSet decodedDirect = StructureSet.DIRECT_CODEC
            .Parse(ops, encodedDirect)
            .GetOrThrow();

        Require(
            decodedDirect.Structures.Count == 1 &&
            ReferenceEquals(decodedDirect.Structures[0].Structure.Value, structureHolder.Value) &&
            decodedDirect.Structures[0].Weight == 3 &&
            ReferenceEquals(decodedDirect.Placement, DimensionOriginStructurePlacement.INSTANCE),
            "StructureSet.DIRECT_CODEC must restore structure selection entries and the placement singleton.");

        JsonElement encodedHolder = StructureSet.CODEC
            .EncodeStart(ops, Holder<StructureSet>.Direct(expected))
            .GetOrThrow();

        Require(
            ops.Get(encodedHolder, "structures").GetOrThrow().GetArrayLength() == 1,
            "StructureSet.CODEC must retain inline definitions for direct top-level holders.");

        Holder<StructureSet> decodedHolder = StructureSet.CODEC
            .Parse(ops, encodedHolder)
            .GetOrThrow();

        Require(
            decodedHolder.Kind == HolderKind.Direct,
            "An inline structure-set definition must decode as a direct holder.");
    }

    private static void CheckSelectionEntryWeightValidation()
    {
        RegistryOps<JsonElement> ops = CreateRegistryOps();
        JsonElement invalidWeight = ParseJson(
            """{"structures":[{"structure":"asteria:test_structure","weight":0}],"placement":{"type":"minecraft:dimension_origin"}}""");

        Require(
            StructureSet.DIRECT_CODEC.Parse(ops, invalidWeight).IsError,
            "Structure selection entries must reject non-positive weights.");

        JsonElement missingWeight = ParseJson(
            """{"structures":[{"structure":"asteria:test_structure"}],"placement":{"type":"minecraft:dimension_origin"}}""");
        Require(
            StructureSet.DIRECT_CODEC.Parse(ops, missingWeight).IsError,
            "Structure selection entry weight is a required field.");

        JsonElement missingPlacement = ParseJson(
            """{"structures":[]}""");
        Require(
            StructureSet.DIRECT_CODEC.Parse(ops, missingPlacement).IsError,
            "StructureSet placement is a required field.");

        JsonElement missingStructures = ParseJson(
            """{"placement":{"type":"minecraft:dimension_origin"}}""");
        Require(
            StructureSet.DIRECT_CODEC.Parse(ops, missingStructures).IsError,
            "StructureSet structures is a required field.");
    }

    private static RegistryOps<JsonElement> CreateRegistryOps()
    {
        HolderLookupProvider lookup = HolderLookupProvider.Create(
            new IRegistryLookup[]
            {
                BuiltInRegistries.Biome,
                BuiltInRegistries.Structure,
                BuiltInRegistries.StructureSet
            });

        return RegistryOps<JsonElement>.Create(JsonOps.INSTANCE, lookup);
    }

    private static Holder<Structure>.Reference RegisteredStructure =>
        _registeredStructure ??
        throw new InvalidOperationException("StructureCodecChecks.RegisterTestContent must run before registry bootstrap.");

    private sealed class TestStructure : Structure
    {
        public TestStructure(StructureSettings settings) : base(settings)
        {
        }

        protected override Structure.GenerationStub? FindGenerationPoint(
            Structure.GenerationContext context) => null;

        public override MapCodec<Structure> Codec() =>
            AdaptedTestCodec;
    }

    private sealed class UnsupportedStructure : Structure
    {
        public UnsupportedStructure(StructureSettings settings) : base(settings)
        {
        }

        protected override Structure.GenerationStub? FindGenerationPoint(
            Structure.GenerationContext context) => null;
    }

    private static JsonElement ParseJson(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
