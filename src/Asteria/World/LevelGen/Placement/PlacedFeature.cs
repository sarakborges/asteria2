using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World.Level;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Features;

namespace Asteria.World.LevelGen.Placement;

/// <summary>
/// A feature algorithm together with its ordered position modifiers.
/// Reference identity is intentional: feature sorting assigns stable indices by instance,
/// matching Minecraft 26.3 PlacedFeature semantics.
/// </summary>
public sealed class PlacedFeature
{
    public Holder<Feature> Feature { get; }
    public IReadOnlyList<PlacementModifier> Placement { get; }

    public PlacedFeature(
        Holder<Feature> feature,
        IEnumerable<PlacementModifier> placement)
    {
        Feature = feature ?? throw new ArgumentNullException(nameof(feature));
        ArgumentNullException.ThrowIfNull(placement);
        Placement = Array.AsReadOnly(placement.ToArray());
    }

    public bool Place(
        OverworldWorld level,
        NoiseBasedChunkGenerator generator,
        IRandomSource random,
        BlockPos origin) =>
        new FeaturePlacer(level, generator).Place(this, random, origin);

    public IEnumerable<Holder<Feature>> GetFeatures()
    {
        yield return Feature;
        foreach (Holder<Feature> subFeature in Feature.Value.GetSubFeatures())
            yield return subFeature;
    }

    public override string ToString() => $"Placed {Feature}";

    public static Codec<PlacedFeature> DIRECT_CODEC { get; } =
        RecordCodecBuilder.Create<PlacedFeature>(instance =>
            instance.Group(
                    FeatureCodecs.CODEC
                        .FieldOf("feature")
                        .ForGetter<PlacedFeature>(static placed => placed.Feature),
                    PlacementModifierCodecs.CODEC
                        .ListOf()
                        .FieldOf("placement")
                        .ForGetter<PlacedFeature>(static placed => placed.Placement))
                .Apply(
                    instance,
                    static (feature, placement) => new PlacedFeature(feature, placement)));

    public static Codec<Holder<PlacedFeature>> CODEC { get; } =
        RegistryCodecs.Holder(Registries.PLACED_FEATURE, DIRECT_CODEC);

    public static Codec<HolderSet<PlacedFeature>> LIST_CODEC { get; } =
        RegistryCodecs.HolderSet(Registries.PLACED_FEATURE);

    public static Codec<IReadOnlyList<HolderSet<PlacedFeature>>> LIST_OF_LISTS_CODEC { get; } =
        RegistryCodecs.HolderSet(Registries.PLACED_FEATURE, alwaysUseList: true)
            .ListOf();
}
