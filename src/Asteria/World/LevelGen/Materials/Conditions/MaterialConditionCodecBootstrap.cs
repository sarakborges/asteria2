using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.LevelGen.Materials.Conditions;

/// <summary>Registers built-in material-condition map codecs in vanilla bootstrap order.</summary>
internal static class MaterialConditionCodecBootstrap
{
    internal static void RegisterAll(Registry<MapCodec<MaterialCondition>> registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Register(registry, "steep", SteepCondition.TYPE_CODEC);
        Register(registry, "not", NotCondition.TYPE_CODEC);
        Register(registry, "hole", HoleCondition.TYPE_CODEC);
    }

    private static void Register(
        Registry<MapCodec<MaterialCondition>> registry,
        string id,
        MapCodec<MaterialCondition> codec) =>
        registry.Register(
            RegistryKey<MapCodec<MaterialCondition>>.Create(
                registry.Key, ResourceLocation.WithDefaultNamespace(id)),
            codec,
            RegistrationInfo.BuiltIn);
}
