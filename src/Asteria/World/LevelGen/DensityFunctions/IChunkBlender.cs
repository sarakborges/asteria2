namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// Chunk-scoped terrain blender consumed by Minecraft Java 26.3 blend_density.
/// The concrete implementation owns the old-chunk measurements and blending rules.
/// </summary>
public interface IChunkBlender
{
    bool IsEmpty { get; }

    float BlendDensity(int blockX, int blockY, int blockZ, float density);
}
