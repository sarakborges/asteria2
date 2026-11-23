using System.Diagnostics;
using Asteria.Content.Biomes;
using Asteria.Content.Blocks;
using Asteria.Content.Fluids;
using Asteria.Content.Registries;
using Asteria.Content.Chunks.Status;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Lighting;
using Asteria.World.BlockScan;
using Asteria.Util;

namespace Asteria.Content.Chunks;

/// <summary>
/// Shared chunk storage and heightmap state.
/// </summary>
public abstract class ChunkAccess : BlockGetter
{
    public const int NoFilledSection = -1;

    private readonly ChunkPos _chunkPos;
    private readonly LevelHeightAccessor _levelHeightAccessor;
    private readonly ChunkSection[] _sections;
    private readonly List<short>?[] _postProcessing;
    private volatile bool _unsaved;
    private volatile bool _isLightCorrect;
    private long _inhabitedTime;
    private readonly Dictionary<Heightmap.Types, Heightmap> _heightmaps = new();
    private readonly IReadOnlyCollection<KeyValuePair<Heightmap.Types, Heightmap>> _heightmapEntries;
    private readonly ChunkSkyLightSources _skyLightSources;

    public ChunkAccess(
        ChunkPos chunkPos,
        LevelHeightAccessor levelHeightAccessor,
        PalettedContainerFactory containerFactory,
        long inhabitedTime,
        ChunkSection?[]? sections = null)
    {
        ArgumentNullException.ThrowIfNull(levelHeightAccessor);
        ArgumentNullException.ThrowIfNull(containerFactory);

        _chunkPos = chunkPos;
        _levelHeightAccessor = levelHeightAccessor;
        _inhabitedTime = inhabitedTime;
        _sections = new ChunkSection[levelHeightAccessor.GetSectionsCount()];
        _postProcessing = new List<short>?[_sections.Length];
        _heightmapEntries = new HeightmapEntries(_heightmaps);
        _skyLightSources = new ChunkSkyLightSources(levelHeightAccessor);

        if (sections is not null)
        {
            if (_sections.Length == sections.Length)
            {
                Array.Copy(sections, _sections, _sections.Length);
            }
            else
            {
                Trace.TraceWarning(
                    "Could not set level chunk sections, array length is {0} instead of {1}",
                    sections.Length,
                    _sections.Length);
            }
        }

        ReplaceMissingSections(containerFactory, _sections);
    }

    private static void ReplaceMissingSections(
        PalettedContainerFactory containerFactory,
        ChunkSection?[] sections)
    {
        for (int index = 0; index < sections.Length; index++)
        {
            sections[index] ??= new ChunkSection(containerFactory);
        }
    }

    public ChunkPos GetPos() => _chunkPos;

    public virtual Holder<Biome> GetNoiseBiome(int quartX, int quartY, int quartZ)
    {
        int quartMinY = QuartPos.FromBlock(GetMinY());
        int quartMaxY = unchecked(quartMinY + QuartPos.FromBlock(GetHeight()) - 1);
        int clampedQuartY = Math.Clamp(quartY, quartMinY, quartMaxY);
        int sectionIndex = GetSectionIndex(QuartPos.ToBlock(clampedQuartY));
        return GetSection(sectionIndex).GetNoiseBiome(
            quartX & QuartPos.Mask,
            clampedQuartY & QuartPos.Mask,
            quartZ & QuartPos.Mask);
    }

    public void FillBiomesFromNoise(BiomeResolver biomeResolver)
    {
        ArgumentNullException.ThrowIfNull(biomeResolver);

        ChunkPos position = GetPos();
        int quartMinX = QuartPos.FromBlock(position.GetMinBlockX());
        int quartMinZ = QuartPos.FromBlock(position.GetMinBlockZ());
        LevelHeightAccessor heightAccessor = GetHeightAccessorForGeneration();

        for (int sectionY = heightAccessor.GetMinSectionY();
             sectionY <= heightAccessor.GetMaxSectionY();
             sectionY++)
        {
            ChunkSection section = GetSection(heightAccessor.GetSectionIndexFromSectionY(sectionY));
            int quartMinY = QuartPos.FromSection(sectionY);
            section.FillBiomesFromNoise(biomeResolver, quartMinX, quartMinY, quartMinZ);
        }
    }

    public LevelHeightAccessor GetHeightAccessorForGeneration() => this;

    public abstract BlockState GetBlockState(BlockPos position);

    public abstract FluidState GetFluidState(BlockPos position);

    /// <summary>
    /// Returns the backing array, matching ChunkAccess.getSections().
    /// Callers must not insert null entries.
    /// </summary>
    public ChunkSection[] GetSections() => _sections;

    public ChunkSection GetSection(int sectionIndex) =>
        _sections[sectionIndex];

    public void FindBlockLightSources(Action<BlockPos, BlockState> consumer)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        FindBlocks(static state => state.GetLightEmission() != 0, consumer);
    }

    public void FindBlocks(
        Predicate<BlockState> predicate,
        Action<BlockPos, BlockState> consumer)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(consumer);

        Func<BlockState, bool> sectionPredicate = state => predicate(state);
        BlockStateConsumer blockStateConsumer = (position, state) =>
        {
            consumer(position, state);
            return Continuation.Continue;
        };

        for (int sectionY = GetMinSectionY(); sectionY <= GetMaxSectionY(); sectionY++)
        {
            ChunkSection section = GetSection(GetSectionIndexFromSectionY(sectionY));
            if (!section.MaybeHas(sectionPredicate))
                continue;

            BlockPos origin = SectionPos.Of(_chunkPos, sectionY).Origin();
            BlockScanUtils.FindBlocksInSection(
                section,
                origin,
                0, 0, 0,
                ChunkSection.Width - 1,
                ChunkSection.Height - 1,
                ChunkSection.Depth - 1,
                predicate,
                blockStateConsumer);
        }
    }

    public void CollectBiomesInPalette(ISet<Holder<Biome>> output)
    {
        ArgumentNullException.ThrowIfNull(output);

        foreach (ChunkSection section in _sections)
        {
            section.GetBiomes().ForEachInPalette(biome =>
            {
                output.Add(biome);
            });
        }
    }

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

    public IReadOnlyCollection<KeyValuePair<Heightmap.Types, Heightmap>> GetHeightmaps() =>
        _heightmapEntries;

    /// <summary>
    /// Loads serialized heightmap words. An incompatible word count falls back to a fresh
    /// priming pass, matching ChunkAccess.setHeightmap and Heightmap.setRawData.
    /// </summary>
    public void SetHeightmap(Heightmap.Types type, ulong[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        GetOrCreateHeightmapUnprimed(type).SetRawData(this, type, data);
    }

    /// <summary>
    /// Gets an existing heightmap or creates an unprimed one, matching ChunkAccess.
    /// </summary>
    public Heightmap GetOrCreateHeightmapUnprimed(Heightmap.Types type)
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
    public bool HasPrimedHeightmap(Heightmap.Types type) =>
        _heightmaps.ContainsKey(type);

    /// <summary>
    /// Returns the highest occupied Y for a column. If the heightmap does not exist,
    /// it is created and primed lazily, matching ChunkAccess.getHeight.
    /// X and Z are world coordinates; their low four bits select the local column.
    /// </summary>
    public int GetHeight(Heightmap.Types type, int x, int z)
    {
        if (!_heightmaps.TryGetValue(type, out Heightmap? heightmap))
        {
            Heightmap.PrimeHeightmaps(this, new HashSet<Heightmap.Types> { type });
            heightmap = _heightmaps[type];
        }

        return heightmap.GetFirstAvailable(x & SectionPos.SectionMask, z & SectionPos.SectionMask) - 1;
    }

    public abstract ChunkStatus GetPersistedStatus();

    /// <summary>
    /// Returns the furthest status represented by the chunk. Without below-zero retrogen data,
    /// this is the persisted status, matching Minecraft Java 26.3.
    /// </summary>
    public virtual ChunkStatus GetHighestGeneratedStatus() =>
        GetPersistedStatus();


    /// <summary>
    /// Returns the backing per-section post-processing array, matching ChunkAccess.getPostProcessing().
    /// </summary>
    public List<short>?[] GetPostProcessing() => _postProcessing;

    public virtual void MarkPosForPostProcessing(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        Trace.TraceWarning(
            "Trying to mark a block for post processing @ {0}, but this operation is not supported.",
            position);
    }

    public virtual void AddPackedPostProcess(IEnumerable<short> packedOffsets, int sectionIndex)
    {
        ArgumentNullException.ThrowIfNull(packedOffsets);
        GetOrCreateOffsetList(_postProcessing, sectionIndex).AddRange(packedOffsets);
    }

    protected static List<short> GetOrCreateOffsetList(
        List<short>?[] postProcessing,
        int sectionIndex)
    {
        ArgumentNullException.ThrowIfNull(postProcessing);
        return postProcessing[sectionIndex] ??= new List<short>();
    }

    public void MarkUnsaved() => _unsaved = true;

    public bool TryMarkSaved()
    {
        if (_unsaved)
        {
            _unsaved = false;
            return true;
        }

        return false;
    }

    public bool IsUnsaved() => _unsaved;

    public long GetInhabitedTime() => _inhabitedTime;

    public void IncrementInhabitedTime() =>
        _inhabitedTime = unchecked(_inhabitedTime + 1);

    public void SetInhabitedTime(long inhabitedTime) =>
        _inhabitedTime = inhabitedTime;

    public bool IsLightCorrect() => _isLightCorrect;

    public void InitializeLightSources() =>
        _skyLightSources.FillFrom(this);

    public ChunkSkyLightSources GetSkyLightSources() =>
        _skyLightSources;

    public void SetLightCorrect(bool isLightCorrect)
    {
        _isLightCorrect = isLightCorrect;
        MarkUnsaved();
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
    private sealed class HeightmapEntries : IReadOnlyCollection<KeyValuePair<Heightmap.Types, Heightmap>>
    {
        private readonly Dictionary<Heightmap.Types, Heightmap> _heightmaps;

        public HeightmapEntries(Dictionary<Heightmap.Types, Heightmap> heightmaps)
        {
            _heightmaps = heightmaps;
        }

        public int Count => _heightmaps.Count;

        public IEnumerator<KeyValuePair<Heightmap.Types, Heightmap>> GetEnumerator() =>
            _heightmaps.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }

}
