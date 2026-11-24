using System.Text.Json;
using Asteria.Content.Blocks;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.Materials.Rules;

internal static class NoiseGeneratorSettingsChecks
{
    public static void Run()
    {
        CheckBuiltInRegistry();
        CheckRequiredSettingsCodecRoundTrip();
        CheckVanillaOverworldSettings();
        CheckOptionalSettingsDefaults();
        CheckRandomSourceSelection();
    }

    private static void CheckBuiltInRegistry()
    {
        Require(
            BuiltInRegistries.NoiseGeneratorSettings.Key.Location ==
                ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/noise_settings"),
            "Noise generator settings must use the vanilla worldgen/noise_settings registry key.");

        Require(
            BuiltInRegistries.NoiseGeneratorSettings.Count == 1 &&
            BuiltInRegistries.NoiseGeneratorSettings.IsFrozen,
            "The built-in Overworld noise settings must be registered and frozen after bootstrap.");

        Require(
            ReferenceEquals(
                BuiltInRegistries.Root.GetValue(
                    RegistryKey<IRegistry>.Create(
                        BuiltInRegistries.Root.Key,
                        BuiltInRegistries.NoiseGeneratorSettings.Location)),
                BuiltInRegistries.NoiseGeneratorSettings),
            "The root registry must resolve the noise generator settings registry.");
    }

    private static void CheckVanillaOverworldSettings()
    {
        ResourceLocation key = ResourceLocation.FromNamespaceAndPath("minecraft", "overworld");
        NoiseGeneratorSettings settings = BuiltInRegistries.NoiseGeneratorSettings.GetValueOrThrow(
            RegistryKey<NoiseGeneratorSettings>.Create(BuiltInRegistries.NoiseGeneratorSettings.Key, key));

        Require(
            settings.NoiseSettings == NoiseSettings.Create(-64, 384) &&
            ReferenceEquals(settings.DefaultBlock, Blocks.STONE.DefaultState) &&
            ReferenceEquals(settings.DefaultFluid, Blocks.WATER.DefaultState) &&
            settings.SeaLevel == 63,
            "The registered vanilla Overworld settings must preserve its build height, default blocks and sea level.");
        Require(
            settings.SpawnTarget.Count == 2 &&
            settings.Aquifers is not null &&
            !settings.UseLegacyRandomSource &&
            settings.DebugFunctions.Functions.Count == 9,
            "The vanilla Overworld settings must load two player spawn targets, aquifers, xoroshiro selection and nine debug functions.");
        Require(
            settings.MaterialRule.Kind == HolderKind.Reference &&
            ReferenceEquals(
                settings.MaterialRule,
                BuiltInRegistries.MaterialRule.GetHolder(
                    ResourceLocation.FromNamespaceAndPath("minecraft", "overworld"))),
            "The Overworld noise settings must reference the registered Overworld material rule holder.");

        static bool IsDensityReference(DensityFunction function, string path) =>
            function is DensityFunctions.HolderHolder holder &&
            holder.Function.KeyOrNull is RegistryKey<DensityFunction> densityKey &&
            densityKey.Location == ResourceLocation.FromNamespaceAndPath("minecraft", path);

        Require(
            IsDensityReference(settings.NoiseRouter.Temperature, "overworld/temperature") &&
            IsDensityReference(settings.NoiseRouter.Vegetation, "overworld/vegetation") &&
            IsDensityReference(settings.NoiseRouter.Continents, "overworld/continents") &&
            IsDensityReference(settings.NoiseRouter.Erosion, "overworld/erosion") &&
            IsDensityReference(settings.NoiseRouter.Depth, "overworld/depth") &&
            IsDensityReference(settings.NoiseRouter.Ridges, "overworld/ridges") &&
            IsDensityReference(settings.NoiseRouter.ChunkSurfaceLevel, "overworld/chunk_surface_level") &&
            IsDensityReference(settings.NoiseRouter.FinalDensity, "overworld/final_density"),
            "The registered Overworld NoiseRouter must resolve all eight canonical density-function references.");

        Require(
            settings.Aquifers is not null &&
            IsDensityReference(settings.Aquifers.SurfaceLevel, "overworld/preliminary_surface_level") &&
            settings.SpawnTarget.All(target => target.Parameters.Keys.All(holder =>
                holder.KeyOrNull is RegistryKey<DensityFunction> densityKey &&
                densityKey.Location.Namespace == "minecraft")),
            "Aquifer and player spawn-target density references must bind to registered Overworld density functions.");
    }

    private static void CheckRequiredSettingsCodecRoundTrip()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Aquifer.Config aquifers = CreateAquiferConfig();
        NoiseGeneratorSettings settings = CreateSettings(
            aquifers,
            new NoiseGeneratorDebugFunctions(
            [
                new NoiseGeneratorDebugFunctionEntry(
                    "final_density",
                    new ConstantFunction(-0.25f))
            ]),
            useLegacyRandomSource: true);

        Holder<NoiseGeneratorSettings> holder = Holder<NoiseGeneratorSettings>.Direct(settings);
        JsonElement encoded = NoiseGeneratorSettings.CODEC.EncodeStart(ops, holder).GetOrThrow();

        Require(
            ops.Get(encoded, "noise").GetOrThrow().ValueKind == JsonValueKind.Object &&
            ops.Get(encoded, "default_block").GetOrThrow().GetString() == "minecraft:stone" &&
            ops.Get(encoded, "default_fluid").GetOrThrow().GetString() == "minecraft:water" &&
            ops.Get(encoded, "noise_router").GetOrThrow().ValueKind == JsonValueKind.Object,
            "NoiseGeneratorSettings must serialize its vertical, block/fluid and eight-channel router settings.");

        Require(
            ops.Get(encoded, "material_rule").GetOrThrow().ValueKind == JsonValueKind.Object &&
            ops.Get(ops.Get(encoded, "material_rule").GetOrThrow(), "type").GetOrThrow().GetString() == "minecraft:block",
            "NoiseGeneratorSettings.material_rule must support inline registered material rules.");

        Require(
            ops.Get(encoded, "sea_level").GetOrThrow().GetInt32() == 63 &&
            ops.Get(encoded, "legacy_random_source").GetOrThrow().GetBoolean(),
            "NoiseGeneratorSettings must preserve sea_level and legacy_random_source.");

        Require(
            ops.Get(encoded, "aquifers").GetOrThrow().ValueKind == JsonValueKind.Object &&
            ops.Get(encoded, "debug_functions").GetOrThrow().GetArrayLength() == 1,
            "Configured aquifers and debug functions must be emitted under their vanilla field names.");

        NoiseGeneratorSettings decoded = NoiseGeneratorSettings.CODEC
            .Parse(ops, encoded)
            .GetOrThrow()
            .Value;

        Require(
            decoded.NoiseSettings == settings.NoiseSettings &&
            ReferenceEquals(decoded.DefaultBlock, settings.DefaultBlock) &&
            ReferenceEquals(decoded.DefaultFluid, settings.DefaultFluid) &&
            decoded.SeaLevel == settings.SeaLevel &&
            decoded.UseLegacyRandomSource &&
            decoded.Aquifers is not null &&
            decoded.DebugFunctions.Functions.Count == 1 &&
            decoded.DebugFunctions.Functions[0].Label == "final_density",
            "NoiseGeneratorSettings must round-trip every required and configured optional component.");

        Require(
            decoded.MaterialRule.Kind == HolderKind.Direct &&
            decoded.MaterialRule.Value is BlockRule blockRule &&
            ReferenceEquals(blockRule.ResultState, Blocks.STONE.DefaultState),
            "The inline material-rule holder must survive the settings round-trip.");
    }

    private static void CheckOptionalSettingsDefaults()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        NoiseGeneratorSettings settings = CreateSettings(
            null,
            NoiseGeneratorDebugFunctions.EMPTY,
            useLegacyRandomSource: false);
        JsonElement encoded = NoiseGeneratorSettings.CODEC.EncodeStart(
            ops,
            Holder<NoiseGeneratorSettings>.Direct(settings)).GetOrThrow();

        Require(
            !ops.Get(encoded, "aquifers").TryGetResult(out _) &&
            !ops.Get(encoded, "debug_functions").TryGetResult(out _) &&
            ops.Get(encoded, "spawn_target").TryGetResult(out JsonElement spawnTargets) &&
            spawnTargets.ValueKind == JsonValueKind.Array &&
            spawnTargets.GetArrayLength() == 0,
            "The required spawn_target list must be emitted even when empty; only aquifers and debug functions are optional.");

        NoiseGeneratorSettings decoded = NoiseGeneratorSettings.CODEC.Parse(ops, encoded)
            .GetOrThrow()
            .Value;

        Require(
            decoded.Aquifers is null &&
            decoded.SpawnTarget.Count == 0 &&
            decoded.DebugFunctions.Functions.Count == 0,
            "Missing optional aquifers/debug functions must default while the required spawn-target list decodes explicitly.");

        JsonElement missingSpawnTarget = ops.Remove(encoded, "spawn_target");
        Require(
            NoiseGeneratorSettings.DIRECT_CODEC.Parse(ops, missingSpawnTarget).IsError,
            "NoiseGeneratorSettings must reject a missing required spawn_target field.");
    }

    private static void CheckRandomSourceSelection()
    {
        NoiseGeneratorSettings legacy = CreateSettings(
            null,
            NoiseGeneratorDebugFunctions.EMPTY,
            useLegacyRandomSource: true);
        NoiseGeneratorSettings xoroshiro = legacy with { UseLegacyRandomSource = false };

        Require(
            ReferenceEquals(legacy.GetRandomSource(), WorldgenRandom.Algorithm.LEGACY) &&
            ReferenceEquals(xoroshiro.GetRandomSource(), WorldgenRandom.Algorithm.XOROSHIRO),
            "NoiseGeneratorSettings must select the configured vanilla random-source algorithm.");
    }

    private static NoiseGeneratorSettings CreateSettings(
        Aquifer.Config? aquifers,
        NoiseGeneratorDebugFunctions debugFunctions,
        bool useLegacyRandomSource)
    {
        var density = new ConstantFunction(0.0f);
        var router = new NoiseRouter(
            density,
            density,
            density,
            density,
            density,
            density,
            density,
            density);

        return new NoiseGeneratorSettings(
            NoiseSettings.Create(-64, 384),
            Blocks.STONE.DefaultState,
            Blocks.WATER.DefaultState,
            router,
            Holder<MaterialRule>.Direct(new BlockRule(Blocks.STONE.DefaultState)),
            Array.Empty<SpawnTargetPoint>(),
            SeaLevel: 63,
            Aquifers: aquifers,
            UseLegacyRandomSource: useLegacyRandomSource,
            DebugFunctions: debugFunctions);
    }

    private static Aquifer.Config CreateAquiferConfig() =>
        new(
            new ConstantFunction(0.1f),
            new ConstantFunction(0.2f),
            new ConstantFunction(0.3f),
            new ConstantFunction(0.4f),
            new ConstantFunction(0.5f),
            new ConstantFunction(0.6f));

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
