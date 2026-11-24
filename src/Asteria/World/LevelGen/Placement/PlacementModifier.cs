using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World.LevelGen;

namespace Asteria.World.LevelGen.Placement;

/// <summary>
/// Transforms one origin position into zero or more positions for a placed feature,
/// using the ordered placement chain from Minecraft Java 26.3.
/// </summary>
public interface PlacementModifier
{
    void Modify(
        PlacementContext context,
        IRandomSource random,
        BlockPos origin,
        Action<BlockPos> output);

    MapCodec<PlacementModifier> Codec();
}
