using Asteria.Content.Blocks;
using Asteria.Core;
using Asteria.World;

namespace Asteria.Content.Chunks;

/// <summary>
/// Vertical chunk storage and height mapping, limited to the block-state portion of ChunkAccess.
/// </summary>
public sealed class Chunk : LevelHeightAccessor
{
    public const int NoFilledSection = -1;

    private readonly ChunkPos _chunkPos;
    private readonly LevelHeightAccessor _levelHeightAccessor;
    private readonly BlockState _defaultBlockState;
    private readonly ChunkSection[] _sections;

    public Chunk(
        ChunkPos chunkPos,
        LevelHeightAccessor levelHeightAccessor,
        BlockStateContainerFactory containerFactory,
        ChunkSection?[]? sections = null)
    {
        ArgumentNullException.ThrowIfNull(levelHeightAccessor);
        ArgumentNullException.ThrowIfNull(containerFactory);

        _chunkPos = chunkPos;
        _levelHeightAccessor = levelHeightAccessor;
        _defaultBlockState = containerFactory.DefaultBlockState;
        _sections = new ChunkSection[levelHeightAccessor.GetSectionsCount()];

        if (sections is not null && _sections.Length == sections.Length)
            Array.Copy(sections, _sections, _sections.Length);

        ReplaceMissingSections(containerFactory, _sections);
    }

    private static void ReplaceMissingSections(
        BlockStateContainerFactory containerFactory,
        ChunkSection?[] sections)
    {
        for (int index = 0; index < sections.Length; index++)
        {
            sections[index] ??= containerFactory.CreateSection();
        }
    }

    public ChunkPos GetPos() => _chunkPos;

    /// <summary>
    /// Returns the block state at a world position using the same section selection and
    /// local-coordinate masking as LevelChunk.getBlockState.
    /// </summary>
    public BlockState GetBlockState(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);

        int x = position.GetX();
        int y = position.GetY();
        int z = position.GetZ();
        int sectionIndex = GetSectionIndex(y);

        if (sectionIndex >= 0 && sectionIndex < _sections.Length)
        {
            ChunkSection currentSection = _sections[sectionIndex];
            if (!currentSection.HasOnlyAir)
            {
                return currentSection.GetBlockState(
                    x & SectionPos.SectionMask,
                    y & SectionPos.SectionMask,
                    z & SectionPos.SectionMask);
            }
        }

        return _defaultBlockState;
    }

    /// <summary>
    /// Returns the backing array, matching ChunkAccess.getSections().
    /// Callers must not insert null entries.
    /// </summary>
    public ChunkSection[] GetSections() => _sections;

    public ChunkSection GetSection(int sectionIndex) =>
        _sections[sectionIndex];

    public int GetHighestFilledSectionIndex()
    {
        for (int sectionIndex = _sections.Length - 1; sectionIndex >= 0; sectionIndex--)
        {
            if (!_sections[sectionIndex].HasOnlyAir)
                return sectionIndex;
        }

        return NoFilledSection;
    }

    [Obsolete("Use GetHighestFilledSectionIndex and section coordinates.")]
    public int GetHighestSectionPosition()
    {
        int sectionIndex = GetHighestFilledSectionIndex();
        return sectionIndex == NoFilledSection
            ? GetMinY()
            : SectionPos.SectionToBlockCoord(GetSectionYFromSectionIndex(sectionIndex));
    }

    public bool IsYSpaceEmpty(int yStartInclusive, int yEndInclusive)
    {
        if (yStartInclusive < GetMinY())
            yStartInclusive = GetMinY();

        if (yEndInclusive > GetMaxY())
            yEndInclusive = GetMaxY();

        for (int y = yStartInclusive; y <= yEndInclusive; y = unchecked(y + 16))
        {
            if (!GetSection(GetSectionIndex(y)).HasOnlyAir)
                return false;
        }

        return true;
    }

    public int GetHeight() => _levelHeightAccessor.GetHeight();

    public int GetMinY() => _levelHeightAccessor.GetMinY();

    public int GetMaxY() => _levelHeightAccessor.GetMaxY();

    public int GetMinSectionY() => _levelHeightAccessor.GetMinSectionY();

    public int GetMaxSectionY() => _levelHeightAccessor.GetMaxSectionY();

    public int GetSectionsCount() => _levelHeightAccessor.GetSectionsCount();

    public int GetSectionIndex(int blockY) => _levelHeightAccessor.GetSectionIndex(blockY);

    public int GetSectionIndexFromSectionY(int sectionY) =>
        _levelHeightAccessor.GetSectionIndexFromSectionY(sectionY);

    public int GetSectionYFromSectionIndex(int sectionIndex) =>
        _levelHeightAccessor.GetSectionYFromSectionIndex(sectionIndex);

    public bool IsInsideBuildHeight(BlockPos position) =>
        _levelHeightAccessor.IsInsideBuildHeight(position);

    public bool IsInsideBuildHeight(int blockY) =>
        _levelHeightAccessor.IsInsideBuildHeight(blockY);

    public bool IsOutsideBuildHeight(BlockPos position) =>
        _levelHeightAccessor.IsOutsideBuildHeight(position);

    public bool IsOutsideBuildHeight(int blockY) =>
        _levelHeightAccessor.IsOutsideBuildHeight(blockY);
}
