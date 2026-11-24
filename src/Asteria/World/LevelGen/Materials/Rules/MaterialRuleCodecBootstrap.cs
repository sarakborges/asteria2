using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.LevelGen.Materials.Rules;

/// <summary>Registers material-rule map codecs by their vanilla type identifiers.</summary>
internal static class MaterialRuleCodecBootstrap
{
    internal static void RegisterAll(Registry<MapCodec<MaterialRule>> registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Register(registry, "block", BlockRule.TYPE_CODEC);
        Register(registry, "bandlands", BandlandsRule.TYPE_CODEC);
        Register(registry, "sequence", SequenceRule.TYPE_CODEC);
        Register(registry, "condition", ConditionRule.TYPE_CODEC);
        Register(registry, "ore_vein", OreVeinRule.TYPE_CODEC);
    }

    private static void Register(
        Registry<MapCodec<MaterialRule>> registry,
        string id,
        MapCodec<MaterialRule> codec) =>
        registry.Register(
            RegistryKey<MapCodec<MaterialRule>>.Create(
                registry.Key,
                ResourceLocation.FromNamespaceAndPath("minecraft", id)),
            codec,
            RegistrationInfo.BuiltIn);
}
