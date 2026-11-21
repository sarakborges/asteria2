using Asteria.Core;

namespace Asteria.World;

/// <summary>
/// Exposes the vertical build bounds and maps block/section Y coordinates to section-array indices.
/// Mirrors the LevelHeightAccessor contract.
/// </summary>
public interface LevelHeightAccessor
{
    int GetHeight();

    int GetMinY();

    int GetMaxY() =>
        unchecked(GetMinY() + GetHeight() - 1);

    int GetSectionsCount() =>
        unchecked(GetMaxSectionY() - GetMinSectionY() + 1);

    int GetMinSectionY() =>
        SectionPos.BlockToSectionCoord(GetMinY());

    int GetMaxSectionY() =>
        SectionPos.BlockToSectionCoord(GetMaxY());

    bool IsInsideBuildHeight(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return IsInsideBuildHeight(position.GetY());
    }

    bool IsInsideBuildHeight(int blockY) =>
        blockY >= GetMinY() && blockY <= GetMaxY();

    bool IsOutsideBuildHeight(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return IsOutsideBuildHeight(position.GetY());
    }

    bool IsOutsideBuildHeight(int blockY) =>
        blockY < GetMinY() || blockY > GetMaxY();

    int GetSectionIndex(int blockY) =>
        GetSectionIndexFromSectionY(SectionPos.BlockToSectionCoord(blockY));

    int GetSectionIndexFromSectionY(int sectionY) =>
        unchecked(sectionY - GetMinSectionY());

    int GetSectionYFromSectionIndex(int sectionIndex) =>
        unchecked(sectionIndex + GetMinSectionY());

    static LevelHeightAccessor Create(int minY, int height) =>
        new FixedLevelHeightAccessor(minY, height);
}

internal sealed class FixedLevelHeightAccessor : LevelHeightAccessor
{
    private readonly int _minY;
    private readonly int _height;

    public FixedLevelHeightAccessor(int minY, int height)
    {
        _minY = minY;
        _height = height;
    }

    public int GetHeight() => _height;

    public int GetMinY() => _minY;
}
