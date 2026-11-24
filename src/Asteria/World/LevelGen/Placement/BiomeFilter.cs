using Asteria.Core;
using Asteria.Serialization;
using Asteria.World.LevelGen;

namespace Asteria.World.LevelGen.Placement;

/// <summary>Retains a position only when its biome permits the top-level placed feature.</summary>
public sealed class BiomeFilter : PlacementFilter
{
    public static BiomeFilter INSTANCE { get; } = new();

    public static MapCodec<BiomeFilter> MAP_CODEC { get; } =
        MapCodec<BiomeFilter>.Unit(INSTANCE);

    public static MapCodec<PlacementModifier> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<PlacementModifier>(
            static modifier => modifier,
            static modifier => (BiomeFilter)modifier);

    private BiomeFilter()
    {
    }

    public static BiomeFilter Biome() => INSTANCE;

    public override bool ShouldPlace(
        PlacementContext context,
        IRandomSource random,
        BlockPos origin)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(origin);

        PlacedFeature topFeature = context.TopFeature ??
            throw new InvalidOperationException(
                "Tried to biome check an unregistered feature, or a feature that should not restrict the biome.");

        return context.Level.GetBiome(origin).Value.GenerationSettings.HasFeature(topFeature);
    }

    public override MapCodec<PlacementModifier> Codec() => TYPE_CODEC;
}
