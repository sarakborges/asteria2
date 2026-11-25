using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World;

namespace Asteria.World.LevelGen.Structure.Placement;

/// <summary>
/// Shared frequency, salt, locate offset and exclusion-zone behavior from
/// Minecraft Java 26.3 AbstractSpreadingStructurePlacement.
/// </summary>
public abstract class AbstractSpreadingStructurePlacement : StructurePlacement
{
    protected const int HIGHLY_ARBITRARY_RANDOM_SALT = 10387320;

    private readonly Vec3i _locateOffset;
    private readonly FrequencyReductionMethod _frequencyReductionMethod;
    private readonly float _frequency;
    private readonly int _salt;
    private readonly ExclusionZone? _exclusionZone;

    protected AbstractSpreadingStructurePlacement(
        Vec3i locateOffset,
        FrequencyReductionMethod frequencyReductionMethod,
        float frequency,
        int salt,
        ExclusionZone? exclusionZone)
    {
        _locateOffset = locateOffset;
        _frequencyReductionMethod = frequencyReductionMethod;
        _frequency = frequency;
        _salt = salt;
        _exclusionZone = exclusionZone;
    }

    public Vec3i LocateOffset() =>
        _locateOffset;

    public FrequencyReductionMethod FrequencyReductionMethod() =>
        _frequencyReductionMethod;

    public float Frequency() =>
        _frequency;

    public int Salt() =>
        _salt;

    public ExclusionZone? ExclusionZoneValue() =>
        _exclusionZone;

    public bool IsStructureChunk(
        IStructurePlacementState state,
        int sourceX,
        int sourceZ)
    {
        ArgumentNullException.ThrowIfNull(state);

        return IsPlacementChunk(state, sourceX, sourceZ) &&
               ApplyAdditionalChunkRestrictions(sourceX, sourceZ, state.LevelSeed) &&
               ApplyInteractionsWithOtherStructures(state, sourceX, sourceZ);
    }

    public bool ApplyAdditionalChunkRestrictions(
        int sourceX,
        int sourceZ,
        long levelSeed) =>
        !(_frequency < 1.0F) ||
        _frequencyReductionMethod.ShouldGenerate(
            levelSeed,
            _salt,
            sourceX,
            sourceZ,
            _frequency);

    public bool ApplyInteractionsWithOtherStructures(
        IStructurePlacementState state,
        int sourceX,
        int sourceZ)
    {
        ArgumentNullException.ThrowIfNull(state);

        return _exclusionZone is null ||
               !_exclusionZone.IsPlacementForbidden(
                   state,
                   sourceX,
                   sourceZ);
    }

    public BlockPos GetLocatePos(ChunkPos chunkPos)
    {
        BlockPos origin = new(
            chunkPos.GetMinBlockX(),
            0,
            chunkPos.GetMinBlockZ());

        return origin.Offset(_locateOffset);
    }

    protected abstract bool IsPlacementChunk(
        IStructurePlacementState state,
        int sourceX,
        int sourceZ);

    public sealed record ExclusionZone(
        Holder<StructureSet> OtherSet,
        int ChunkCount)
    {
        public bool IsPlacementForbidden(
            IStructurePlacementState state,
            int sourceX,
            int sourceZ)
        {
            ArgumentNullException.ThrowIfNull(state);
            return state.HasStructureChunkInRange(
                OtherSet,
                sourceX,
                sourceZ,
                ChunkCount);
        }
    }
}
