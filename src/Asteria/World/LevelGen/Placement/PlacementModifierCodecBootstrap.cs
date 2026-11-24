using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.LevelGen.Placement;

/// <summary>Registers the initial placement-modifier algorithms supported by Asteria.</summary>
internal static class PlacementModifierCodecBootstrap
{
    internal static void RegisterAll(Registry<MapCodec<PlacementModifier>> registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Register(registry, "in_square", InSquarePlacement.TYPE_CODEC);
        Register(registry, "biome", BiomeFilter.TYPE_CODEC);
        Register(registry, "rarity_filter", RarityFilter.TYPE_CODEC);
        Register(registry, "count", CountPlacement.TYPE_CODEC);
    }

    private static void Register(
        Registry<MapCodec<PlacementModifier>> registry,
        string id,
        MapCodec<PlacementModifier> codec) =>
        registry.Register(
            RegistryKey<MapCodec<PlacementModifier>>.Create(
                registry.Key,
                ResourceLocation.FromNamespaceAndPath("asteria", id)),
            codec,
            RegistrationInfo.BuiltIn);
}
