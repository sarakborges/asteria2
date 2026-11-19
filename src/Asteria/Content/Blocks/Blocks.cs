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

    public static Block AIR { get; } = new(
        new BlockProperties(
            destroyTime: 0.0f,
            explosionResistance: 0.0f,
            isAir: true),
        createIntrusiveRegistryHolder: true);

    public static Block TEST_BLOCK { get; } = CreateTestBlock();

    private static Block CreateTestBlock()
    {
        var block = new Block(
            new BlockProperties(
                destroyTime: 1.5f,
                explosionResistance: 6.0f),
            static builder =>
            {
                builder.Add(TEST_POWERED);
                builder.Add(TEST_STAGE);
            },
            createIntrusiveRegistryHolder: true);

        block.RegisterDefaultState(
            block.DefaultState
                .SetValue(TEST_POWERED, false)
                .SetValue(TEST_STAGE, 1));

        return block;
    }

    internal static void RegisterAll()
    {
        BuiltInRegistries.Block.Register(
            ResourceLocation.FromNamespaceAndPath("asteria", "air"),
            AIR);

        BuiltInRegistries.Block.Register(
            ResourceLocation.FromNamespaceAndPath("asteria", "registry_test"),
            TEST_BLOCK);
    }
}
