using Asteria.Serialization;
using Asteria.World.LevelGen.Materials;

namespace Asteria.World.LevelGen.Materials.Conditions;

/// <summary>Tests the vanilla water-height relation for a material-rule position.</summary>
public sealed record WaterCondition(
    int Offset,
    int SurfaceDepthMultiplier,
    bool AddStoneDepth) : MaterialCondition
{
    public static MapCodec<WaterCondition> CODEC { get; } =
        RecordCodecBuilder.MapCodec<WaterCondition>(instance =>
            instance.Group(
                    Codecs.INT.FieldOf("offset")
                        .ForGetter<WaterCondition>(static condition => condition.Offset),
                    Codecs.IntRange(-20, 20).FieldOf("surface_depth_multiplier")
                        .ForGetter<WaterCondition>(static condition => condition.SurfaceDepthMultiplier),
                    Codecs.BOOL.FieldOf("add_stone_depth")
                        .ForGetter<WaterCondition>(static condition => condition.AddStoneDepth))
                .Apply(
                    instance,
                    static (offset, surfaceDepthMultiplier, addStoneDepth) =>
                        new WaterCondition(offset, surfaceDepthMultiplier, addStoneDepth)));

    public static MapCodec<MaterialCondition> TYPE_CODEC { get; } =
        CODEC.XMap<MaterialCondition>(
            static condition => condition,
            static condition => (WaterCondition)condition);

    public ConditionEvaluator Compile(MaterialRuleContext context) =>
        context.LazyYCondition(() =>
            context.WaterHeight == int.MinValue ||
            context.BlockY + (AddStoneDepth ? context.StoneDepthAbove : 0) >=
            context.WaterHeight + Offset + context.SurfaceDepth * SurfaceDepthMultiplier);

    public MapCodec<MaterialCondition> Codec() => TYPE_CODEC;
}
