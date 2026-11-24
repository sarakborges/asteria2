using Asteria.Content.Blocks;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Level;
using Asteria.World.LevelGen;

namespace Asteria.World.LevelGen.Placement;

/// <summary>
/// Context exposed to placement modifiers. The optional top feature is present only
/// when a placed feature is entered through biome-checked placement.
/// </summary>
public sealed class PlacementContext
{
    public OverworldWorld Level { get; }
    public NoiseBasedChunkGenerator Generator { get; }
    public PlacedFeature? TopFeature { get; }
    public WorldGenerationContext GenerationContext { get; }

    public PlacementContext(
        OverworldWorld level,
        NoiseBasedChunkGenerator generator,
        PlacedFeature? topFeature)
    {
        Level = level ?? throw new ArgumentNullException(nameof(level));
        Generator = generator ?? throw new ArgumentNullException(nameof(generator));
        TopFeature = topFeature;
        GenerationContext = new WorldGenerationContext(
            Generator,
            LevelHeightAccessor.Create(Level.GetMinY(), Level.GetHeight()));
    }

    public int GetMinY() => Level.GetMinY();

    public int GetHeight() => Level.GetHeight();

    public int GetHeight(Heightmap.Types type, int blockX, int blockZ) =>
        Level.GetHeight(type, blockX, blockZ);

    public BlockState GetBlockState(BlockPos position) =>
        Level.GetBlockState(position);
}
