using Asteria.Content.Blocks;
using Asteria.Serialization;
using Asteria.World.LevelGen.Materials;

namespace Asteria.World.LevelGen.Materials.Rules;

/// <summary>A material rule that unconditionally selects one block state.</summary>
public sealed record BlockRule(BlockState ResultState) : MaterialRule, RuleEvaluator
{
    public static MapCodec<BlockRule> CODEC { get; } =
        RecordCodecBuilder.MapCodec<BlockRule>(instance =>
            instance.Group(
                    BlockState.CODEC.FieldOf("result_state")
                        .ForGetter<BlockRule>(static rule => rule.ResultState))
                .Apply(instance, static state => new BlockRule(state)));

    public static MapCodec<MaterialRule> TYPE_CODEC { get; } =
        CODEC.XMap<MaterialRule>(
            static rule => rule,
            static rule => (BlockRule)rule);

    public RuleEvaluator Compile(MaterialRuleContext context) => this;

    public MapCodec<MaterialRule> Codec() => TYPE_CODEC;

    public BlockState? TryApply(int blockX, int blockY, int blockZ) => ResultState;
}
