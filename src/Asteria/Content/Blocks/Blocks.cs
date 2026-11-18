using Asteria.Content.Blocks.StateProperties;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;

namespace Asteria.Content.Blocks;

/// <summary>
/// Built-in Asteria block definitions.
/// TEST_BLOCK exists only to exercise block registration and block-state behaviour.
/// </summary>
public static class Blocks
{
    public static BooleanProperty TEST_POWERED { get; } =
        BooleanProperty.Create("powered");

    public static IntegerProperty TEST_STAGE { get; } =
        IntegerProperty.Create("stage", 0, 2);

    public static Block AIR { get; } = new(new BlockProperties(
        destroyTime: 0.0f,
        explosionResistance: 0.0f,
        isAir: true));

    public static Block TEST_BLOCK { get; } = new(
        new BlockProperties(
            destroyTime: 1.5f,
            explosionResistance: 6.0f),
        static builder =>
        {
            builder.Add(TEST_POWERED);
            builder.Add(TEST_STAGE);
            builder.SetDefault(TEST_POWERED, false);
            builder.SetDefault(TEST_STAGE, 1);
        });

    internal static void RegisterAll()
    {
        // Keep the default entry first, matching the registry's declared fallback.
        BuiltInRegistries.Block.Register(
            ResourceLocation.FromNamespaceAndPath("asteria", "air"),
            AIR);

        BuiltInRegistries.Block.Register(
            ResourceLocation.FromNamespaceAndPath("asteria", "registry_test"),
            TEST_BLOCK);
    }
}
