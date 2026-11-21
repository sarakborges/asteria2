using System.Collections.ObjectModel;
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
    private readonly ChunkSection[] _sections;
    private readonly Dictionary<HeightmapType, Heightmap> _heightmaps = new();
    private readonly ReadOnlyDictionary<HeightmapType, Heightmap> _heightmapsView;

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
        _sections = new ChunkSection[levelHeightAccessor.GetSectionsCount()];
        _heightmapsView = new ReadOnlyDictionary<HeightmapType, Heightmap>(_heightmaps);

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

        return global::Asteria.Content.Blocks.Blocks.AIR.DefaultState;
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

    /// <summary>Returns a read-only view of the heightmaps currently owned by this chunk.</summary>
    public IReadOnlyDictionary<HeightmapType, Heightmap> GetHeightmaps() => _heightmapsView;

    /// <summary>
    /// Loads serialized heightmap words. An incompatible word count falls back to a fresh
    /// priming pass, matching ChunkAccess.setHeightmap and Heightmap.setRawData.
    /// </summary>
    public void SetHeightmap(HeightmapType type, ulong[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        GetOrCreateHeightmapUnprimed(type).SetRawData(this, type, data);
    }

    /// <summary>
    /// Gets an existing heightmap or creates an unprimed one, matching ChunkAccess.
    /// </summary>
    public Heightmap GetOrCreateHeightmapUnprimed(HeightmapType type)
    {
        if (_heightmaps.TryGetValue(type, out Heightmap? heightmap))
            return heightmap;

        heightmap = new Heightmap(this, type);
        _heightmaps.Add(type, heightmap);
        return heightmap;
    }

    /// <summary>
    /// Reports whether a heightmap entry exists, matching the reference method's map-presence semantics.
    /// An explicitly created unprimed map also counts as present.
    /// </summary>
    public bool HasPrimedHeightmap(HeightmapType type) =>
        _heightmaps.ContainsKey(type);

    /// <summary>
    /// Creates and primes all requested heightmaps in one shared top-down scan.
    /// </summary>
    public void PrimeHeightmaps(IEnumerable<HeightmapType> types)
    {
        ArgumentNullException.ThrowIfNull(types);

        var requested = new List<Heightmap>();
        var seen = new HashSet<HeightmapType>();
        foreach (HeightmapType type in types)
        {
            if (seen.Add(type))
                requested.Add(GetOrCreateHeightmapUnprimed(type));
        }

        Heightmap.PrimeAll(requested);
    }

    /// <summary>
    /// Returns the highest occupied Y for a column. If the heightmap does not exist,
    /// it is created and primed lazily, matching ChunkAccess.getHeight.
    /// X and Z are world coordinates; their low four bits select the local column.
    /// </summary>
    public int GetHeight(HeightmapType type, int x, int z)
    {
        if (!_heightmaps.TryGetValue(type, out Heightmap? heightmap))
        {
            PrimeHeightmaps(new[] { type });
            heightmap = _heightmaps[type];
        }

        return heightmap.GetFirstAvailable(x & SectionPos.SectionMask, z & SectionPos.SectionMask) - 1;
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
