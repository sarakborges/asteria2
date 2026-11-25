using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.LevelGen.Structure.Placement;

/// <summary>
/// Registers only structure-placement map codecs whose complete behavior is ported.
/// Missing vanilla placement types are intentionally not replaced by placeholders.
/// </summary>
internal static class StructurePlacementCodecBootstrap
{
    internal static void RegisterAll(
        Registry<MapCodec<StructurePlacement>> registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.Register(
            RegistryKey<MapCodec<StructurePlacement>>.Create(
                registry.Key,
                ResourceLocation.FromNamespaceAndPath("minecraft", "dimension_origin")),
            DimensionOriginStructurePlacement.PLACEMENT_CODEC,
            RegistrationInfo.BuiltIn);

        registry.Register(
            RegistryKey<MapCodec<StructurePlacement>>.Create(
                registry.Key,
                ResourceLocation.FromNamespaceAndPath("minecraft", "random_spread")),
            RandomSpreadStructurePlacement.PLACEMENT_CODEC,
            RegistrationInfo.BuiltIn);
    }
}
