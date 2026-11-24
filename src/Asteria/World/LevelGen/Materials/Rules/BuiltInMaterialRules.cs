using System.Text.Json;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.LevelGen.Materials.Rules;

/// <summary>
/// Loads the built-in Overworld material-rule data before registry freeze.
/// References are reserved first, then each JSON definition is decoded with RegistryOps
/// and registered into the same stable holders.
/// </summary>
internal static class BuiltInMaterialRules
{
    private const string ResourcePrefix = "Asteria.Worldgen.MaterialRuleCatalog.";
    private static readonly string[] Catalogs =
    [
        "catalog-01.json",
        "catalog-02.json",
        "catalog-03.json"
    ];

    private sealed record Definition(
        RegistryKey<MaterialRule> Key,
        JsonElement Json);

    internal static void RegisterAll()
    {
        Registry<MaterialRule> registry = BuiltInRegistries.MaterialRule;
        List<Definition> definitions = ReadDefinitions(registry);

        HolderGetter<MaterialRule> registrationLookup = registry.CreateRegistrationLookup();
        foreach (Definition definition in definitions)
            _ = registrationLookup.GetOrThrow(definition.Key);

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

        foreach (Definition definition in definitions)
        {
            MaterialRule rule = MaterialRule.CODEC.Parse(ops, definition.Json).GetOrThrow();
            registry.Register(definition.Key, rule, RegistrationInfo.BuiltIn);
        }
    }

    private static List<Definition> ReadDefinitions(Registry<MaterialRule> registry)
    {
        System.Reflection.Assembly assembly = typeof(BuiltInMaterialRules).Assembly;
        var definitions = new List<Definition>(37);
        var locations = new HashSet<ResourceLocation>();

        foreach (string catalog in Catalogs)
        {
            string resourceName = ResourcePrefix + catalog;
            using Stream stream = assembly.GetManifestResourceStream(resourceName) ??
                throw new InvalidOperationException(
                    $"Missing built-in material-rule resource '{resourceName}'.");
            using JsonDocument document = JsonDocument.Parse(stream);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException(
                    $"Built-in material-rule catalog '{resourceName}' must be a JSON object.");

            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                ResourceLocation location = ResourceLocation.TryParse(property.Name) ??
                    throw new InvalidOperationException(
                        $"Invalid material-rule resource key '{property.Name}' in '{resourceName}'.");
                if (!locations.Add(location))
                    throw new InvalidOperationException(
                        $"Duplicate built-in material-rule key '{location}'.");

                if (property.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.String))
                    throw new InvalidOperationException(
                        $"Material-rule definition '{location}' must be an object or a holder reference.");

                RegistryKey<MaterialRule> key = RegistryKey<MaterialRule>.Create(
                    registry.Key,
                    location);
                definitions.Add(new Definition(key, property.Value.Clone()));
            }
        }

        if (definitions.Count != 37)
            throw new InvalidOperationException(
                $"Expected 37 vanilla Overworld material-rule definitions, but loaded {definitions.Count}.");

        return definitions;
    }
}
