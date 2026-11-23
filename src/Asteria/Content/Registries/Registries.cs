using Asteria.Content.Identifiers;
using Asteria.World.LevelGen.DensityFunctions;

namespace Asteria.Content.Registries;

/// <summary>
/// Canonical registry keys referenced by registry-aware serialization and world generation.
/// </summary>
public static class Registries
{
    public static RegistryKey<Registry<DensityFunction>> DENSITY_FUNCTION { get; } =
        RegistryKey<DensityFunction>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "density_function"));
}
