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
        BlockProperties.Of().Air());

    public static Block TEST_BLOCK { get; } = new TestBlock();

    private sealed class TestBlock : Block
    {
        public TestBlock()
            : base(BlockProperties.Of().Strength(1.5f, 6.0f))
        {
            RegisterDefaultState(
                DefaultState
                    .SetValue(TEST_POWERED, false)
                    .SetValue(TEST_STAGE, 1));
        }

        protected override void CreateBlockStateDefinition(StateDefinitionBuilder builder)
        {
            builder.Add(TEST_POWERED);
            builder.Add(TEST_STAGE);
        }
    }

    internal static void RegisterAll()
    {
        Registry.Register(BuiltInRegistries.Block,
            ResourceLocation.FromNamespaceAndPath("asteria", "air"),
            AIR);

        Registry.Register(BuiltInRegistries.Block,
            ResourceLocation.FromNamespaceAndPath("asteria", "registry_test"),
            TEST_BLOCK);
    }
}
