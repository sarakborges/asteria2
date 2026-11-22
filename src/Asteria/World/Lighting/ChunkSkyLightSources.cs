using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Storage;
using Asteria.Core;
using Asteria.World;

namespace Asteria.World.Lighting;

/// <summary>
/// Tracks the lowest vertical edge from which skylight enters each 16x16 chunk column.
/// </summary>
public sealed class ChunkSkyLightSources
{
    private const int Size = 16;
    private const int ColumnCount = Size * Size;

    public const int NegativeInfinity = int.MinValue;

    private readonly int _minY;
    private readonly PackedBitStorage _heightmap;
    private readonly BlockPos.MutableBlockPos _mutablePosition1 = new();
    private readonly BlockPos.MutableBlockPos _mutablePosition2 = new();

    public ChunkSkyLightSources(LevelHeightAccessor level)
    {
        ArgumentNullException.ThrowIfNull(level);

        _minY = unchecked(level.GetMinY() - 1);
        int maxY = unchecked(level.GetMaxY() + 1);
        int bits = Math.Max(
            1,
            PackedBitStorage.RequiredBitsForValueCount(unchecked(maxY - _minY + 1)));
        _heightmap = new PackedBitStorage(bits, ColumnCount);
    }

    public void FillFrom(ChunkAccess chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        int maxSectionIndex = chunk.GetHighestFilledSectionIndex();
        if (maxSectionIndex == ChunkAccess.NoFilledSection)
        {
            Fill(_minY);
            return;
        }

        for (int z = 0; z < Size; z++)
        {
            for (int x = 0; x < Size; x++)
            {
                int initialEdgeY = Math.Max(
                    FindLowestSourceY(chunk, maxSectionIndex, x, z),
                    _minY);
                Set(Index(x, z), initialEdgeY);
            }
        }
    }

    private int FindLowestSourceY(ChunkAccess chunk, int topSectionIndex, int x, int z)
    {
        int topY = SectionPos.SectionToBlockCoord(
            chunk.GetSectionYFromSectionIndex(topSectionIndex) + 1);
        BlockPos.MutableBlockPos topPosition = _mutablePosition1.Set(x, topY, z);
        BlockPos.MutableBlockPos bottomPosition =
            _mutablePosition2.Set(x, unchecked(topY - 1), z);
        BlockState topState = Blocks.AIR.DefaultState;

        for (int sectionIndex = topSectionIndex; sectionIndex >= 0; sectionIndex--)
        {
            ChunkSection section = chunk.GetSection(sectionIndex);
            if (section.HasOnlyAir)
            {
                topState = Blocks.AIR.DefaultState;
                int sectionY = chunk.GetSectionYFromSectionIndex(sectionIndex);
                topPosition.SetY(SectionPos.SectionToBlockCoord(sectionY));
                bottomPosition.SetY(unchecked(topPosition.GetY() - 1));
                continue;
            }

            for (int y = 15; y >= 0; y--)
            {
                BlockState bottomState = section.GetBlockState(x, y, z);
                if (IsEdgeOccluded(topState, bottomState))
                    return topPosition.GetY();

                topState = bottomState;
                topPosition.Set(bottomPosition);
                bottomPosition.Move(0, -1, 0);
            }
        }

        return _minY;
    }

    public bool Update(BlockGetter level, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(level);

        int upperEdgeY = unchecked(y + 1);
        int index = Index(x, z);
        int currentLowestSourceY = Get(index);
        if (upperEdgeY < currentLowestSourceY)
            return false;

        BlockPos.MutableBlockPos topPosition = _mutablePosition1.Set(x, upperEdgeY, z);
        BlockState topState = level.GetBlockState(topPosition);
        BlockPos.MutableBlockPos middlePosition = _mutablePosition2.Set(x, y, z);
        BlockState middleState = level.GetBlockState(middlePosition);

        if (UpdateEdge(
                level,
                index,
                currentLowestSourceY,
                topPosition,
                topState,
                middlePosition,
                middleState))
        {
            return true;
        }

        BlockPos.MutableBlockPos bottomPosition =
            _mutablePosition1.Set(x, unchecked(y - 1), z);
        BlockState bottomState = level.GetBlockState(bottomPosition);
        return UpdateEdge(
            level,
            index,
            currentLowestSourceY,
            middlePosition,
            middleState,
            bottomPosition,
            bottomState);
    }

    private bool UpdateEdge(
        BlockGetter level,
        int index,
        int oldTopEdgeY,
        BlockPos topPosition,
        BlockState topState,
        BlockPos bottomPosition,
        BlockState bottomState)
    {
        int checkedEdgeY = topPosition.GetY();
        if (IsEdgeOccluded(topState, bottomState))
        {
            if (checkedEdgeY > oldTopEdgeY)
            {
                Set(index, checkedEdgeY);
                return true;
            }
        }
        else if (checkedEdgeY == oldTopEdgeY)
        {
            Set(index, FindLowestSourceBelow(level, bottomPosition, bottomState));
            return true;
        }

        return false;
    }

    private int FindLowestSourceBelow(
        BlockGetter level,
        BlockPos startPosition,
        BlockState startState)
    {
        BlockPos.MutableBlockPos topPosition = _mutablePosition1.Set(startPosition);
        BlockPos.MutableBlockPos bottomPosition =
            _mutablePosition2.SetWithOffset(startPosition, 0, -1, 0);
        BlockState topState = startState;

        while (bottomPosition.GetY() >= _minY)
        {
            BlockState bottomState = level.GetBlockState(bottomPosition);
            if (IsEdgeOccluded(topState, bottomState))
                return topPosition.GetY();

            topState = bottomState;
            topPosition.Set(bottomPosition);
            bottomPosition.Move(0, -1, 0);
        }

        return _minY;
    }

    private static bool IsEdgeOccluded(BlockState topState, BlockState bottomState)
    {
        if (bottomState.LightDampening != 0)
            return true;

        VoxelShape topShape = LightEngine.GetOcclusionShape(topState, Direction.DOWN);
        VoxelShape bottomShape = LightEngine.GetOcclusionShape(bottomState, Direction.UP);
        return Shapes.FaceShapeOccludes(topShape, bottomShape);
    }

    public int GetLowestSourceY(int x, int z) =>
        ExtendSourcesBelowWorld(Get(Index(x, z)));

    public int GetHighestLowestSourceY()
    {
        int maxValue = int.MinValue;
        for (int index = 0; index < _heightmap.Size; index++)
        {
            int value = _heightmap.Get(index);
            if (value > maxValue)
                maxValue = value;
        }

        return ExtendSourcesBelowWorld(unchecked(maxValue + _minY));
    }

    private void Fill(int lowestSourceY) =>
        _heightmap.Fill(unchecked(lowestSourceY - _minY));

    private void Set(int index, int value) =>
        _heightmap.Set(index, unchecked(value - _minY));

    private int Get(int index) =>
        unchecked(_heightmap.Get(index) + _minY);

    private int ExtendSourcesBelowWorld(int value) =>
        value == _minY ? NegativeInfinity : value;

    private static int Index(int x, int z) =>
        unchecked(x + z * Size);
}
