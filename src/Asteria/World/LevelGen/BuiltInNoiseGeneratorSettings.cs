using System.Text.Json;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>
/// Loads the built-in Minecraft Java 26.3 Overworld noise-generator settings
/// after all referenced registries and the material-rule tree are finalized.
/// </summary>
internal static class BuiltInNoiseGeneratorSettings
{
    private const string ResourceName = "Asteria.Worldgen.NoiseSettings.overworld.json";

    internal static void RegisterAll()
    {
        Registry<NoiseGeneratorSettings> registry = BuiltInRegistries.NoiseGeneratorSettings;
        if (registry.Count != 0)
            throw new InvalidOperationException(
                "The built-in noise-generator-settings registry must be empty before Overworld data bootstrap.");

        System.Reflection.Assembly assembly = typeof(BuiltInNoiseGeneratorSettings).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName) ??
            throw new InvalidOperationException(
                $"Missing built-in noise-generator-settings resource '{ResourceName}'.");
        using JsonDocument document = JsonDocument.Parse(stream);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException(
                "The built-in Overworld noise settings resource must be a JSON object.");

        IRegistryLookup[] registryLookups =
        [
            BuiltInRegistries.Block,
            BuiltInRegistries.Biome,
            BuiltInRegistries.Noise,
            BuiltInRegistries.DensityFunction,
            BuiltInRegistries.DensityFunctionType,
            BuiltInRegistries.MaterialRule,
            BuiltInRegistries.MaterialRuleType,
            BuiltInRegistries.MaterialCondition,
            BuiltInRegistries.MaterialConditionType
        ];
        HolderLookupProvider lookupProvider = HolderLookupProvider.Create(registryLookups);
        RegistryOps<JsonElement> ops = RegistryOps<JsonElement>.Create(
            JsonOps.INSTANCE,
            lookupProvider);

        NoiseGeneratorSettings settings =
            NoiseGeneratorSettings.DIRECT_CODEC.Parse(ops, document.RootElement).GetOrThrow();

        ResourceLocation location = ResourceLocation.FromNamespaceAndPath("minecraft", "overworld");
        RegistryKey<NoiseGeneratorSettings> key =
            RegistryKey<NoiseGeneratorSettings>.Create(registry.Key, location);
        registry.Register(key, settings, RegistrationInfo.BuiltIn);
    }
}
