using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Registers context-free integer providers from Minecraft Java 26.3.</summary>
internal static class IntProviderCodecBootstrap
{
    internal static void RegisterAll(Registry<MapCodec<IntProvider>> registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Register(registry, "constant", ConstantInt.TYPE_CODEC);
        Register(registry, "uniform", UniformInt.TYPE_CODEC);
        Register(registry, "biased_to_bottom", BiasedToBottomInt.TYPE_CODEC);
        Register(registry, "very_biased_to_bottom", VeryBiasedToBottomInt.TYPE_CODEC);
        Register(registry, "clamped", ClampedInt.TYPE_CODEC);
        Register(registry, "weighted_list", WeightedListInt.TYPE_CODEC);
        Register(registry, "clamped_normal", ClampedNormalInt.TYPE_CODEC);
        Register(registry, "trapezoid", TrapezoidInt.TYPE_CODEC);
    }

    private static void Register(
        Registry<MapCodec<IntProvider>> registry,
        string id,
        MapCodec<IntProvider> codec) =>
        registry.Register(
            RegistryKey<MapCodec<IntProvider>>.Create(
                registry.Key,
                ResourceLocation.FromNamespaceAndPath("minecraft", id)),
            codec,
            RegistrationInfo.BuiltIn);
}
