using Asteria.Serialization;
using Asteria.World.LevelGen.Materials;

namespace Asteria.World.LevelGen.Materials.Conditions;

/// <summary>Checks block Y against a resolved vertical anchor and surface-depth offset.</summary>
public sealed record YCondition(
    VerticalAnchor Anchor,
    int SurfaceDepthMultiplier,
    bool AddStoneDepth) : MaterialCondition
{
    public static MapCodec<YCondition> CODEC { get; } =
        RecordCodecBuilder.MapCodec<YCondition>(instance =>
            instance.Group(
                    VerticalAnchor.CODEC.FieldOf("anchor")
                        .ForGetter<YCondition>(static condition => condition.Anchor),
                    Codecs.IntRange(-20, 20).FieldOf("surface_depth_multiplier")
                        .ForGetter<YCondition>(static condition => condition.SurfaceDepthMultiplier),
                    Codecs.BOOL.FieldOf("add_stone_depth")
                        .ForGetter<YCondition>(static condition => condition.AddStoneDepth))
                .Apply(
                    instance,
                    static (anchor, multiplier, addStoneDepth) =>
                        new YCondition(anchor, multiplier, addStoneDepth)));

    public static MapCodec<MaterialCondition> TYPE_CODEC { get; } =
        CODEC.XMap<MaterialCondition>(
            static condition => condition,
            static condition => (YCondition)condition);

    public ConditionEvaluator Compile(MaterialRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        int anchorY = context.ResolveAnchorY(Anchor);

        return context.LazyYCondition(() =>
            context.BlockY + (AddStoneDepth ? context.StoneDepthAbove : 0) >=
            anchorY + context.SurfaceDepth * SurfaceDepthMultiplier);
    }

    public MapCodec<MaterialCondition> Codec() => TYPE_CODEC;
}
