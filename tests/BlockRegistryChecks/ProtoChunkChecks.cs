using Asteria.Content.Blocks;
using Asteria.Content.Biomes;
using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Content.Registries;
using Asteria.Content.Fluids;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Lighting;
using BuiltInBlocks = Asteria.Content.Blocks.Blocks;

internal static class ProtoChunkChecks
{
    private sealed class RecordingLightEngine : LightEngine
    {
        public List<(SectionPos Position, bool IsEmpty)> SectionStatusChanges { get; } = new();
        public List<BlockPos> CheckedBlocks { get; } = new();

        public override void CheckBlock(BlockPos position) =>
            CheckedBlocks.Add(position.Immutable());

        public override void UpdateSectionStatus(SectionPos sectionPosition, bool sectionEmpty) =>
            SectionStatusChanges.Add((sectionPosition, sectionEmpty));
    }

    public static void Run()
    {
        CheckBlockAndFluidLookup();
        CheckMutationAndWorldgenHeightmaps();
        CheckFinalHeightmapTransition();
        CheckOutsideBuildHeightAndAirFastPath();
        CheckLightEngineNotifications();
        CheckPersistedStatusDirtyState();
        CheckPostProcessingOffsets();
        CheckBiomeAvailabilityGate();
    }

    private static ProtoChunk CreateChunk(int chunkX = 0, int chunkZ = 0) =>
        new(
            new ChunkPos(chunkX, chunkZ),
            LevelHeightAccessor.Create(-16, 48),
            PalettedContainerFactory.CreateBuiltIn());

    private static void CheckBlockAndFluidLookup()
    {
        ProtoChunk chunk = CreateChunk(2, -3);
        int x = chunk.GetPos().GetMinBlockX() + 3;
        int z = chunk.GetPos().GetMinBlockZ() + 5;
        var position = new BlockPos(x, 4, z);

        Require(
            ReferenceEquals(chunk.GetBlockState(position), BuiltInBlocks.AIR.DefaultState),
            "A ProtoChunk empty section must return ordinary AIR.");

        Require(
            chunk.GetFluidState(position).IsEmpty(),
            "An empty ProtoChunk section must return the empty fluid state.");

        chunk.GetSection(chunk.GetSectionIndex(4))
            .SetBlockState(3, 4, 5, BuiltInBlocks.STONE.DefaultState);

        Require(
            ReferenceEquals(chunk.GetBlockState(position), BuiltInBlocks.STONE.DefaultState),
            "ProtoChunk lookup must read the local section palette at world coordinates.");

        Require(
            chunk.GetFluidState(position).IsEmpty(),
            "A non-fluid block must retain its own empty fluid state.");
    }

    private static void CheckMutationAndWorldgenHeightmaps()
    {
        ProtoChunk chunk = CreateChunk(-2, 4);
        int x = chunk.GetPos().GetMinBlockX() + 3;
        int z = chunk.GetPos().GetMinBlockZ() + 6;
        var firstPosition = new BlockPos(x, 2, z);
        var topPosition = new BlockPos(x, 7, z);

        BlockState previous = chunk.SetBlockState(firstPosition, BuiltInBlocks.STONE.DefaultState);
        Require(
            ReferenceEquals(previous, BuiltInBlocks.AIR.DefaultState) &&
            ReferenceEquals(chunk.GetBlockState(firstPosition), BuiltInBlocks.STONE.DefaultState),
            "ProtoChunk.SetBlockState must return the previous palette state and store the new state.");

        Require(
            chunk.GetHeightmaps().Count == 2 &&
            chunk.GetHeight(Heightmap.Types.WORLD_SURFACE_WG, x, z) == 2,
            "The EMPTY persisted status must lazily create and prime the two world-generation heightmaps.");

        BlockState previousTop = chunk.SetBlockState(topPosition, BuiltInBlocks.STONE.DefaultState, flags: 0);
        Require(
            ReferenceEquals(previousTop, BuiltInBlocks.AIR.DefaultState) &&
            chunk.GetHeight(Heightmap.Types.WORLD_SURFACE_WG, x, z) == 7,
            "Adding a higher block must incrementally update an already-primed world-generation heightmap.");

        chunk.SetBlockState(topPosition, BuiltInBlocks.AIR.DefaultState);
        Require(
            chunk.GetHeight(Heightmap.Types.WORLD_SURFACE_WG, x, z) == 2,
            "Removing the topmost block must rescan below and restore the previous highest surface.");
    }

    private static void CheckFinalHeightmapTransition()
    {
        ProtoChunk chunk = CreateChunk();
        int x = 1;
        int z = 2;
        chunk.SetBlockState(new BlockPos(x, 1, z), BuiltInBlocks.STONE.DefaultState);
        chunk.SetPersistedStatus(ChunkStatus.TERRAIN);
        chunk.TryMarkSaved();

        chunk.SetBlockState(new BlockPos(x, 9, z), BuiltInBlocks.STONE.DefaultState);

        Require(
            chunk.GetHeightmaps().Count == 6 &&
            chunk.GetHeight(Heightmap.Types.WORLD_SURFACE, x, z) == 9,
            "At TERRAIN, the first block mutation must prime the four final heightmaps alongside the two retained world-generation maps.");
    }

    private static void CheckOutsideBuildHeightAndAirFastPath()
    {
        ProtoChunk chunk = CreateChunk();
        BlockPos below = new(1, chunk.GetMinY() - 1, 1);
        BlockPos above = new(1, chunk.GetMaxY() + 1, 1);

        Require(
            ReferenceEquals(chunk.GetBlockState(below), BuiltInBlocks.VOID_AIR.DefaultState) &&
            ReferenceEquals(chunk.GetBlockState(above), BuiltInBlocks.VOID_AIR.DefaultState),
            "ProtoChunk block reads outside the build height must return VOID_AIR.");

        Require(
            chunk.GetFluidState(below).IsEmpty() && chunk.GetFluidState(above).IsEmpty(),
            "ProtoChunk fluid reads outside the build height must return EMPTY.");

        Require(
            ReferenceEquals(chunk.SetBlockState(below, BuiltInBlocks.STONE.DefaultState), BuiltInBlocks.VOID_AIR.DefaultState) &&
            ReferenceEquals(chunk.GetBlockState(below), BuiltInBlocks.VOID_AIR.DefaultState),
            "ProtoChunk block writes outside the build height must return VOID_AIR without storing the requested state.");

        ProtoChunk emptyChunk = CreateChunk();
        BlockState air = BuiltInBlocks.AIR.DefaultState;
        int mapsBefore = emptyChunk.GetHeightmaps().Count;
        Require(
            ReferenceEquals(emptyChunk.SetBlockState(new BlockPos(0, 0, 0), air), air) &&
            emptyChunk.GetHeightmaps().Count == mapsBefore,
            "An AIR write into an already-empty section must take the vanilla fast path before heightmap creation.");
    }

    private static void CheckLightEngineNotifications()
    {
        ProtoChunk chunk = CreateChunk();
        var engine = new RecordingLightEngine();
        chunk.SetLightEngine(engine);
        chunk.InitializeLightSources();
        chunk.SetPersistedStatus(ChunkStatus.INITIALIZE_LIGHT);

        BlockPos position = new(2, 3, 4);
        chunk.SetBlockState(position, BuiltInBlocks.STONE.DefaultState);

        Require(
            engine.SectionStatusChanges.Count == 1 &&
            engine.SectionStatusChanges[0].Position.Equals(SectionPos.Of(position)) &&
            !engine.SectionStatusChanges[0].IsEmpty,
            "When a section transitions from empty to non-empty after INITIALIZE_LIGHT, ProtoChunk must notify the corresponding section node.");

        Require(
            engine.CheckedBlocks.Count == 1 &&
            engine.CheckedBlocks[0].Equals(position),
            "A change in light dampening must update skylight sources and enqueue a light-engine block check.");

        chunk.SetBlockState(position, BuiltInBlocks.AIR.DefaultState);
        Require(
            engine.SectionStatusChanges.Count == 2 &&
            engine.SectionStatusChanges[1].IsEmpty &&
            engine.CheckedBlocks.Count == 2,
            "Removing the only block must notify the section becoming empty and schedule a second light check.");
    }

    private static void CheckPersistedStatusDirtyState()
    {
        ProtoChunk chunk = CreateChunk();
        Require(
            ReferenceEquals(chunk.GetPersistedStatus(), ChunkStatus.EMPTY) &&
            ReferenceEquals(chunk.GetHighestGeneratedStatus(), ChunkStatus.EMPTY) &&
            !chunk.IsUnsaved(),
            "A new ProtoChunk without retrogen must report EMPTY as both persisted and highest generated status.");

        chunk.SetPersistedStatus(ChunkStatus.BIOMES);
        Require(
            ReferenceEquals(chunk.GetPersistedStatus(), ChunkStatus.BIOMES) &&
            ReferenceEquals(chunk.GetHighestGeneratedStatus(), ChunkStatus.BIOMES) &&
            chunk.IsUnsaved() &&
            chunk.TryMarkSaved(),
            "Without below-zero retrogen, the highest generated status must track the persisted status.");
    }

    private static void CheckPostProcessingOffsets()
    {
        ProtoChunk chunk = CreateChunk(-2, 4);
        BlockPos position = new(
            chunk.GetPos().GetMinBlockX() + 3,
            -7,
            chunk.GetPos().GetMinBlockZ() + 5);
        int sectionIndex = chunk.GetSectionIndex(position.GetY());
        int sectionY = chunk.GetSectionYFromSectionIndex(sectionIndex);

        Require(
            chunk.GetPostProcessing().Length == chunk.GetSectionsCount() &&
            chunk.GetPostProcessing().All(offsets => offsets is null),
            "ChunkAccess must allocate one initially empty post-processing slot per vertical section.");

        chunk.MarkPosForPostProcessing(position);
        List<short> offsets = chunk.GetPostProcessing()[sectionIndex] ??
            throw new InvalidOperationException("MarkPosForPostProcessing must create the target section's offset list.");

        Require(
            offsets.Count == 1 &&
            offsets[0] == ProtoChunk.PackOffsetCoordinates(position) &&
            ProtoChunk.UnpackOffsetCoordinates(offsets[0], sectionY, chunk.GetPos()).Equals(position),
            "ProtoChunk post-processing coordinates must round-trip through the vanilla packed local X/Y/Z bit layout.");

        short second = ProtoChunk.PackOffsetCoordinates(new BlockPos(
            chunk.GetPos().GetMinBlockX() + 9,
            -2,
            chunk.GetPos().GetMinBlockZ() + 12));
        short third = ProtoChunk.PackOffsetCoordinates(new BlockPos(
            chunk.GetPos().GetMinBlockX() + 14,
            -1,
            chunk.GetPos().GetMinBlockZ() + 1));
        chunk.AddPackedPostProcess(new[] { second, third }, sectionIndex);

        Require(
            offsets.SequenceEqual(new[] { ProtoChunk.PackOffsetCoordinates(position), second, third }),
            "addPackedPostProcess must append incoming offsets to the existing section list without reordering or replacing it.");

        int totalOffsets = chunk.GetPostProcessing().Where(list => list is not null).Sum(list => list!.Count);
        chunk.MarkPosForPostProcessing(new BlockPos(
            position.GetX(),
            chunk.GetMinY() - 1,
            position.GetZ()));
        chunk.MarkPosForPostProcessing(new BlockPos(
            position.GetX(),
            chunk.GetMaxY() + 1,
            position.GetZ()));

        Require(
            chunk.GetPostProcessing().Where(list => list is not null).Sum(list => list!.Count) == totalOffsets,
            "ProtoChunk must ignore post-processing positions outside the inclusive build-height range.");

        RequireThrows<IndexOutOfRangeException>(
            () => chunk.AddPackedPostProcess(new[] { second }, chunk.GetSectionsCount()),
            "A post-processing section index outside the backing array must preserve the array bounds failure.");
    }

    private sealed class AlternatingBiomeResolver : BiomeResolver
    {
        private readonly Holder<Biome> _plains = Biomes.PLAINS.BuiltInRegistryHolder!;
        private readonly Holder<Biome> _desert = Biomes.DESERT.BuiltInRegistryHolder!;

        public int CallCount { get; private set; }

        public Holder<Biome> GetNoiseBiome(int quartX, int quartY, int quartZ)
        {
            CallCount++;
            return ((quartX + quartY + quartZ) & 1) == 0 ? _plains : _desert;
        }
    }

    private static void CheckBiomeAvailabilityGate()
    {
        ProtoChunk chunk = CreateChunk(2, -3);
        var resolver = new AlternatingBiomeResolver();

        RequireThrows<InvalidOperationException>(
            () => chunk.GetNoiseBiome(0, 0, 0),
            "ProtoChunk must reject biome lookup before the persisted status reaches BIOMES.");

        chunk.FillBiomesFromNoise(resolver);
        Require(
            resolver.CallCount == chunk.GetSectionsCount() * ChunkSection.BiomeCount,
            "ProtoChunk biome filling must resolve all 64 quart cells in each vertical section.");

        RequireThrows<InvalidOperationException>(
            () => chunk.GetNoiseBiome(0, 0, 0),
            "Filling the biome palette alone must not expose biomes before the chunk status is promoted.");

        chunk.SetPersistedStatus(ChunkStatus.BIOMES);

        int quartMinX = QuartPos.FromBlock(chunk.GetPos().GetMinBlockX());
        int quartMinY = QuartPos.FromBlock(chunk.GetMinY());
        int quartMinZ = QuartPos.FromBlock(chunk.GetPos().GetMinBlockZ());
        int quartMaxY = unchecked(quartMinY + QuartPos.FromBlock(chunk.GetHeight()) - 1);

        Require(
            ReferenceEquals(
                chunk.GetNoiseBiome(quartMinX, quartMinY, quartMinZ),
                chunk.GetSection(chunk.GetSectionIndex(chunk.GetMinY()))
                    .GetNoiseBiome(0, 0, 0)),
            "After BIOMES, ProtoChunk must delegate lookup to the section's quart biome palette.");

        Require(
            ReferenceEquals(
                chunk.GetNoiseBiome(quartMinX, int.MinValue, quartMinZ),
                chunk.GetNoiseBiome(quartMinX, quartMinY, quartMinZ)),
            "After BIOMES, ProtoChunk must clamp low quart Y values to the build-height minimum.");

        Require(
            ReferenceEquals(
                chunk.GetNoiseBiome(quartMinX, int.MaxValue, quartMinZ),
                chunk.GetNoiseBiome(quartMinX, quartMaxY, quartMinZ)),
            "After BIOMES, ProtoChunk must clamp high quart Y values to the build-height maximum.");
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
