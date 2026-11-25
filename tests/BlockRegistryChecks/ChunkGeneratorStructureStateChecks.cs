using Asteria.Content.Biomes;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Level;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Structure;
using Asteria.World.LevelGen.Structure.Placement;

internal static class ChunkGeneratorStructureStateChecks
{
    public static void Run()
    {
        CheckBiomeFilteringAndPlacementIndex();
        CheckFlatAndNormalStateSeeds();
        CheckExclusionRangeScanOrderAndShortCircuit();
    }

    private static void CheckBiomeFilteringAndPlacementIndex()
    {
        var world = new OverworldWorld(0x5354525543545552L);
        RandomState randomState = world.Generator.RandomState;
        Holder<Biome> plains = Biomes.PLAINS.BuiltInRegistryHolder;
        Holder<Biome> desert = Biomes.DESERT.BuiltInRegistryHolder;
        MultiNoiseBiomeSource biomeSource = CreateBiomeSource(plains);

        var plainsStructure = new TestStructure(HolderSet<Biome>.Direct(plains));
        var desertStructure = new TestStructure(HolderSet<Biome>.Direct(desert));
        Holder<Structure> plainsHolder = Holder<Structure>.Direct(plainsStructure);
        Holder<Structure> desertHolder = Holder<Structure>.Direct(desertStructure);

        var firstPlacement = new RecordingPlacement();
        var secondPlacement = new RecordingPlacement();
        var filteredPlacement = new RecordingPlacement();

        var mixedSet = new StructureSet(
            new[]
            {
                StructureSet.Entry(plainsHolder, 3),
                StructureSet.Entry(desertHolder, 1)
            },
            firstPlacement);
        var plainsSet = new StructureSet(plainsHolder, secondPlacement);
        var desertSet = new StructureSet(desertHolder, filteredPlacement);

        Holder<StructureSet> mixedSetHolder = Holder<StructureSet>.Direct(mixedSet);
        Holder<StructureSet> plainsSetHolder = Holder<StructureSet>.Direct(plainsSet);
        Holder<StructureSet> desertSetHolder = Holder<StructureSet>.Direct(desertSet);

        ChunkGeneratorStructureState state = ChunkGeneratorStructureState.CreateForNormal(
            randomState,
            levelSeed: 9918273L,
            origin: new ChunkPos(-4, 7),
            biomeSource,
            new[] { mixedSetHolder, plainsSetHolder, desertSetHolder });

        Require(
            state.PossibleStructureSets().Count == 2 &&
            ReferenceEquals(state.PossibleStructureSets()[0], mixedSetHolder) &&
            ReferenceEquals(state.PossibleStructureSets()[1], plainsSetHolder),
            "Normal structure state must retain only sets with at least one structure supported by a possible biome, preserving input order.");

        Require(
            state.LevelSeed == 9918273L &&
            state.GetLevelSeed() == 9918273L &&
            state.GetDimensionOrigin() == new ChunkPos(-4, 7) &&
            ReferenceEquals(state.RandomState(), randomState),
            "Structure state must preserve its seed, dimension origin and RandomState.");

        IReadOnlyList<StructurePlacement> plainsPlacements =
            state.GetPlacementsForStructure(plainsHolder);
        Require(
            plainsPlacements.Count == 2 &&
            ReferenceEquals(plainsPlacements[0], firstPlacement) &&
            ReferenceEquals(plainsPlacements[1], secondPlacement),
            "The lazy placement index must append every eligible set placement in source order.");

        Require(
            state.GetPlacementsForStructure(desertHolder).Count == 0,
            "The placement index must omit structures whose biome sets do not intersect the source's possible biomes.");

        Require(
            state.GetPlacementsForStructure(Holder<Structure>.Direct(
                new TestStructure(HolderSet<Biome>.Direct(plains)))).Count == 0,
            "The placement index must return an empty result for a structure instance absent from all possible sets.");
    }

    private static void CheckFlatAndNormalStateSeeds()
    {
        var world = new OverworldWorld(0x464C415453544154L);
        MultiNoiseBiomeSource biomeSource = CreateBiomeSource(
            Biomes.PLAINS.BuiltInRegistryHolder);
        RandomState randomState = world.Generator.RandomState;

        var structure = new TestStructure(
            HolderSet<Biome>.Direct(Biomes.PLAINS.BuiltInRegistryHolder));
        var set = new StructureSet(
            Holder<Structure>.Direct(structure),
            new RecordingPlacement());
        Holder<StructureSet> setHolder = Holder<StructureSet>.Direct(set);

        ChunkGeneratorStructureState normal = ChunkGeneratorStructureState.CreateForNormal(
            randomState,
            456789L,
            ChunkPos.Zero,
            biomeSource,
            new[] { setHolder });
        ChunkGeneratorStructureState flat = ChunkGeneratorStructureState.CreateForFlat(
            randomState,
            456789L,
            ChunkPos.Zero,
            biomeSource,
            new[] { setHolder });

        Require(
            normal.PossibleStructureSets().Count == 1 &&
            flat.PossibleStructureSets().Count == 1,
            "Normal and flat state factories must preserve biome-compatible structure overrides.");
        Require(
            normal.LevelSeed == flat.LevelSeed,
            "Flat and normal factories must both retain the supplied level seed.");
    }

    private static void CheckExclusionRangeScanOrderAndShortCircuit()
    {
        var world = new OverworldWorld(0x52414E4745534341L);
        MultiNoiseBiomeSource biomeSource = CreateBiomeSource(
            Biomes.PLAINS.BuiltInRegistryHolder);
        var structure = new TestStructure(
            HolderSet<Biome>.Direct(Biomes.PLAINS.BuiltInRegistryHolder));
        Holder<Structure> structureHolder = Holder<Structure>.Direct(structure);

        var fullScanPlacement = new RecordingPlacement();
        var fullScanSet = new StructureSet(structureHolder, fullScanPlacement);
        Holder<StructureSet> fullScanSetHolder =
            Holder<StructureSet>.Direct(fullScanSet);
        var fullScanState = ChunkGeneratorStructureState.CreateForNormal(
            world.Generator.RandomState,
            12L,
            ChunkPos.Zero,
            biomeSource,
            new[] { fullScanSetHolder });

        Require(
            !fullScanState.HasStructureChunkInRange(
                fullScanSetHolder,
                sourceX: 5,
                sourceZ: 9,
                chunkCount: 1),
            "An exclusion scan must return false when no candidate placement matches.");
        Require(
            fullScanPlacement.Queries.SequenceEqual(new[]
            {
                (4, 8), (4, 9), (4, 10),
                (5, 8), (5, 9), (5, 10),
                (6, 8), (6, 9), (6, 10)
            }),
            "The exclusion scan must visit the inclusive square in X-outer, Z-inner order.");

        var shortCircuitPlacement = new RecordingPlacement(
            acceptX: 5,
            acceptZ: 9);
        var shortCircuitSet = new StructureSet(structureHolder, shortCircuitPlacement);
        Holder<StructureSet> shortCircuitSetHolder =
            Holder<StructureSet>.Direct(shortCircuitSet);
        var shortCircuitState = ChunkGeneratorStructureState.CreateForNormal(
            world.Generator.RandomState,
            12L,
            ChunkPos.Zero,
            biomeSource,
            new[] { shortCircuitSetHolder });

        Require(
            shortCircuitState.HasStructureChunkInRange(
                shortCircuitSetHolder,
                sourceX: 5,
                sourceZ: 9,
                chunkCount: 1),
            "The exclusion scan must return true when a candidate structure chunk is found.");
        Require(
            shortCircuitPlacement.Queries.SequenceEqual(new[]
            {
                (4, 8), (4, 9), (4, 10), (5, 8), (5, 9)
            }),
            "The exclusion scan must stop immediately when the placement accepts a queried chunk.");
    }

    private static MultiNoiseBiomeSource CreateBiomeSource(
        params Holder<Biome>[] biomes)
    {
        var values = biomes.Select((biome, index) =>
            new Pair<Climate.ParameterPoint, Holder<Biome>>(
                Climate.Parameters(
                    temperature: index * 0.25F,
                    humidity: 0.0F,
                    continentalness: 0.0F,
                    erosion: 0.0F,
                    depth: 0.0F,
                    weirdness: 0.0F,
                    offset: 0.0F),
                biome)).ToArray();

        return new MultiNoiseBiomeSource(
            new Climate.ParameterList<Holder<Biome>>(values));
    }

    private sealed class TestStructure : Structure
    {
        public TestStructure(HolderSet<Biome> biomes)
            : base(new StructureSettings(biomes))
        {
        }
    }

    private sealed class RecordingPlacement : StructurePlacement
    {
        private readonly int? _acceptX;
        private readonly int? _acceptZ;

        public List<(int X, int Z)> Queries { get; } = new();

        public RecordingPlacement(int? acceptX = null, int? acceptZ = null)
        {
            _acceptX = acceptX;
            _acceptZ = acceptZ;
        }

        public bool IsStructureChunk(
            IStructurePlacementState state,
            int sourceX,
            int sourceZ)
        {
            Queries.Add((sourceX, sourceZ));
            return _acceptX == sourceX && _acceptZ == sourceZ;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
