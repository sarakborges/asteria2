using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.LevelGen.Features.StateProviders;

/// <summary>Registers the concrete BlockStateProvider codecs currently ported from 26.3.</summary>
internal static class BlockStateProviderCodecBootstrap
{
    internal static void RegisterAll(Registry<MapCodec<BlockStateProvider>> registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Register(registry, "simple", SimpleStateProvider.TYPE_CODEC);
        Register(registry, "weighted", WeightedStateProvider.TYPE_CODEC);
    }

    private static void Register(
        Registry<MapCodec<BlockStateProvider>> registry,
        string id,
        MapCodec<BlockStateProvider> codec) =>
        registry.Register(
            RegistryKey<MapCodec<BlockStateProvider>>.Create(
                registry.Key,
                ResourceLocation.FromNamespaceAndPath("asteria", id)),
            codec,
            RegistrationInfo.BuiltIn);
}
