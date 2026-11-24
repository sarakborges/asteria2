using Asteria.Content.Chunks;
using Asteria.Core;

namespace Asteria.World.Lighting;

/// <summary>
/// Coordinates the block and sky light engines for one level.
/// Port of Minecraft Java 26.3 LevelLightEngine.
/// </summary>
public sealed class LevelLightEngine : LightEventListener
{
    public const int LIGHT_SECTION_PADDING = 1;

    public static readonly LevelLightEngine EMPTY = new();

    private readonly LevelHeightAccessor _levelHeightAccessor;
    private readonly LightEngine? _blockEngine;
    private readonly LightEngine? _skyEngine;

    public LevelLightEngine(
        LightChunkGetter chunkSource,
        bool hasBlockLight,
        bool hasSkyLight)
    {
        ArgumentNullException.ThrowIfNull(chunkSource);

        _levelHeightAccessor = chunkSource.GetLevel();
        _blockEngine = hasBlockLight ? new BlockLightEngine(chunkSource) : null;
        _skyEngine = hasSkyLight ? new SkyLightEngine(chunkSource) : null;
    }

    private LevelLightEngine()
    {
        _levelHeightAccessor = LevelHeightAccessor.Create(0, 0);
        _blockEngine = null;
        _skyEngine = null;
    }

    public void CheckBlock(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        _blockEngine?.CheckBlock(position);
        _skyEngine?.CheckBlock(position);
    }

    public bool HasLightWork() =>
        (_skyEngine?.HasLightWork() ?? false) ||
        (_blockEngine?.HasLightWork() ?? false);

    public int RunLightUpdates()
    {
        int count = 0;
        if (_blockEngine is not null)
            count += _blockEngine.RunLightUpdates();
        if (_skyEngine is not null)
            count += _skyEngine.RunLightUpdates();

        return count;
    }

    public void UpdateSectionStatus(SectionPos position, bool sectionEmpty)
    {
        ArgumentNullException.ThrowIfNull(position);
        _blockEngine?.UpdateSectionStatus(position, sectionEmpty);
        _skyEngine?.UpdateSectionStatus(position, sectionEmpty);
    }

    public void SetLightEnabled(ChunkPos position, bool enabled)
    {
        _blockEngine?.SetLightEnabled(position, enabled);
        _skyEngine?.SetLightEnabled(position, enabled);
    }

    public void PropagateLightSources(ChunkPos position)
    {
        _blockEngine?.PropagateLightSources(position);
        _skyEngine?.PropagateLightSources(position);
    }

    public LayerLightEventListener GetLayerListener(LightLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (layer == LightLayer.BLOCK)
            return _blockEngine?.GetLayerListener() ?? LayerLightEventListener.Dummy.Instance;

        return _skyEngine?.GetLayerListener() ?? LayerLightEventListener.Dummy.Instance;
    }

    public string GetDebugData(LightLayer layer, SectionPos position)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(position);

        if (ReferenceEquals(layer, LightLayer.BLOCK))
            return _blockEngine?.GetDebugData(position.AsLong()) ?? "n/a";

        return _skyEngine?.GetDebugData(position.AsLong()) ?? "n/a";
    }

    public LightSectionType GetDebugSectionType(LightLayer layer, SectionPos position)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(position);

        if (ReferenceEquals(layer, LightLayer.BLOCK))
            return _blockEngine?.GetDebugSectionType(position.AsLong()) ?? LightSectionType.Empty;

        return _skyEngine?.GetDebugSectionType(position.AsLong()) ?? LightSectionType.Empty;
    }

    public void QueueSectionData(LightLayer layer, SectionPos position, DataLayer? data)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(position);

        if (ReferenceEquals(layer, LightLayer.BLOCK))
            _blockEngine?.QueueSectionData(position.AsLong(), data);
        else
            _skyEngine?.QueueSectionData(position.AsLong(), data);
    }

    public void RetainData(ChunkPos position, bool retain)
    {
        _blockEngine?.RetainData(position, retain);
        _skyEngine?.RetainData(position, retain);
    }

    public int GetRawBrightness(BlockPos position, int skyDampen)
    {
        ArgumentNullException.ThrowIfNull(position);

        int skyLight = (_skyEngine?.GetLightValue(position) ?? 0) - skyDampen;
        int blockLight = _blockEngine?.GetLightValue(position) ?? 0;
        return Math.Max(blockLight, skyLight);
    }

    public bool LightOnInColumn(long sectionZeroNode) =>
        _blockEngine is null ||
        _blockEngine.LightOnInColumn(sectionZeroNode) &&
        (_skyEngine is null || _skyEngine.LightOnInColumn(sectionZeroNode));

    public int GetLightSectionCount() =>
        unchecked(_levelHeightAccessor.GetSectionsCount() + 2);

    public int GetMinLightSection() =>
        unchecked(_levelHeightAccessor.GetMinSectionY() - LIGHT_SECTION_PADDING);

    public int GetMaxLightSection() =>
        unchecked(GetMinLightSection() + GetLightSectionCount());
}
