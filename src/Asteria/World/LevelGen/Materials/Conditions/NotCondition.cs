using Asteria.Serialization;
using Asteria.World.LevelGen.Materials;

namespace Asteria.World.LevelGen.Materials.Conditions;

/// <summary>Inverts another material condition.</summary>
public sealed record NotCondition(MaterialCondition Target) : MaterialCondition
{
    public static MapCodec<NotCondition> CODEC { get; } =
        MaterialCondition.CODEC.FieldOf("invert")
            .XMap<NotCondition>(static condition => new NotCondition(condition), static condition => condition.Target);

    public static MapCodec<MaterialCondition> TYPE_CODEC { get; } =
        CODEC.XMap<MaterialCondition>(static condition => condition, static condition => (NotCondition)condition);

    public ConditionEvaluator Compile(MaterialRuleContext context)
    {
        ConditionEvaluator target = Target.Compile(context);
        return () => !target();
    }

    public MapCodec<MaterialCondition> Codec() => TYPE_CODEC;
}
