using Asteria.Content.Blocks;
using Asteria.Serialization;
using Asteria.World.LevelGen.Materials;

namespace Asteria.World.LevelGen.Materials.Rules;

/// <summary>Tries material rules in order and returns the first non-null block state.</summary>
public sealed record SequenceRule(IReadOnlyList<MaterialRule> Sequence) : MaterialRule
{
    public static MapCodec<SequenceRule> CODEC { get; } =
        MaterialRule.CODEC.ListOf()
            .FieldOf("sequence")
            .XMap<SequenceRule>(
                static sequence => new SequenceRule(sequence),
                static rule => rule.Sequence);

    public static MapCodec<MaterialRule> TYPE_CODEC { get; } =
        CODEC.XMap<MaterialRule>(
            static rule => rule,
            static rule => (SequenceRule)rule);

    public RuleEvaluator Compile(MaterialRuleContext context)
    {
        if (Sequence.Count == 1)
            return Sequence[0].Compile(context);

        RuleEvaluator[] sequence = Sequence.Select(rule => rule.Compile(context)).ToArray();
        return (blockX, blockY, blockZ) =>
        {
            foreach (RuleEvaluator rule in sequence)
            {
                BlockState? state = rule(blockX, blockY, blockZ);
                if (state is not null)
                    return state;
            }

            return null;
        };
    }

    public MapCodec<MaterialRule> Codec() => TYPE_CODEC;
}
