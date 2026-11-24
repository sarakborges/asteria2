using Asteria.Core;
using Asteria.World.Level;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Features;

namespace Asteria.World.LevelGen.Placement;

/// <summary>
/// Executes a placed feature's ordered modifier chain using the same depth-first,
/// last-position-first stack behavior as Minecraft Java 26.3 FeaturePlacer.
/// </summary>
public sealed class FeaturePlacer
{
    private readonly OverworldWorld _level;
    private readonly NoiseBasedChunkGenerator _generator;
    private readonly List<BlockPos> _positions = new();
    private readonly List<int> _modifierIndices = new();
    private readonly List<BlockPos> _modifiedPositions = new();

    public FeaturePlacer(OverworldWorld level, NoiseBasedChunkGenerator generator)
    {
        _level = level ?? throw new ArgumentNullException(nameof(level));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
    }

    public bool Place(
        PlacedFeature placedFeature,
        IRandomSource random,
        BlockPos origin) =>
        PlaceInternal(placedFeature, random, origin, biomeCheck: false);

    public bool PlaceWithBiomeCheck(
        PlacedFeature placedFeature,
        IRandomSource random,
        BlockPos origin) =>
        PlaceInternal(placedFeature, random, origin, biomeCheck: true);

    private bool PlaceInternal(
        PlacedFeature placedFeature,
        IRandomSource random,
        BlockPos origin,
        bool biomeCheck)
    {
        ArgumentNullException.ThrowIfNull(placedFeature);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(origin);

        if (_positions.Count != 0 || _modifierIndices.Count != 0 || _modifiedPositions.Count != 0)
            throw new InvalidOperationException("FeaturePlacer cannot be entered recursively.");

        Feature feature = placedFeature.Feature.Value;
        IReadOnlyList<PlacementModifier> placement = placedFeature.Placement;
        if (placement.Count == 0)
            return feature.Place(_level, _generator, random, origin);

        var context = new PlacementContext(
            _level,
            _generator,
            biomeCheck ? placedFeature : null);

        bool placedAny = false;
        _positions.Add(origin);
        _modifierIndices.Add(0);

        for (; _positions.Count > 0; _modifiedPositions.Clear())
        {
            int lastPositionIndex = _positions.Count - 1;
            BlockPos position = _positions[lastPositionIndex];
            _positions.RemoveAt(lastPositionIndex);

            int lastModifierIndex = _modifierIndices.Count - 1;
            int modifierIndex = _modifierIndices[lastModifierIndex];
            _modifierIndices.RemoveAt(lastModifierIndex);

            PlacementModifier modifier = placement[modifierIndex];
            modifier.Modify(context, random, position, _modifiedPositions.Add);

            int nextModifierIndex = modifierIndex + 1;
            if (nextModifierIndex < placement.Count)
            {
                for (int index = _modifiedPositions.Count - 1; index >= 0; index--)
                {
                    _positions.Add(_modifiedPositions[index]);
                    _modifierIndices.Add(nextModifierIndex);
                }
            }
            else
            {
                foreach (BlockPos nextPosition in _modifiedPositions)
                    placedAny |= feature.Place(_level, _generator, random, nextPosition);
            }
        }

        return placedAny;
    }
}
