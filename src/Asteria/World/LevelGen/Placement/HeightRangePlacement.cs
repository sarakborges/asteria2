using Asteria.Core;
using Asteria.Serialization;
using Asteria.World.LevelGen;

namespace Asteria.World.LevelGen.Placement;

/// <summary>Samples one Y coordinate from a contextual HeightProvider.</summary>
public sealed class HeightRangePlacement : PlacementModifier
{
    public HeightProvider Height { get; }

    public static MapCodec<HeightRangePlacement> MAP_CODEC { get; } =
        HeightProvider.CODEC
            .FieldOf("height")
            .XMap<HeightRangePlacement>(
                static height => new HeightRangePlacement(height),
                static placement => placement.Height);

    public static MapCodec<PlacementModifier> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<PlacementModifier>(
            static modifier => modifier,
            static modifier => (HeightRangePlacement)modifier);

    public HeightRangePlacement(HeightProvider height) =>
        Height = height ?? throw new ArgumentNullException(nameof(height));

    public static HeightRangePlacement Of(HeightProvider height) =>
        new(height);

    public static HeightRangePlacement Uniform(
        VerticalAnchor minimum,
        VerticalAnchor maximum) =>
        Of(UniformHeight.Of(minimum, maximum));

    public static HeightRangePlacement Triangle(
        VerticalAnchor minimum,
        VerticalAnchor maximum) =>
        Of(TrapezoidHeight.Of(minimum, maximum));

    public void Modify(
        PlacementContext context,
        IRandomSource random,
        BlockPos origin,
        Action<BlockPos> output)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(output);

        int y = Height.Sample(random, context.GenerationContext);
        output(new BlockPos(origin.GetX(), y, origin.GetZ()));
    }

    public MapCodec<PlacementModifier> Codec() => TYPE_CODEC;
}
