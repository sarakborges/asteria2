using Asteria.Serialization;
using Asteria.World.LevelGen.Materials;

namespace Asteria.World.LevelGen.Materials.Conditions;

/// <summary>Tests the vanilla material surface-gradient steepness threshold.</summary>
public sealed class SteepCondition : MaterialCondition
{
    public static SteepCondition INSTANCE { get; } = new();
    public static MapCodec<SteepCondition> CODEC { get; } = MapCodec<SteepCondition>.Unit(INSTANCE);
    public static MapCodec<MaterialCondition> TYPE_CODEC { get; } =
        CODEC.XMap<MaterialCondition>(static condition => condition, static condition => (SteepCondition)condition);

    private SteepCondition() { }

    public ConditionEvaluator Compile(MaterialRuleContext context) =>
        context.LazyXZCondition(() => context.SurfaceGradientX <= -4 || context.SurfaceGradientZ >= 4);

    public MapCodec<MaterialCondition> Codec() => TYPE_CODEC;
}
