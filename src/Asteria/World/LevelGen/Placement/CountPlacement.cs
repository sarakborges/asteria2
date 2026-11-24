using Asteria.Core;
using Asteria.Serialization;

namespace Asteria.World.LevelGen.Placement;

/// <summary>Repeats its input according to a context-free integer provider.</summary>
public sealed record CountPlacement(IntProvider CountProvider) : RepeatingPlacement
{
    public static MapCodec<CountPlacement> MAP_CODEC { get; } =
        IntProviders.Codec(0, 4096)
            .FieldOf("count")
            .XMap<CountPlacement>(
                static count => new CountPlacement(count),
                static placement => placement.CountProvider);

    public static MapCodec<PlacementModifier> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<PlacementModifier>(
            static modifier => modifier,
            static modifier => (CountPlacement)modifier);

    public static CountPlacement Of(IntProvider count) =>
        new(count ?? throw new ArgumentNullException(nameof(count)));

    public static CountPlacement Of(int count) =>
        Of(ConstantInt.Of(count));

    public override int Count(IRandomSource random, BlockPos origin)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(origin);
        return CountProvider.Sample(random);
    }

    public override MapCodec<PlacementModifier> Codec() => TYPE_CODEC;
}
