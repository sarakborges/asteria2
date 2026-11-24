using Asteria.Content.Identifiers;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World.LevelGen.Materials;

namespace Asteria.World.LevelGen.Materials.Conditions;

/// <summary>Deterministic randomized gradient between two resolved vertical anchors.</summary>
public sealed record VerticalGradientCondition(
    ResourceLocation RandomName,
    VerticalAnchor TrueAtAndBelow,
    VerticalAnchor FalseAtAndAbove) : MaterialCondition
{
    private static Codec<ResourceLocation> ResourceLocationCodec { get; } =
        Codecs.STRING.FlatXMap(
            static text => ResourceLocation.TryParse(text) is ResourceLocation identifier
                ? DataResult<ResourceLocation>.Success(identifier)
                : DataResult<ResourceLocation>.Error(() => $"Invalid resource location '{text}'"),
            static identifier => DataResult<string>.Success(identifier.ToString()));

    public static MapCodec<VerticalGradientCondition> CODEC { get; } =
        RecordCodecBuilder.MapCodec<VerticalGradientCondition>(instance =>
            instance.Group(
                    ResourceLocationCodec.FieldOf("random_name")
                        .ForGetter<VerticalGradientCondition>(static condition => condition.RandomName),
                    VerticalAnchor.CODEC.FieldOf("true_at_and_below")
                        .ForGetter<VerticalGradientCondition>(static condition => condition.TrueAtAndBelow),
                    VerticalAnchor.CODEC.FieldOf("false_at_and_above")
                        .ForGetter<VerticalGradientCondition>(static condition => condition.FalseAtAndAbove))
                .Apply(
                    instance,
                    static (name, trueAt, falseAt) =>
                        new VerticalGradientCondition(name, trueAt, falseAt)));

    public static MapCodec<MaterialCondition> TYPE_CODEC { get; } =
        CODEC.XMap<MaterialCondition>(
            static condition => condition,
            static condition => (VerticalGradientCondition)condition);

    public ConditionEvaluator Compile(MaterialRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        int trueAtAndBelow = context.ResolveAnchorY(TrueAtAndBelow);
        int falseAtAndAbove = context.ResolveAnchorY(FalseAtAndAbove);
        PositionalRandomFactory randomFactory = context.GetOrCreateRandomFactory(RandomName);

        return context.LazyYCondition(() =>
        {
            int blockY = context.BlockY;
            if (blockY <= trueAtAndBelow)
                return true;
            if (blockY >= falseAtAndAbove)
                return false;

            double probability = Mth.Map(blockY, trueAtAndBelow, falseAtAndAbove, 1.0, 0.0);
            IRandomSource random = randomFactory.At(context.BlockX, blockY, context.BlockZ);
            return random.NextFloat() < probability;
        });
    }

    public MapCodec<MaterialCondition> Codec() => TYPE_CODEC;
}
