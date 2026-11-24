using Asteria.Serialization;
using Asteria.World.LevelGen.Materials;
using Asteria.World.LevelGen.Materials.Conditions;

namespace Asteria.World.LevelGen.Materials.Rules;

/// <summary>Applies a material rule only while its material condition evaluates true.</summary>
public sealed record ConditionRule(MaterialCondition IfTrue, MaterialRule ThenRun) : MaterialRule
{
    public static MapCodec<ConditionRule> CODEC { get; } =
        RecordCodecBuilder.MapCodec<ConditionRule>(instance =>
            instance.Group(
                    MaterialCondition.CODEC.FieldOf("if_true")
                        .ForGetter<ConditionRule>(static rule => rule.IfTrue),
                    MaterialRule.CODEC.FieldOf("then_run")
                        .ForGetter<ConditionRule>(static rule => rule.ThenRun))
                .Apply(instance, static (condition, rule) => new ConditionRule(condition, rule)));

    public static MapCodec<MaterialRule> TYPE_CODEC { get; } =
        CODEC.XMap<MaterialRule>(static rule => rule, static rule => (ConditionRule)rule);

    public RuleEvaluator Compile(MaterialRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Conditions.ConditionEvaluator condition = IfTrue.Compile(context);
        RuleEvaluator rule = ThenRun.Compile(context);
        return (blockX, blockY, blockZ) => !condition() ? null : rule(blockX, blockY, blockZ);
    }

    public MapCodec<MaterialRule> Codec() => TYPE_CODEC;
}
