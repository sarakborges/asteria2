using Asteria.Serialization;
using Asteria.World.LevelGen.Materials;

namespace Asteria.World.LevelGen.Materials.Conditions;

/// <summary>Tests whether the current block Y has reached the preliminary surface level.</summary>
public sealed class AbovePreliminarySurfaceCondition : MaterialCondition
{
    public static AbovePreliminarySurfaceCondition INSTANCE { get; } = new();

    public static MapCodec<AbovePreliminarySurfaceCondition> CODEC { get; } =
        MapCodec<AbovePreliminarySurfaceCondition>.Unit(INSTANCE);

    public static MapCodec<MaterialCondition> TYPE_CODEC { get; } =
        CODEC.XMap<MaterialCondition>(
            static condition => condition,
            static condition => (AbovePreliminarySurfaceCondition)condition);

    private AbovePreliminarySurfaceCondition()
    {
    }

    public ConditionEvaluator Compile(MaterialRuleContext context) =>
        () => context.BlockY >= context.MinSurfaceLevel;

    public MapCodec<MaterialCondition> Codec() => TYPE_CODEC;
}
