using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Registers the context-free float-provider types used by Minecraft 26.3 world generation.</summary>
internal static class FloatProviderCodecBootstrap
{
    internal static void RegisterAll(Registry<MapCodec<FloatProvider>> registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Register(registry, "constant", ConstantFloat.TYPE_CODEC);
        Register(registry, "uniform", UniformFloat.TYPE_CODEC);
        Register(registry, "clamped_normal", ClampedNormalFloat.TYPE_CODEC);
        Register(registry, "trapezoid", TrapezoidFloat.TYPE_CODEC);
    }

    private static void Register(
        Registry<MapCodec<FloatProvider>> registry,
        string id,
        MapCodec<FloatProvider> codec) =>
        registry.Register(
            RegistryKey<MapCodec<FloatProvider>>.Create(
                registry.Key,
                ResourceLocation.FromNamespaceAndPath("minecraft", id)),
            codec,
            RegistrationInfo.BuiltIn);
}
