using Asteria.Content.Blocks;
using Asteria.Content.Registries;
using Asteria.Serialization;
using MaterialRuleDefinition = Asteria.World.LevelGen.Materials.Rules.MaterialRule;

namespace Asteria.World.LevelGen;

/// <summary>
/// Complete Minecraft Java 26.3 noise-generator configuration. The deprecated mob-generation
/// switch is intentionally excluded from Asteria while mob-related systems remain out of scope.
/// </summary>
public sealed record NoiseGeneratorSettings(
    NoiseSettings NoiseSettings,
    BlockState DefaultBlock,
    BlockState DefaultFluid,
    NoiseRouter NoiseRouter,
    Holder<MaterialRule> MaterialRule,
    IReadOnlyList<SpawnTargetPoint> SpawnTarget,
    int SeaLevel,
    Aquifer.Config? Aquifers,
    bool UseLegacyRandomSource,
    NoiseGeneratorDebugFunctions DebugFunctions)
{
    private sealed record NoiseAndBlocks(
        NoiseSettings NoiseSettings,
        BlockState DefaultBlock,
        BlockState DefaultFluid,
        NoiseRouter NoiseRouter);

    private sealed record MaterialAndFluidSettings(
        Holder<MaterialRule> MaterialRule,
        IReadOnlyList<SpawnTargetPoint> SpawnTarget,
        int SeaLevel,
        Aquifer.Config? Aquifers);

    private sealed record RandomAndDebugSettings(
        bool UseLegacyRandomSource,
        NoiseGeneratorDebugFunctions DebugFunctions);

    private static readonly IReadOnlyList<SpawnTargetPoint> EMPTY_SPAWN_TARGETS =
        Array.Empty<SpawnTargetPoint>();

    private static MapCodec<NoiseAndBlocks> NoiseAndBlocksCodec { get; } =
        RecordCodecBuilder.MapCodec<NoiseAndBlocks>(instance =>
            instance.Group(
                    NoiseSettings.CODEC.FieldOf("noise")
                        .ForGetter<NoiseAndBlocks>(static settings => settings.NoiseSettings),
                    BlockState.CODEC.FieldOf("default_block")
                        .ForGetter<NoiseAndBlocks>(static settings => settings.DefaultBlock),
                    BlockState.CODEC.FieldOf("default_fluid")
                        .ForGetter<NoiseAndBlocks>(static settings => settings.DefaultFluid),
                    NoiseRouter.CODEC.FieldOf("noise_router")
                        .ForGetter<NoiseAndBlocks>(static settings => settings.NoiseRouter))
                .Apply(
                    instance,
                    static (noise, defaultBlock, defaultFluid, router) =>
                        new NoiseAndBlocks(noise, defaultBlock, defaultFluid, router)));

    private static MapCodec<MaterialAndFluidSettings> MaterialAndFluidSettingsCodec { get; } =
        RecordCodecBuilder.MapCodec<MaterialAndFluidSettings>(instance =>
            instance.Group(
                    MaterialRuleDefinition.HOLDER_CODEC.FieldOf("material_rule")
                        .ForGetter<MaterialAndFluidSettings>(static settings => settings.MaterialRule),
                    SpawnTargetPoint.CODEC.ListOf()
                        .OptionalFieldOf("spawn_target", EMPTY_SPAWN_TARGETS)
                        .ForGetter<MaterialAndFluidSettings>(static settings => settings.SpawnTarget),
                    Codecs.INT.FieldOf("sea_level")
                        .ForGetter<MaterialAndFluidSettings>(static settings => settings.SeaLevel),
                    Aquifer.Config.CODEC.OptionalFieldOf("aquifers", null!)
                        .ForGetter<MaterialAndFluidSettings>(static settings => settings.Aquifers!))
                .Apply(
                    instance,
                    static (materialRule, spawnTarget, seaLevel, aquifers) =>
                        new MaterialAndFluidSettings(
                            materialRule,
                            spawnTarget,
                            seaLevel,
                            aquifers)));

    private static MapCodec<RandomAndDebugSettings> RandomAndDebugSettingsCodec { get; } =
        RecordCodecBuilder.MapCodec<RandomAndDebugSettings>(instance =>
            instance.Group(
                    Codecs.BOOL.FieldOf("use_legacy_random_source")
                        .ForGetter<RandomAndDebugSettings>(static settings => settings.UseLegacyRandomSource),
                    NoiseGeneratorDebugFunctions.CODEC
                        .OptionalFieldOf("debug_functions", NoiseGeneratorDebugFunctions.EMPTY)
                        .ForGetter<RandomAndDebugSettings>(static settings => settings.DebugFunctions))
                .Apply(
                    instance,
                    static (legacyRandom, debugFunctions) =>
                        new RandomAndDebugSettings(legacyRandom, debugFunctions)));

    public static Codec<NoiseGeneratorSettings> DIRECT_CODEC { get; } =
        RecordCodecBuilder.MapCodec<NoiseGeneratorSettings>(instance =>
            instance.Group(
                    NoiseAndBlocksCodec.ForGetter<NoiseGeneratorSettings>(static settings =>
                        new NoiseAndBlocks(
                            settings.NoiseSettings,
                            settings.DefaultBlock,
                            settings.DefaultFluid,
                            settings.NoiseRouter)),
                    MaterialAndFluidSettingsCodec.ForGetter<NoiseGeneratorSettings>(static settings =>
                        new MaterialAndFluidSettings(
                            settings.MaterialRule,
                            settings.SpawnTarget,
                            settings.SeaLevel,
                            settings.Aquifers)),
                    RandomAndDebugSettingsCodec.ForGetter<NoiseGeneratorSettings>(static settings =>
                        new RandomAndDebugSettings(
                            settings.UseLegacyRandomSource,
                            settings.DebugFunctions)))
                .Apply(
                    instance,
                    static (noise, material, runtime) =>
                        new NoiseGeneratorSettings(
                            noise.NoiseSettings,
                            noise.DefaultBlock,
                            noise.DefaultFluid,
                            noise.NoiseRouter,
                            material.MaterialRule,
                            material.SpawnTarget,
                            material.SeaLevel,
                            material.Aquifers,
                            runtime.UseLegacyRandomSource,
                            runtime.DebugFunctions)))
        .Codec();

    /// <summary>Registry-aware holder codec with inline material settings support.</summary>
    public static Codec<Holder<NoiseGeneratorSettings>> CODEC { get; } =
        RegistryCodecs.Holder(Registries.NOISE_SETTINGS, DIRECT_CODEC);

    /// <summary>Chooses the exact world-generation random algorithm requested by the settings.</summary>
    public WorldgenRandom.Algorithm GetRandomSource() =>
        UseLegacyRandomSource
            ? WorldgenRandom.Algorithm.LEGACY
            : WorldgenRandom.Algorithm.XOROSHIRO;
}
