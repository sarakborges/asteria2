using Asteria.Serialization;
using Asteria.World.LevelGen.Materials;

namespace Asteria.World.LevelGen.Materials.Conditions;

/// <summary>Tests whether the current biome is cold enough to snow at the current position.</summary>
public sealed class TemperatureCondition : MaterialCondition
{
    public static TemperatureCondition INSTANCE { get; } = new();

    public static MapCodec<TemperatureCondition> CODEC { get; } =
        MapCodec<TemperatureCondition>.Unit(INSTANCE);

    public static MapCodec<MaterialCondition> TYPE_CODEC { get; } =
        CODEC.XMap<MaterialCondition>(
            static condition => condition,
            static condition => (TemperatureCondition)condition);

    private TemperatureCondition()
    {
    }

    public ConditionEvaluator Compile(MaterialRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return () => context.GetBiome().Value.ColdEnoughToSnow(
            new Asteria.Core.BlockPos(context.BlockX, context.BlockY, context.BlockZ),
            context.GetSeaLevel());
    }

    public MapCodec<MaterialCondition> Codec() => TYPE_CODEC;
}
