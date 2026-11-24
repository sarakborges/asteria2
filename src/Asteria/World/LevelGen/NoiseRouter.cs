using Asteria.Content.Biomes;
using Asteria.Serialization;
using Asteria.World.LevelGen.DensityFunctions;

namespace Asteria.World.LevelGen;

/// <summary>
/// The eight density-function inputs consumed by Minecraft Java 26.3 world generation.
/// </summary>
public sealed record NoiseRouter(
    DensityFunction Temperature,
    DensityFunction Vegetation,
    DensityFunction Continents,
    DensityFunction Erosion,
    DensityFunction Depth,
    DensityFunction Ridges,
    DensityFunction ChunkSurfaceLevel,
    DensityFunction FinalDensity)
{
    /// <summary>
    /// Creates the vanilla eight-channel Overworld router from the resolved
    /// twelve-slot function set, omitting the four intermediate terrain functions.
    /// </summary>
    public static NoiseRouter CreateOverworld(
        OverworldFunctionSet<DensityFunction> functions,
        DensityFunction ridges)
    {
        ArgumentNullException.ThrowIfNull(functions);
        ArgumentNullException.ThrowIfNull(ridges);

        return new NoiseRouter(
            functions.Temperature,
            functions.Vegetation,
            functions.Continents,
            functions.Erosion,
            functions.Depth,
            ridges,
            functions.ChunkSurfaceLevel,
            functions.FinalDensity);
    }

    private sealed record ClimateFunctions(
        DensityFunction Temperature,
        DensityFunction Vegetation,
        DensityFunction Continents,
        DensityFunction Erosion);

    private sealed record TerrainFunctions(
        DensityFunction Depth,
        DensityFunction Ridges,
        DensityFunction ChunkSurfaceLevel,
        DensityFunction FinalDensity);

    private static MapCodec<ClimateFunctions> ClimateFunctionsCodec { get; } =
        RecordCodecBuilder.MapCodec<ClimateFunctions>(instance =>
            instance.Group(
                    DensityFunction.CODEC.FieldOf("temperature")
                        .ForGetter<ClimateFunctions>(static functions => functions.Temperature),
                    DensityFunction.CODEC.FieldOf("vegetation")
                        .ForGetter<ClimateFunctions>(static functions => functions.Vegetation),
                    DensityFunction.CODEC.FieldOf("continents")
                        .ForGetter<ClimateFunctions>(static functions => functions.Continents),
                    DensityFunction.CODEC.FieldOf("erosion")
                        .ForGetter<ClimateFunctions>(static functions => functions.Erosion))
                .Apply(
                    instance,
                    static (temperature, vegetation, continents, erosion) =>
                        new ClimateFunctions(temperature, vegetation, continents, erosion)));

    private static MapCodec<TerrainFunctions> TerrainFunctionsCodec { get; } =
        RecordCodecBuilder.MapCodec<TerrainFunctions>(instance =>
            instance.Group(
                    DensityFunction.CODEC.FieldOf("depth")
                        .ForGetter<TerrainFunctions>(static functions => functions.Depth),
                    DensityFunction.CODEC.FieldOf("ridges")
                        .ForGetter<TerrainFunctions>(static functions => functions.Ridges),
                    DensityFunction.CODEC.FieldOf("chunk_surface_level")
                        .ForGetter<TerrainFunctions>(static functions => functions.ChunkSurfaceLevel),
                    DensityFunction.CODEC.FieldOf("final_density")
                        .ForGetter<TerrainFunctions>(static functions => functions.FinalDensity))
                .Apply(
                    instance,
                    static (depth, ridges, chunkSurfaceLevel, finalDensity) =>
                        new TerrainFunctions(depth, ridges, chunkSurfaceLevel, finalDensity)));

    public static Codec<NoiseRouter> CODEC { get; } =
        RecordCodecBuilder.MapCodec<NoiseRouter>(instance =>
            instance.Group(
                    ClimateFunctionsCodec.ForGetter<NoiseRouter>(static router =>
                        new ClimateFunctions(
                            router.Temperature,
                            router.Vegetation,
                            router.Continents,
                            router.Erosion)),
                    TerrainFunctionsCodec.ForGetter<NoiseRouter>(static router =>
                        new TerrainFunctions(
                            router.Depth,
                            router.Ridges,
                            router.ChunkSurfaceLevel,
                            router.FinalDensity)))
                .Apply(
                    instance,
                    static (climate, terrain) =>
                        new NoiseRouter(
                            climate.Temperature,
                            climate.Vegetation,
                            climate.Continents,
                            climate.Erosion,
                            terrain.Depth,
                            terrain.Ridges,
                            terrain.ChunkSurfaceLevel,
                            terrain.FinalDensity)))
        .Codec();

    /// <summary>
    /// Binds the six climate channels in the same order as Climate.Sampler.
    /// Terrain-only channels are intentionally not requested by biome sampling.
    /// </summary>
    public Climate.Sampler CreateClimateSampler(DensitySamplerSet densitySamplers)
    {
        ArgumentNullException.ThrowIfNull(densitySamplers);

        return new Climate.Sampler(
            densitySamplers.Get(Temperature),
            densitySamplers.Get(Vegetation),
            densitySamplers.Get(Continents),
            densitySamplers.Get(Erosion),
            densitySamplers.Get(Depth),
            densitySamplers.Get(Ridges));
    }
}
