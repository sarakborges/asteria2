using Asteria.Serialization;
using Asteria.World.LevelGen.Materials;

namespace Asteria.World.LevelGen.Materials.Conditions;

/// <summary>Tests whether the current material surface column is a hole.</summary>
public sealed class HoleCondition : MaterialCondition
{
    public static HoleCondition INSTANCE { get; } = new();
    public static MapCodec<HoleCondition> CODEC { get; } = MapCodec<HoleCondition>.Unit(INSTANCE);
    public static MapCodec<MaterialCondition> TYPE_CODEC { get; } =
        CODEC.XMap<MaterialCondition>(static condition => condition, static condition => (HoleCondition)condition);

    private HoleCondition() { }

    public ConditionEvaluator Compile(MaterialRuleContext context) =>
        context.LazyXZCondition(() => context.SurfaceDepth <= 0);

    public MapCodec<MaterialCondition> Codec() => TYPE_CODEC;
}
