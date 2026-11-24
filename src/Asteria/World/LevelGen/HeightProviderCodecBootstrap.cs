using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Registers all context-sensitive height providers from Minecraft Java 26.3.</summary>
internal static class HeightProviderCodecBootstrap
{
    internal static void RegisterAll(Registry<MapCodec<HeightProvider>> registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Register(registry, "constant", ConstantHeight.TYPE_CODEC);
        Register(registry, "uniform", UniformHeight.TYPE_CODEC);
        Register(registry, "biased_to_bottom", BiasedToBottomHeight.TYPE_CODEC);
        Register(registry, "very_biased_to_bottom", VeryBiasedToBottomHeight.TYPE_CODEC);
        Register(registry, "trapezoid", TrapezoidHeight.TYPE_CODEC);
        Register(registry, "weighted_list", WeightedListHeight.TYPE_CODEC);
    }

    private static void Register(
        Registry<MapCodec<HeightProvider>> registry,
        string id,
        MapCodec<HeightProvider> codec) =>
        registry.Register(
            RegistryKey<MapCodec<HeightProvider>>.Create(
                registry.Key,
                ResourceLocation.FromNamespaceAndPath("minecraft", id)),
            codec,
            RegistrationInfo.BuiltIn);
}
