using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Registers the concrete data codecs for Minecraft 26.3 world carvers.</summary>
internal static class WorldCarverCodecBootstrap
{
    internal static void RegisterAll(Registry<MapCodec<WorldCarver>> registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Register(registry, "cave", CaveWorldCarver.TYPE_CODEC);
        Register(registry, "canyon", CanyonWorldCarver.TYPE_CODEC);
    }

    private static void Register(
        Registry<MapCodec<WorldCarver>> registry,
        string id,
        MapCodec<WorldCarver> codec) =>
        registry.Register(
            RegistryKey<MapCodec<WorldCarver>>.Create(
                registry.Key,
                ResourceLocation.FromNamespaceAndPath("minecraft", id)),
            codec,
            RegistrationInfo.BuiltIn);
}
