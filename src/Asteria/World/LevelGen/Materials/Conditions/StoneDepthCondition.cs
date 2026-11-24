using Asteria.Core;
using Asteria.Serialization;
using Asteria.World.LevelGen.Materials;
using Asteria.World.LevelGen.Placement;

namespace Asteria.World.LevelGen.Materials.Conditions;

/// <summary>Tests terrain depth from the selected cave surface using Minecraft Java 26.3 semantics.</summary>
public sealed record StoneDepthCondition(
    int Offset,
    bool AddSurfaceDepth,
    int SecondaryDepthRange,
    CaveSurface SurfaceType) : MaterialCondition
{
    public static MapCodec<StoneDepthCondition> CODEC { get; } =
        RecordCodecBuilder.MapCodec<StoneDepthCondition>(instance =>
            instance.Group(
                    Codecs.INT.FieldOf("offset")
                        .ForGetter<StoneDepthCondition>(static condition => condition.Offset),
                    Codecs.BOOL.FieldOf("add_surface_depth")
                        .ForGetter<StoneDepthCondition>(static condition => condition.AddSurfaceDepth),
                    Codecs.INT.FieldOf("secondary_depth_range")
                        .ForGetter<StoneDepthCondition>(static condition => condition.SecondaryDepthRange),
                    CaveSurfaceCodecs.CODEC.FieldOf("surface_type")
                        .ForGetter<StoneDepthCondition>(static condition => condition.SurfaceType))
                .Apply(
                    instance,
                    static (offset, addSurfaceDepth, secondaryDepthRange, surfaceType) =>
                        new StoneDepthCondition(offset, addSurfaceDepth, secondaryDepthRange, surfaceType)));

    public static MapCodec<MaterialCondition> TYPE_CODEC { get; } =
        CODEC.XMap<MaterialCondition>(
            static condition => condition,
            static condition => (StoneDepthCondition)condition);

    public ConditionEvaluator Compile(MaterialRuleContext context)
    {
        bool ceiling = SurfaceType == CaveSurface.CEILING;
        return context.LazyYCondition(() =>
        {
            int stoneDepth = ceiling ? context.StoneDepthBelow : context.StoneDepthAbove;
            int surfaceDepth = AddSurfaceDepth ? context.SurfaceDepth : 0;
            int secondarySurfaceDepth = SecondaryDepthRange == 0
                ? 0
                : (int)Mth.Map(
                    context.SurfaceSecondary,
                    -1.0,
                    1.0,
                    0.0,
                    SecondaryDepthRange);

            return stoneDepth <= 1 + Offset + surfaceDepth + secondarySurfaceDepth;
        });
    }

    public MapCodec<MaterialCondition> Codec() => TYPE_CODEC;
}
