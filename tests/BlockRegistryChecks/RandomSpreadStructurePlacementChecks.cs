using System.Text.Json;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Structure;
using Asteria.World.LevelGen.Structure.Placement;

internal static class RandomSpreadStructurePlacementChecks
{
    public static void Run()
    {
        CheckPotentialChunkMatchesTheReferenceFormula();
        CheckNegativeCoordinatesUseFloorDivision();
        CheckCandidateIsStableForSeedAndCell();
        CheckInvalidParametersFailAtUseTime();
        CheckCodecFieldsDefaultsAndValidation();
        CheckExclusionZoneRegistryCodec();
    }

    private static void CheckPotentialChunkMatchesTheReferenceFormula()
    {
        const long seed = 0x4D43535452554354L;
        const int spacing = 37;
        const int separation = 9;
        const int salt = 10387320;
        const int sourceX = 147;
        const int sourceZ = -206;

        var placement = new RandomSpreadStructurePlacement(
            spacing,
            separation,
            RandomSpreadType.TRIANGULAR,
            salt);

        ChunkPos expected = CalculateReference(
            seed,
            sourceX,
            sourceZ,
            spacing,
            separation,
            RandomSpreadType.TRIANGULAR,
            salt);
        ChunkPos actual = placement.GetPotentialStructureChunk(
            seed,
            sourceX,
            sourceZ);

        Require(
            actual == expected,
            "The candidate chunk must match the vanilla floor-grid, large-feature seed and spread evaluation formula.");
    }

    private static void CheckNegativeCoordinatesUseFloorDivision()
    {
        const int spacing = 32;
        const int separation = 8;
        const long seed = 918273645L;

        var placement = new RandomSpreadStructurePlacement(
            spacing,
            separation,
            RandomSpreadType.LINEAR,
            17);

        ChunkPos actual = placement.GetPotentialStructureChunk(seed, -1, -33);
        ChunkPos expected = CalculateReference(
            seed,
            -1,
            -33,
            spacing,
            separation,
            RandomSpreadType.LINEAR,
            17);

        Require(
            actual == expected &&
            actual.X >= -spacing &&
            actual.X < 0 &&
            actual.Z >= -2 * spacing &&
            actual.Z < -spacing,
            "Negative source coordinates must use floor division and remain in their containing spacing cells.");
    }

    private static void CheckCandidateIsStableForSeedAndCell()
    {
        var placement = new RandomSpreadStructurePlacement(
            28,
            5,
            RandomSpreadType.TRIANGULAR,
            10387320);

        ChunkPos first = placement.GetPotentialStructureChunk(123456789L, 65, 97);
        ChunkPos repeated = placement.GetPotentialStructureChunk(123456789L, 65, 97);
        ChunkPos sameCell = placement.GetPotentialStructureChunk(123456789L, 79, 110);
        ChunkPos otherSeed = placement.GetPotentialStructureChunk(123456790L, 65, 97);

        Require(
            first == repeated,
            "A fixed seed and spacing-grid cell must always produce the same candidate.");
        Require(
            first == sameCell,
            "Every source coordinate inside one spacing-grid cell must resolve to that cell's same candidate.");
        Require(
            first != otherSeed,
            "Changing the level seed must change the deterministic candidate for this regression fixture.");
    }

    private static void CheckInvalidParametersFailAtUseTime()
    {
        var zeroSpacing = new RandomSpreadStructurePlacement(
            0,
            0,
            RandomSpreadType.LINEAR,
            0);

        RequireThrows<DivideByZeroException>(
            () => zeroSpacing.GetPotentialStructureChunk(0L, 1, 1),
            "The direct constructor must not silently normalize zero spacing before floor division.");

        var zeroSpreadRange = new RandomSpreadStructurePlacement(
            8,
            8,
            RandomSpreadType.LINEAR,
            0);

        RequireThrows<ArgumentException>(
            () => zeroSpreadRange.GetPotentialStructureChunk(0L, 1, 1),
            "The direct constructor must preserve the random source's failure when spacing minus separation is zero.");
    }

    private static void CheckCodecFieldsDefaultsAndValidation()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var expected = new RandomSpreadStructurePlacement(
            new Vec3i(4, -2, 7),
            FrequencyReductionMethod.LEGACY_TYPE_2,
            0.375F,
            10387320,
            null,
            40,
            9,
            RandomSpreadType.TRIANGULAR);

        JsonElement encoded = RandomSpreadStructurePlacement.MAP_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "locate_offset").GetOrThrow().GetArrayLength() == 3 &&
            ops.Get(encoded, "locate_offset").GetOrThrow()[0].GetInt32() == 4 &&
            ops.Get(encoded, "locate_offset").GetOrThrow()[1].GetInt32() == -2 &&
            ops.Get(encoded, "locate_offset").GetOrThrow()[2].GetInt32() == 7 &&
            ops.Get(encoded, "frequency_reduction_method").GetOrThrow().GetString() == "legacy_type_2" &&
            ops.Get(encoded, "frequency").GetOrThrow().GetSingle() == 0.375F &&
            ops.Get(encoded, "salt").GetOrThrow().GetInt32() == 10387320 &&
            ops.Get(encoded, "spacing").GetOrThrow().GetInt32() == 40 &&
            ops.Get(encoded, "separation").GetOrThrow().GetInt32() == 9 &&
            ops.Get(encoded, "spread_type").GetOrThrow().GetString() == "triangular",
            "RandomSpreadStructurePlacement codec must preserve every shared and subtype field under its vanilla field name.");

        RandomSpreadStructurePlacement decoded =
            RandomSpreadStructurePlacement.MAP_CODEC.Parse(ops, encoded).GetOrThrow();

        Require(
            decoded.Spacing() == expected.Spacing() &&
            decoded.Separation() == expected.Separation() &&
            ReferenceEquals(decoded.SpreadType(), expected.SpreadType()) &&
            decoded.LocateOffset().Equals(expected.LocateOffset()) &&
            decoded.GetLocatePos(new ChunkPos(2, -3)).GetX() == 36 &&
            decoded.GetLocatePos(new ChunkPos(2, -3)).GetY() == -2 &&
            decoded.GetLocatePos(new ChunkPos(2, -3)).GetZ() == -41,
            "RandomSpreadStructurePlacement codec must restore spacing, separation, spread type and locate offset.");

        JsonElement defaultedInput = ParseJson(
            """{"spacing":32,"separation":8,"salt":10387320,"frequency":0.25}""");
        RandomSpreadStructurePlacement defaulted =
            RandomSpreadStructurePlacement.MAP_CODEC.Parse(ops, defaultedInput).GetOrThrow();

        Require(
            ReferenceEquals(defaulted.SpreadType(), RandomSpreadType.LINEAR) &&
            defaulted.LocateOffset().Equals(Vec3i.Zero) &&
            !defaulted.ApplyAdditionalChunkRestrictions(-7, 19, 123456789L),
            "Missing spread type, locate offset and frequency-reduction method must use their vanilla defaults.");

        JsonElement omittedFrequencyInput = ParseJson(
            """{"spacing":32,"separation":8,"salt":1}""");
        RandomSpreadStructurePlacement omittedFrequency =
            RandomSpreadStructurePlacement.MAP_CODEC.Parse(ops, omittedFrequencyInput).GetOrThrow();
        Require(
            omittedFrequency.ApplyAdditionalChunkRestrictions(0, 0, 0L),
            "Missing frequency must default to 1.0 and skip probability reduction.");

        Require(
            RandomSpreadStructurePlacement.MAP_CODEC.Parse(
                ops,
                ParseJson("""{"spacing":32,"separation":32,"salt":1}""")).IsError,
            "A random spread placement must reject spacing that is not larger than separation.");
        Require(
            RandomSpreadStructurePlacement.MAP_CODEC.Parse(
                ops,
                ParseJson("""{"spacing":4097,"separation":8,"salt":1}""")).IsError &&
            RandomSpreadStructurePlacement.MAP_CODEC.Parse(
                ops,
                ParseJson("""{"spacing":32,"separation":-1,"salt":1}""")).IsError,
            "Spacing and separation must each remain inside the inclusive 0..4096 codec range.");
        Require(
            RandomSpreadStructurePlacement.MAP_CODEC.Parse(
                ops,
                ParseJson("""{"spacing":32,"separation":8}""")).IsError,
            "Salt is a required field in the random spread placement codec.");
        Require(
            RandomSpreadStructurePlacement.MAP_CODEC.Parse(
                ops,
                ParseJson("""{"spacing":32,"separation":8,"salt":-1}""")).IsError,
            "Random spread placement must reject negative salts.");
        Require(
            RandomSpreadStructurePlacement.MAP_CODEC.Parse(
                ops,
                ParseJson("""{"spacing":32,"separation":8,"salt":1,"frequency":1.01}""")).IsError,
            "Frequency must remain inside the inclusive 0..1 codec range.");
        Require(
            RandomSpreadStructurePlacement.MAP_CODEC.Parse(
                ops,
                ParseJson("""{"spacing":32,"separation":8,"salt":1,"locate_offset":[17,0,0]}""")).IsError,
            "Locate offset components must remain inside the inclusive -16..16 range.");

        RandomSpreadStructurePlacement overflowOffset =
            RandomSpreadStructurePlacement.MAP_CODEC.Parse(
                ops,
                ParseJson("""{"spacing":32,"separation":8,"salt":1,"locate_offset":[-2147483648,0,0]}"""))
                .GetOrThrow();
        Require(
            overflowOffset.LocateOffset().GetX() == int.MinValue,
            "Vec3i.offsetCodec must preserve Java Math.abs(Integer.MIN_VALUE) overflow during the range check.");
        Require(
            RandomSpreadStructurePlacement.MAP_CODEC.Parse(
                ops,
                ParseJson("""{"spacing":32,"separation":8,"salt":1,"spread_type":"unknown"}""")).IsError,
            "Unknown random spread distributions must be rejected by their codec.");
    }

    private static void CheckExclusionZoneRegistryCodec()
    {
        var structure = new TestStructure();
        var set = new StructureSet(
            Holder<Structure>.Direct(structure),
            DimensionOriginStructurePlacement.INSTANCE);

        var structureSets = new Registry<StructureSet>(
            Registries.STRUCTURE_SET.Location);
        RegistryKey<StructureSet> setKey = RegistryKey<StructureSet>.Create(
            structureSets.Key,
            ResourceLocation.FromNamespaceAndPath("asteria", "test_structure_set"));
        Holder<StructureSet>.Reference registeredSet = structureSets.Register(
            setKey,
            set,
            RegistrationInfo.BuiltIn);
        structureSets.Freeze();

        HolderLookupProvider lookup = HolderLookupProvider.Create(
            new IRegistryLookup[] { structureSets });
        RegistryOps<JsonElement> ops = RegistryOps<JsonElement>.Create(
            JsonOps.INSTANCE,
            lookup);

        var expected = new AbstractSpreadingStructurePlacement.ExclusionZone(
            registeredSet,
            5);

        JsonElement encoded = AbstractSpreadingStructurePlacement.ExclusionZone.CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "other_set").GetOrThrow().GetString() ==
                "asteria:test_structure_set" &&
            ops.Get(encoded, "chunk_count").GetOrThrow().GetInt32() == 5,
            "Exclusion zones must serialize the registered structure-set key and chunk count, not inline structure-set data.");

        AbstractSpreadingStructurePlacement.ExclusionZone decoded =
            AbstractSpreadingStructurePlacement.ExclusionZone.CODEC
                .Parse(ops, encoded)
                .GetOrThrow();

        Require(
            ReferenceEquals(decoded.OtherSet.Value, set) &&
            decoded.ChunkCount == 5,
            "Exclusion-zone decoding must resolve the registered structure-set holder and preserve its range.");

        JsonElement invalidCount = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("other_set"),
                ops.CreateString("asteria:test_structure_set")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("chunk_count"),
                JsonOps.INSTANCE.CreateInt(0))
        });
        Require(
            AbstractSpreadingStructurePlacement.ExclusionZone.CODEC
                .Parse(ops, invalidCount).IsError,
            "Exclusion-zone chunk_count must reject values below one.");

        JsonElement inlineSet = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("other_set"),
                ops.CreateMap(Array.Empty<KeyValuePair<JsonElement, JsonElement>>())),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("chunk_count"),
                JsonOps.INSTANCE.CreateInt(5))
        });
        Require(
            AbstractSpreadingStructurePlacement.ExclusionZone.CODEC
                .Parse(ops, inlineSet).IsError,
            "Exclusion-zone other_set must be a registered holder reference, not an inline set.");
    }

    private sealed class TestStructure : Structure
    {
        public TestStructure() : base(StructureSettings.DEFAULT)
        {
        } 
        protected override Structure.GenerationStub? FindGenerationPoint(
            Structure.GenerationContext context) => null;
    }

    private static JsonElement ParseJson(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static ChunkPos CalculateReference(
        long seed,
        int sourceX,
        int sourceZ,
        int spacing,
        int separation,
        RandomSpreadType spreadType,
        int salt)
    {
        int gridX = Mth.FloorDiv(sourceX, spacing);
        int gridZ = Mth.FloorDiv(sourceZ, spacing);
        var random = new WorldgenRandom(new LegacyRandomSource(0L));
        random.SetLargeFeatureWithSalt(seed, gridX, gridZ, salt);

        int limit = spacing - separation;
        int spreadX = spreadType.Evaluate(random, limit);
        int spreadZ = spreadType.Evaluate(random, limit);

        return new ChunkPos(
            unchecked(gridX * spacing + spreadX),
            unchecked(gridZ * spacing + spreadZ));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static TException RequireThrows<TException>(Action action, string message)
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
