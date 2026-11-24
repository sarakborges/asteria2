using Asteria.Serialization;
using Asteria.World.LevelGen.DensityFunctions;

namespace Asteria.World.LevelGen;

/// <summary>
/// Aquifer generation contract from Minecraft Java 26.3.
/// The implementation is supplied by the chunk-generation pipeline.
/// </summary>
public interface Aquifer
{
    /// <summary>
    /// Density inputs used to construct the aquifer sampler.
    /// </summary>
    public sealed record Config(
        DensityFunction BarrierNoise,
        DensityFunction FluidLevelFloodednessNoise,
        DensityFunction FluidLevelSpreadNoise,
        DensityFunction LavaNoise,
        DensityFunction Exclusion,
        DensityFunction SurfaceLevel)
    {
        /// <summary>
        /// Decodes the six independent 26.3 aquifer density-function inputs.
        /// </summary>
        public static Codec<Config> CODEC { get; } =
            RecordCodecBuilder.Create<Config>(instance =>
                instance.Group(
                        DensityFunction.CODEC.FieldOf("barrier")
                            .ForGetter<Config>(static config => config.BarrierNoise),
                        DensityFunction.CODEC.FieldOf("fluid_level_floodedness")
                            .ForGetter<Config>(static config => config.FluidLevelFloodednessNoise),
                        DensityFunction.CODEC.FieldOf("fluid_level_spread")
                            .ForGetter<Config>(static config => config.FluidLevelSpreadNoise),
                        DensityFunction.CODEC.FieldOf("lava")
                            .ForGetter<Config>(static config => config.LavaNoise),
                        DensityFunction.CODEC.FieldOf("exclusion")
                            .ForGetter<Config>(static config => config.Exclusion),
                        DensityFunction.CODEC.FieldOf("surface_level")
                            .ForGetter<Config>(static config => config.SurfaceLevel))
                    .Apply(
                        instance,
                        static (
                            barrierNoise,
                            fluidLevelFloodednessNoise,
                            fluidLevelSpreadNoise,
                            lavaNoise,
                            exclusion,
                            surfaceLevel) =>
                            new Config(
                                barrierNoise,
                                fluidLevelFloodednessNoise,
                                fluidLevelSpreadNoise,
                                lavaNoise,
                                exclusion,
                                surfaceLevel)));
    }
}
