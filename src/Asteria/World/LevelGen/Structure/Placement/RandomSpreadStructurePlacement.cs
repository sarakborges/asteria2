using Asteria.Core;
using Asteria.World;

namespace Asteria.World.LevelGen.Structure.Placement;

/// <summary>
/// Candidate-chunk calculation from Minecraft Java 26.3 RandomSpreadStructurePlacement.
/// Placement-state filtering and codecs are implemented separately from this pure calculation.
/// </summary>
public sealed class RandomSpreadStructurePlacement
{
    private readonly int _spacing;
    private readonly int _separation;
    private readonly RandomSpreadType _spreadType;
    private readonly int _salt;

    public RandomSpreadStructurePlacement(
        int spacing,
        int separation,
        RandomSpreadType spreadType,
        int salt)
    {
        _spacing = spacing;
        _separation = separation;
        _spreadType = spreadType ??
            throw new ArgumentNullException(nameof(spreadType));
        _salt = salt;
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
    public ChunkPos GetPotentialStructureChunk(
        long seed,
        int sourceX,
        int sourceZ)
    {
        int spacedGridX = Mth.FloorDiv(sourceX, _spacing);
        int spacedGridZ = Mth.FloorDiv(sourceZ, _spacing);

        var random = new WorldgenRandom(new LegacyRandomSource(0L));
        random.SetLargeFeatureWithSalt(seed, spacedGridX, spacedGridZ, _salt);

        int limit = _spacing - _separation;
        int spreadX = _spreadType.Evaluate(random, limit);
        int spreadZ = _spreadType.Evaluate(random, limit);

        return new ChunkPos(
            unchecked(spacedGridX * _spacing + spreadX),
            unchecked(spacedGridZ * _spacing + spreadZ));
    }
}
