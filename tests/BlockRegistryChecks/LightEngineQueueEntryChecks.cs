using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Lighting;

internal static class LightEngineQueueEntryChecks
{
    private sealed class TestMap : DataLayerStorageMap<TestMap>
    {
        public TestMap()
            : this(new Dictionary<long, DataLayer>())
        {
        }

        private TestMap(Dictionary<long, DataLayer> map)
            : base(map)
        {
        }

        public override TestMap Copy() =>
            new(new Dictionary<long, DataLayer>(Map));
    }

    private sealed class TestStorage : LayerLightSectionStorage<TestMap>
    {
        public TestStorage(LightChunkGetter source)
            : base(LightLayer.BLOCK, source, new TestMap())
        {
        }

        protected override int GetLightValue(long blockNode) => 0;
    }

    private sealed class TestGetter : LightChunkGetter
    {
        public LightChunk? GetChunkForLighting(int chunkX, int chunkZ) => null;

        public BlockGetter GetLevel() => EmptyBlockGetter.Instance;
    }

    public static void Run()
    {
        RegistryBootstrap.Initialize();
        CheckDirectionMaskAndLevelPacking();
        CheckIncreaseFlagsAndDirectionSelection();
        CheckSkySourceDirectionEncoding();
        CheckListenerDummyContract();
        CheckShapeHelpers();
    }

    private static void CheckDirectionMaskAndLevelPacking()
    {
        long allDirections = LightEngine<TestMap, TestStorage>.QueueEntry.DecreaseAllDirections(15);
        Require(LightEngine<TestMap, TestStorage>.QueueEntry.GetFromLevel(allDirections) == 15,
            "The low four bits must store a light level.");
        Require(Direction.GetValues().Length == 6,
            "Direction must expose the six vanilla propagation directions.");

        foreach (Direction direction in Direction.GetValues())
        {
            Require(LightEngine<TestMap, TestStorage>.QueueEntry.ShouldPropagateInDirection(allDirections, direction),
                "Decrease-all must set every direction bit.");
        }

        long truncated = LightEngine<TestMap, TestStorage>.QueueEntry.DecreaseAllDirections(31);
        Require(LightEngine<TestMap, TestStorage>.QueueEntry.GetFromLevel(truncated) == 15,
            "Queue levels must be masked to the four-bit range.");

        long skipNorth = LightEngine<TestMap, TestStorage>.QueueEntry.DecreaseSkipOneDirection(9, Direction.NORTH);
        Require(LightEngine<TestMap, TestStorage>.QueueEntry.GetFromLevel(skipNorth) == 9,
            "Decrease-skip must preserve the requested source level.");
        foreach (Direction direction in Direction.GetValues())
        {
            Require(
                LightEngine<TestMap, TestStorage>.QueueEntry.ShouldPropagateInDirection(skipNorth, direction) !=
                ReferenceEquals(direction, Direction.NORTH),
                "Decrease-skip must omit exactly the selected direction.");
        }
    }

    private static void CheckIncreaseFlagsAndDirectionSelection()
    {
        long emission = LightEngine<TestMap, TestStorage>.QueueEntry.IncreaseLightFromEmission(12, fromEmptyShape: true);
        Require(LightEngine<TestMap, TestStorage>.QueueEntry.GetFromLevel(emission) == 12,
            "Emission queue entries must preserve the four-bit level.");
        Require(LightEngine<TestMap, TestStorage>.QueueEntry.IsIncreaseFromEmission(emission),
            "Emission queue entries must set the emission flag.");
        Require(LightEngine<TestMap, TestStorage>.QueueEntry.IsFromEmptyShape(emission),
            "Emission queue entries must preserve the empty-shape flag.");
        Require(Direction.GetValues().All(direction =>
                LightEngine<TestMap, TestStorage>.QueueEntry.ShouldPropagateInDirection(emission, direction)),
            "Emission queue entries must initially select all directions.");

        long skip = LightEngine<TestMap, TestStorage>.QueueEntry.IncreaseSkipOneDirection(
            7,
            fromEmptyShape: true,
            Direction.WEST);
        Require(LightEngine<TestMap, TestStorage>.QueueEntry.IsFromEmptyShape(skip),
            "Increase-skip must preserve the empty-shape flag.");
        Require(!LightEngine<TestMap, TestStorage>.QueueEntry.IsIncreaseFromEmission(skip),
            "Increase-skip must not set the emission flag.");
        foreach (Direction direction in Direction.GetValues())
        {
            Require(
                LightEngine<TestMap, TestStorage>.QueueEntry.ShouldPropagateInDirection(skip, direction) !=
                ReferenceEquals(direction, Direction.WEST),
                "Increase-skip must omit exactly the selected direction.");
        }

        long onlyEast = LightEngine<TestMap, TestStorage>.QueueEntry.IncreaseOnlyOneDirection(
            5,
            fromEmptyShape: false,
            Direction.EAST);
        Require(LightEngine<TestMap, TestStorage>.QueueEntry.GetFromLevel(onlyEast) == 5,
            "Single-direction increase must store the source level.");
        Require(Direction.GetValues().Count(direction =>
                LightEngine<TestMap, TestStorage>.QueueEntry.ShouldPropagateInDirection(onlyEast, direction)) == 1 &&
                LightEngine<TestMap, TestStorage>.QueueEntry.ShouldPropagateInDirection(onlyEast, Direction.EAST),
            "Single-direction increase must set only the requested direction bit.");
    }

    private static void CheckSkySourceDirectionEncoding()
    {
        long data = LightEngine<TestMap, TestStorage>.QueueEntry.IncreaseSkySourceInDirections(
            down: true,
            north: false,
            south: true,
            west: false,
            east: true);

        Require(LightEngine<TestMap, TestStorage>.QueueEntry.GetFromLevel(data) == 15,
            "Sky source queue entries must begin at maximum light level.");
        Require(LightEngine<TestMap, TestStorage>.QueueEntry.ShouldPropagateInDirection(data, Direction.DOWN),
            "Sky source entries must encode downward propagation.");
        Require(!LightEngine<TestMap, TestStorage>.QueueEntry.ShouldPropagateInDirection(data, Direction.NORTH),
            "Sky source entries must leave disabled directions unset.");
        Require(LightEngine<TestMap, TestStorage>.QueueEntry.ShouldPropagateInDirection(data, Direction.SOUTH),
            "Sky source entries must encode south propagation.");
        Require(!LightEngine<TestMap, TestStorage>.QueueEntry.ShouldPropagateInDirection(data, Direction.WEST),
            "Sky source entries must leave west disabled when requested.");
        Require(LightEngine<TestMap, TestStorage>.QueueEntry.ShouldPropagateInDirection(data, Direction.EAST),
            "Sky source entries must encode east propagation.");
        Require(!LightEngine<TestMap, TestStorage>.QueueEntry.IsIncreaseFromEmission(data),
            "Sky source entries must not be flagged as block emission.");
    }

    private static void CheckListenerDummyContract()
    {
        LayerLightEventListener dummy = LayerLightEventListener.Dummy.Instance;
        var position = new BlockPos(1, 2, 3);
        Require(dummy.GetDataLayerData(SectionPos.Of(position)) is null,
            "The dummy listener must expose no light section data.");
        Require(dummy.GetLightValue(position) == 0,
            "The dummy listener must return zero brightness.");
        Require(!dummy.HasLightWork() && dummy.RunLightUpdates() == 0,
            "The dummy listener must report no pending work.");
        dummy.CheckBlock(position);
        dummy.UpdateSectionStatus(SectionPos.Of(position), sectionEmpty: false);
        dummy.SetLightEnabled(new ChunkPos(0, 0), enabled: true);
        dummy.PropagateLightSources(new ChunkPos(0, 0));
    }

    private static void CheckShapeHelpers()
    {
        BlockState air = Blocks.AIR.DefaultState;
        Require(LightEngine.GetLightDampeningInto(air, air, Direction.UP, 0) == 0,
            "Two empty occlusion shapes must retain the simple opacity.");
        Require(LightEngine.GetOcclusionShape(air, Direction.UP).IsEmpty(),
            "Air must expose an empty light occlusion shape.");
        Require(!LightEngine.HasDifferentLightProperties(air, air),
            "The same block-state instance must not request a light update.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
