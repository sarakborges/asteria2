using System.Text.Json;
using Asteria.Content.Blocks;
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
            BuiltInRegistries.NoiseGeneratorSettings.Count == 0,
            "The noise settings registry remains data-driven until worldgen settings are bootstrapped.");

        Require(
            ReferenceEquals(
                BuiltInRegistries.Root.GetValue(
                    RegistryKey<IRegistry>.Create(
                        BuiltInRegistries.Root.Key,
                        BuiltInRegistries.NoiseGeneratorSettings.Location)),
                BuiltInRegistries.NoiseGeneratorSettings),
            "The root registry must resolve the noise generator settings registry.");
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
            ops.Get(encoded, "default_block").GetOrThrow().GetString() == "asteria:stone" &&
            ops.Get(encoded, "default_fluid").GetOrThrow().GetString() == "asteria:water" &&
            ops.Get(encoded, "noise_router").GetOrThrow().ValueKind == JsonValueKind.Object,
            "NoiseGeneratorSettings must serialize its vertical, block/fluid and eight-channel router settings.");

        Require(
            ops.Get(encoded, "material_rule").GetOrThrow().ValueKind == JsonValueKind.Object &&
            ops.Get(ops.Get(encoded, "material_rule").GetOrThrow(), "type").GetOrThrow().GetString() == "asteria:block",
            "NoiseGeneratorSettings.material_rule must support inline registered material rules.");

        Require(
            ops.Get(encoded, "sea_level").GetOrThrow().GetInt32() == 63 &&
            ops.Get(encoded, "use_legacy_random_source").GetOrThrow().GetBoolean(),
            "NoiseGeneratorSettings must preserve sea_level and use_legacy_random_source.");

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
            !ops.Get(encoded, "spawn_target").TryGetResult(out _),
            "Empty spawn targets, absent aquifers and empty debug functions must be omitted from encoded settings.");

        NoiseGeneratorSettings decoded = NoiseGeneratorSettings.CODEC.Parse(ops, encoded)
            .GetOrThrow()
            .Value;

        Require(
            decoded.Aquifers is null &&
            decoded.SpawnTarget.Count == 0 &&
            decoded.DebugFunctions.Functions.Count == 0,
            "Missing optional settings must decode to null aquifers and empty target/debug lists.");
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
