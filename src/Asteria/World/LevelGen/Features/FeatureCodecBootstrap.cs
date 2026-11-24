using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.LevelGen.Features;

/// <summary>Registers the feature algorithms implemented in Asteria.</summary>
internal static class FeatureCodecBootstrap
{
    internal static void RegisterAll(Registry<MapCodec<Feature>> registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(
            RegistryKey<MapCodec<Feature>>.Create(
                registry.Key,
                ResourceLocation.FromNamespaceAndPath("asteria", "simple_block")),
            SimpleBlockFeature.TYPE_CODEC,
            RegistrationInfo.BuiltIn);
    }
}
