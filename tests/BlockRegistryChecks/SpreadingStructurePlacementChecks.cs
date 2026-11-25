using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World;
using Asteria.World.LevelGen.Structure;
using Asteria.World.LevelGen.Structure.Placement;

internal static class SpreadingStructurePlacementChecks
{
    public static void Run()
    {
        CheckStructurePlacementDefaults();
        CheckStructureSetSelectionEntries();
        CheckLocatePositionOffset();
        CheckRandomSpreadPlacementSelection();
        CheckPlacementCheckOrdering();
        CheckFrequencyReductionAndExclusionZone();
    }

    private static void CheckStructurePlacementDefaults()
    {
        StructurePlacement placement = new DefaultPlacement();
        ChunkPos chunk = new(-4, 9);
        BlockPos located = placement.GetLocatePos(chunk);

        Require(
            placement.ApplyAdditionalChunkRestrictions(12, -7, 987654321L),
            "The base StructurePlacement restriction must allow a candidate by default.");
        Require(
            placement.LocateOffset().Equals(Vec3i.Zero),
            "The base StructurePlacement locate offset must default to Vec3i.ZERO.");
        Require(
            located.GetX() == chunk.GetMinBlockX() &&
            located.GetY() == 0 &&
            located.GetZ() == chunk.GetMinBlockZ(),
            "The default locate position must use the chunk minimum block corner at Y=0.");
    }

    private static void CheckStructureSetSelectionEntries()
    {
        var structure = new TestStructure();
        Holder<Structure> structureHolder = Holder<Structure>.Direct(structure);
        var placement = new TestPlacement(
            Vec3i.Zero,
            FrequencyReductionMethod.DEFAULT,
            1.0F,
            17,
            null,
            placementResult: true);

        var single = new StructureSet(structureHolder, placement);
        Require(
            single.Structures.Count == 1 &&
            ReferenceEquals(single.Structures[0].Structure, structureHolder) &&
            single.Structures[0].Weight == 1 &&
            ReferenceEquals(single.Placement, placement),
            "The single-entry StructureSet constructor must create one weight-1 selection entry.");

        StructureSet.StructureSelectionEntry weighted =
            StructureSet.Entry(structureHolder, 7);
        StructureSet.StructureSelectionEntry defaultWeight =
            StructureSet.Entry(structureHolder);

        Require(
            weighted.Weight == 7 &&
            ReferenceEquals(weighted.Structure, structureHolder) &&
            defaultWeight.Weight == 1,
            "StructureSet entry factories must preserve explicit weights and default to one.");
    }

    private static void CheckLocatePositionOffset()
    {
        var placement = new TestPlacement(
            new Vec3i(4, -2, 7),
            FrequencyReductionMethod.DEFAULT,
            1.0F,
            0,
            null,
            placementResult: true);

        BlockPos located = placement.GetLocatePos(new ChunkPos(2, -3));

        Require(
            located.GetX() == 36 &&
            located.GetY() == -2 &&
            located.GetZ() == -41,
            "Locate positions must offset the chunk's minimum block corner at Y=0.");
    }

    private static void CheckRandomSpreadPlacementSelection()
    {
        const long seed = 20261124L;
        var placement = new RandomSpreadStructurePlacement(
            28,
            6,
            RandomSpreadType.TRIANGULAR,
            10387320);
        ChunkPos candidate = placement.GetPotentialStructureChunk(seed, -47, 83);
        var state = new TestState(seed, hasNearbyStructure: false);

        Require(
            placement.IsStructureChunk(state, candidate.X, candidate.Z),
            "RandomSpreadStructurePlacement must accept the exact seeded candidate when frequency and exclusion restrictions permit it.");
        Require(
            !placement.IsStructureChunk(state, candidate.X + 1, candidate.Z),
            "RandomSpreadStructurePlacement must reject another chunk in the same spacing cell.");
        Require(
            state.ExclusionQueries == 1,
            "Only the exact candidate may proceed to the final exclusion-zone interaction stage.");
    }

    private static void CheckPlacementCheckOrdering()
    {
        var set = new StructureSet(
            Holder<Structure>.Direct(new TestStructure()),
            new TestPlacement(
                Vec3i.Zero,
                FrequencyReductionMethod.DEFAULT,
                1.0F,
                1,
                null,
                placementResult: true));
        Holder<StructureSet> otherSet = Holder<StructureSet>.Direct(set);
        var state = new TestState(123456789L, hasNearbyStructure: false);
        var placement = new TestPlacement(
            Vec3i.Zero,
            FrequencyReductionMethod.DEFAULT,
            1.0F,
            17,
            new AbstractSpreadingStructurePlacement.ExclusionZone(otherSet, 5),
            placementResult: false);

        Require(
            !placement.IsStructureChunk(state, 3, 9) &&
            state.ExclusionQueries == 0,
            "A failed placement-chunk predicate must short-circuit frequency and exclusion checks.");
    }

    private static void CheckFrequencyReductionAndExclusionZone()
    {
        var set = new StructureSet(
            Holder<Structure>.Direct(new TestStructure()),
            new TestPlacement(
                Vec3i.Zero,
                FrequencyReductionMethod.DEFAULT,
                1.0F,
                1,
                null,
                placementResult: true));
        Holder<StructureSet> otherSet = Holder<StructureSet>.Direct(set);

        var allowedState = new TestState(123456789L, hasNearbyStructure: false);
        var unrestricted = new TestPlacement(
            Vec3i.Zero,
            FrequencyReductionMethod.DEFAULT,
            1.0F,
            17,
            null,
            placementResult: true);
        Require(
            unrestricted.IsStructureChunk(allowedState, 3, 9) &&
            allowedState.ExclusionQueries == 0,
            "Frequency 1.0 with no exclusion zone must accept the placement without an unnecessary lookup.");

        var exclusionState = new TestState(123456789L, hasNearbyStructure: true);
        var excluded = new TestPlacement(
            Vec3i.Zero,
            FrequencyReductionMethod.DEFAULT,
            1.0F,
            17,
            new AbstractSpreadingStructurePlacement.ExclusionZone(otherSet, 5),
            placementResult: true);
        Require(
            !excluded.IsStructureChunk(exclusionState, 3, 9) &&
            exclusionState.ExclusionQueries == 1 &&
            ReferenceEquals(exclusionState.LastSet, otherSet) &&
            exclusionState.LastX == 3 &&
            exclusionState.LastZ == 9 &&
            exclusionState.LastRange == 5,
            "The exclusion zone must query the state with the exact other set, chunk and radius.");

        var zeroFrequencyState = new TestState(123456789L, hasNearbyStructure: false);
        var zeroFrequency = new TestPlacement(
            Vec3i.Zero,
            FrequencyReductionMethod.DEFAULT,
            0.0F,
            17,
            new AbstractSpreadingStructurePlacement.ExclusionZone(otherSet, 5),
            placementResult: true);
        Require(
            !zeroFrequency.IsStructureChunk(zeroFrequencyState, 3, 9) &&
            zeroFrequencyState.ExclusionQueries == 0,
            "A zero frequency must reject the candidate before querying exclusion-zone interactions.");
    }

    private sealed class DefaultPlacement : StructurePlacement
    {
        public bool IsStructureChunk(
            IStructurePlacementState state,
            int sourceX,
            int sourceZ) =>
            true;
    }

    private sealed class TestStructure : Structure
    {
        public TestStructure() : base(StructureSettings.DEFAULT)
        {
        }
    }

    private sealed class TestPlacement : AbstractSpreadingStructurePlacement
    {
        private readonly bool _placementResult;

        public TestPlacement(
            Vec3i locateOffset,
            FrequencyReductionMethod frequencyReductionMethod,
            float frequency,
            int salt,
            ExclusionZone? exclusionZone,
            bool placementResult)
            : base(
                locateOffset,
                frequencyReductionMethod,
                frequency,
                salt,
                exclusionZone)
        {
            _placementResult = placementResult;
        }

        protected override bool IsPlacementChunk(
            IStructurePlacementState state,
            int sourceX,
            int sourceZ) =>
            _placementResult;
    }

    private sealed class TestState : IStructurePlacementState
    {
        private readonly bool _hasNearbyStructure;

        public long LevelSeed { get; }
        public int ExclusionQueries { get; private set; }
        public Holder<StructureSet>? LastSet { get; private set; }
        public int LastX { get; private set; }
        public int LastZ { get; private set; }
        public int LastRange { get; private set; }

        public TestState(long levelSeed, bool hasNearbyStructure)
        {
            LevelSeed = levelSeed;
            _hasNearbyStructure = hasNearbyStructure;
        }

        public bool HasStructureChunkInRange(
            Holder<StructureSet> otherSet,
            int sourceX,
            int sourceZ,
            int chunkCount)
        {
            ExclusionQueries++;
            LastSet = otherSet;
            LastX = sourceX;
            LastZ = sourceZ;
            LastRange = chunkCount;
            return _hasNearbyStructure;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
