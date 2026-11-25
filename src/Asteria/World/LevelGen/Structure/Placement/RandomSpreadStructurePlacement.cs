using Asteria.Core;
using Asteria.World;

namespace Asteria.World.LevelGen.Structure.Placement;

/// <summary>
/// Candidate-chunk calculation from Minecraft Java 26.3 RandomSpreadStructurePlacement.
/// Placement-state filtering and codecs are implemented separately from this pure calculation.
/// </summary>
public sealed class RandomSpreadStructurePlacement : AbstractSpreadingStructurePlacement
{
    private readonly int _spacing;
    private readonly int _separation;
    private readonly RandomSpreadType _spreadType;
    public RandomSpreadStructurePlacement(
        int spacing,
        int separation,
        RandomSpreadType spreadType,
        int salt)
        : this(
            Vec3i.Zero,
            global::Asteria.World.LevelGen.Structure.Placement.FrequencyReductionMethod.DEFAULT,
            1.0F,
            salt,
            null,
            spacing,
            separation,
            spreadType)
    {
    }

    public RandomSpreadStructurePlacement(
        Vec3i locateOffset,
        FrequencyReductionMethod frequencyReductionMethod,
        float frequency,
        int salt,
        ExclusionZone? exclusionZone,
        int spacing,
        int separation,
        RandomSpreadType spreadType)
        : base(
            locateOffset,
            frequencyReductionMethod,
            frequency,
            salt,
            exclusionZone)
    {
        _spacing = spacing;
        _separation = separation;
        _spreadType = spreadType ??
            throw new ArgumentNullException(nameof(spreadType));
    }

    public int Spacing() =>
        _spacing;

    public int Separation() =>
        _separation;

    public RandomSpreadType SpreadType() =>
        _spreadType;

    /// <summary>
    /// Calculates the sole potential structure chunk for the spacing grid cell
    /// containing the supplied source coordinates. Parameter validation belongs
    /// to the codec, as in the source; this constructor does not clamp or rewrite values.
    /// </summary>
    protected override bool IsPlacementChunk(
        IStructurePlacementState state,
        int sourceX,
        int sourceZ)
    {
        ChunkPos candidate = GetPotentialStructureChunk(
            state.LevelSeed,
            sourceX,
            sourceZ);

        return candidate.X == sourceX && candidate.Z == sourceZ;
    }

    public ChunkPos GetPotentialStructureChunk(
        long seed,
        int sourceX,
        int sourceZ)
    {
        int spacedGridX = Mth.FloorDiv(sourceX, _spacing);
        int spacedGridZ = Mth.FloorDiv(sourceZ, _spacing);

        var random = new WorldgenRandom(new LegacyRandomSource(0L));
        random.SetLargeFeatureWithSalt(seed, spacedGridX, spacedGridZ, Salt());

        int limit = _spacing - _separation;
        int spreadX = _spreadType.Evaluate(random, limit);
        int spreadZ = _spreadType.Evaluate(random, limit);

        return new ChunkPos(
            unchecked(spacedGridX * _spacing + spreadX),
            unchecked(spacedGridZ * _spacing + spreadZ));
    }
}
