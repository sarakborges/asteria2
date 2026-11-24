using Asteria.Content.Blocks;
using Asteria.Serialization;
using Asteria.World.LevelGen.Materials;

namespace Asteria.World.LevelGen.Materials.Rules;

/// <summary>A material rule that unconditionally selects one block state.</summary>
public sealed record BlockRule(BlockState ResultState) : MaterialRule
{
    public static MapCodec<BlockRule> CODEC { get; } =
        BlockState.CODEC.FieldOf("result_state")
            .XMap<BlockRule>(
                static state => new BlockRule(state),
                static rule => rule.ResultState);

    public static MapCodec<MaterialRule> TYPE_CODEC { get; } =
        CODEC.XMap<MaterialRule>(
            static rule => rule,
            static rule => (BlockRule)rule);

    public RuleEvaluator Compile(MaterialRuleContext context) => TryApply;

    public MapCodec<MaterialRule> Codec() => TYPE_CODEC;

    public BlockState? TryApply(int blockX, int blockY, int blockZ) => ResultState;
}
