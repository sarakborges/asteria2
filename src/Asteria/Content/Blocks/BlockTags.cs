using Asteria.Content.Identifiers;
using Asteria.Content.Registries;

namespace Asteria.Content.Blocks;

/// <summary>
/// Built-in block tags consumed by chunk and terrain systems.
/// Tag values are populated by the registry tag-reload lifecycle.
/// </summary>
public static class BlockTags
{
    public static TagKey<Block> BlocksMotionInHeightmap { get; } =
        Create("blocks_motion_in_heightmap");

    public static TagKey<Block> BlocksMotionInHeightmapNoLeaves { get; } =
        Create("blocks_motion_in_heightmap_no_leaves");

    public static TagKey<Block> Leaves { get; } =
        Create("leaves");

    public static TagKey<Block> FallDamageResetting { get; } =
        Create("fall_damage_resetting");

    public static TagKey<Block> CausesSuffocation { get; } =
        Create("causes_suffocation");

    private static TagKey<Block> Create(string path) =>
        TagKey<Block>.Create(
            BuiltInRegistries.Block.Key,
            ResourceLocation.WithDefaultNamespace(path));
}
