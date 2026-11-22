using Asteria.Content.Blocks;
using Asteria.Core;
using Asteria.Core.Shapes;

namespace Asteria.World.Lighting;

/// <summary>
/// Shared shape rules used by light propagation engines.
/// </summary>
public abstract class LightEngine
{
    public static VoxelShape GetOcclusionShape(BlockState state, Direction direction)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(direction);

        return !state.CanOcclude || !state.UseShapeForLightOcclusion
            ? Shapes.Empty()
            : state.GetFaceOcclusionShape(direction);
    }
}
