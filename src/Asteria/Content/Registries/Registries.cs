using Asteria.Content.Identifiers;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.Synth;

namespace Asteria.Content.Registries;

/// <summary>
/// Canonical registry keys referenced by registry-aware serialization and world generation.
/// </summary>
public static class Registries
{
    public static RegistryKey<Registry<DensityFunction>> DENSITY_FUNCTION { get; } =
        RegistryKey<DensityFunction>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/density_function"));

    public static RegistryKey<Registry<NormalNoise>> NOISE { get; } =
        RegistryKey<NormalNoise>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/noise"));
}
