using Asteria.Content.Biomes;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.LevelGen.Structure;

/// <summary>
/// Settings shared by structure definitions. Mob spawn overrides are intentionally excluded
/// from this local-game scope; biome selection, generation ordering and terrain adaptation remain.
/// </summary>
public sealed record StructureSettings(
    HolderSet<Biome> Biomes,
    GenerationStep.Decoration Step,
    TerrainAdjustment TerrainAdaptation)
{
    public static readonly StructureSettings DEFAULT = new(
        HolderSet<Biome>.Empty,
        GenerationStep.Decoration.SURFACE_STRUCTURES,
        TerrainAdjustment.NONE);

    public static MapCodec<StructureSettings> CODEC { get; } = CreateCodec();

    public StructureSettings(HolderSet<Biome> biomes)
        : this(biomes, DEFAULT.Step, DEFAULT.TerrainAdaptation)
    {
    }

    private static MapCodec<StructureSettings> CreateCodec() =>
        RecordCodecBuilder.MapCodec<StructureSettings>(instance =>
            instance.Group(
                RegistryCodecs.HolderSet<Biome>(BuiltInRegistries.Biome.Key)
                    .FieldOf("biomes")
                    .ForGetter<StructureSettings>(static settings => settings.Biomes),
                GenerationStep.Decoration.CODEC
                    .FieldOf("step")
                    .ForGetter<StructureSettings>(static settings => settings.Step),
                TerrainAdjustment.CODEC
                    .OptionalFieldOf("terrain_adaptation", DEFAULT.TerrainAdaptation)
                    .ForGetter<StructureSettings>(static settings => settings.TerrainAdaptation))
            .Apply(
                instance,
                static (biomes, step, terrainAdaptation) =>
                    new StructureSettings(biomes, step, terrainAdaptation)));

    /// <summary>Vanilla-style builder for the settings that remain in Asteria's product scope.</summary>
    public sealed class Builder
    {
        private readonly HolderSet<Biome> _biomes;
        private GenerationStep.Decoration _step;
        private TerrainAdjustment _terrainAdaption;

        public Builder(HolderSet<Biome> biomes)
        {
            _biomes = biomes;
            _step = DEFAULT.Step;
            _terrainAdaption = DEFAULT.TerrainAdaptation;
        }

        public Builder GenerationStep(GenerationStep.Decoration step)
        {
            _step = step;
            return this;
        }

        /// <remarks>The source API spells this member "terrainAdapation".</remarks>
        public Builder TerrainAdapation(TerrainAdjustment terrainAdaption)
        {
            _terrainAdaption = terrainAdaption;
            return this;
        }

        public StructureSettings Build() =>
            new(_biomes, _step, _terrainAdaption);
    }
}
