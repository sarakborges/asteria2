using Asteria.Core;
using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.World;
using Asteria.World.Rendering;
using Asteria.World.Level;

internal static class ChunkRenderRegionChecks
{
    public static void Run()
    {
        IReadOnlyList<ChunkPos> singleChunk =
            ChunkRenderRegion.GetChunkPositions(ChunkPos.Zero, 0);
        Require(
            singleChunk.Count == 1 &&
            singleChunk[0] == ChunkPos.Zero,
            "A zero-radius render region must contain only its center chunk.");

        IReadOnlyList<ChunkPos> originRegion =
            ChunkRenderRegion.GetChunkPositions(ChunkPos.Zero, 1);
        ChunkPos[] expected =
        {
            new(0, 0),
            new(-1, 0),
            new(0, -1),
            new(0, 1),
            new(1, 0),
            new(-1, -1),
            new(-1, 1),
            new(1, -1),
            new(1, 1)
        };
        Require(
            originRegion.SequenceEqual(expected),
            "A radius-one region must contain the 3x3 square in deterministic center-out order.");

        ChunkPos negativeCenter = new(-2, 3);
        IReadOnlyList<ChunkPos> negativeRegion =
            ChunkRenderRegion.GetChunkPositions(negativeCenter, 1);
        Require(
            negativeRegion.Count == 9 &&
            negativeRegion[0] == negativeCenter &&
            negativeRegion.Contains(new ChunkPos(-3, 3)) &&
            negativeRegion.Contains(new ChunkPos(-2, 2)) &&
            negativeRegion.Contains(new ChunkPos(-1, 4)),
            "Render-region coordinates must remain correct across negative chunk coordinates.");

        int maximum = ChunkPyramid.MaxChunkCoordinateValue;
        IReadOnlyList<ChunkPos> boundaryRegion =
            ChunkRenderRegion.GetChunkPositions(new ChunkPos(maximum, maximum), 1);
        Require(
            boundaryRegion.Count == 4 &&
            boundaryRegion.All(position => position.IsValid()),
            "Render regions at the world-generation limit must omit invalid neighboring chunk coordinates.");

        bool rejectedNegativeRadius = false;
        try
        {
            _ = ChunkRenderRegion.GetChunkPositions(ChunkPos.Zero, -1);
        }
        catch (ArgumentOutOfRangeException)
        {
            rejectedNegativeRadius = true;
        }

        Require(
            rejectedNegativeRadius,
            "A negative render radius must be rejected.");

        CheckEditedChunkRetentionOutsideRenderRegion();
    }

    private static void CheckEditedChunkRetentionOutsideRenderRegion()
    {
        var world = new OverworldWorld(
            seed: 0x53545245414DL,
            heightAccessor: LevelHeightAccessor.Create(-64, 96));
        ProtoChunk originalChunk = world.GetChunk(ChunkPos.Zero);
        BlockPos editedPosition = new(4, -1, 5);
        BlockState originalState = originalChunk.GetBlockState(editedPosition);
        BlockState editedState = ReferenceEquals(originalState, Blocks.TUFF.DefaultState)
            ? Blocks.STONE.DefaultState
            : Blocks.TUFF.DefaultState;

        world.SetBlockState(editedPosition, editedState, flags: 0);
        IReadOnlyList<ChunkPos> movedRegion =
            ChunkRenderRegion.GetChunkPositions(new ChunkPos(2, 0), 1);

        Require(
            !movedRegion.Contains(ChunkPos.Zero),
            "Moving the active radius-two chunks away must place the original chunk outside the new render region.");

        world.GetChunk(movedRegion[0]);

        Require(
            world.TryGetChunk(ChunkPos.Zero, out ProtoChunk? retainedChunk) &&
            ReferenceEquals(retainedChunk, originalChunk) &&
            ReferenceEquals(world.GetBlockState(editedPosition), editedState) &&
            originalChunk.IsUnsaved() &&
            world.LoadedChunkCount == 2,
            "Generating a non-overlapping render region must not evict cached chunks or lose unsaved block edits.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
