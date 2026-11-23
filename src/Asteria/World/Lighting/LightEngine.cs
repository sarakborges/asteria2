using Asteria.Content.Blocks;
using Asteria.Core;
using Asteria.Core.Shapes;

namespace Asteria.World.Lighting;

/// <summary>
/// Shared shape rules used by light propagation engines.
/// </summary>
public abstract class LightEngine
{
    public abstract void CheckBlock(BlockPos position);

    public abstract void UpdateSectionStatus(SectionPos sectionPosition, bool sectionEmpty);

    public static bool HasDifferentLightProperties(BlockState oldState, BlockState newState)
    {
        ArgumentNullException.ThrowIfNull(oldState);
        ArgumentNullException.ThrowIfNull(newState);

        if (ReferenceEquals(newState, oldState))
            return false;

        return newState.GetLightDampening() != oldState.GetLightDampening() ||
            newState.GetLightEmission() != oldState.GetLightEmission() ||
            newState.UseShapeForLightOcclusion ||
            oldState.UseShapeForLightOcclusion;
    }

    public static VoxelShape GetOcclusionShape(BlockState state, Direction direction)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(direction);

        return !state.CanOcclude || !state.UseShapeForLightOcclusion
            ? Shapes.Empty()
            : state.GetFaceOcclusionShape(direction);
    }
}
