using Asteria.Content.Blocks;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World;
using Asteria.World.LevelGen.DensityFunctions;

namespace Asteria.World.LevelGen;

/// <summary>
/// Fluid selection and pressure-aware terrain materialization contract from Minecraft Java 26.3.
/// </summary>
public interface Aquifer
{
    BlockState? ComputeSubstance(int blockX, int blockY, int blockZ, double density);

    bool ShouldScheduleFluidUpdate();

    static Aquifer CreateDisabled(FluidPicker fluidRule)
    {
        ArgumentNullException.ThrowIfNull(fluidRule);
        return new DisabledAquifer(fluidRule);
    }

    public delegate FluidStatus FluidPicker(int blockX, int blockY, int blockZ);

    public readonly record struct FluidStatus(int FluidLevel, BlockState FluidType)
    {
        public BlockState At(int blockY) =>
            blockY < FluidLevel ? FluidType : Blocks.AIR.DefaultState;
    }
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
        private sealed record BarrierInputs(
            DensityFunction BarrierNoise,
            DensityFunction FluidLevelFloodednessNoise,
            DensityFunction FluidLevelSpreadNoise,
            DensityFunction LavaNoise);

        private sealed record TerminalInputs(
            DensityFunction Exclusion,
            DensityFunction SurfaceLevel);

        private static MapCodec<BarrierInputs> BarrierInputsCodec { get; } =
            RecordCodecBuilder.MapCodec<BarrierInputs>(instance =>
                instance.Group(
                        DensityFunction.CODEC.FieldOf("barrier")
                            .ForGetter<BarrierInputs>(static inputs => inputs.BarrierNoise),
                        DensityFunction.CODEC.FieldOf("fluid_level_floodedness")
                            .ForGetter<BarrierInputs>(static inputs => inputs.FluidLevelFloodednessNoise),
                        DensityFunction.CODEC.FieldOf("fluid_level_spread")
                            .ForGetter<BarrierInputs>(static inputs => inputs.FluidLevelSpreadNoise),
                        DensityFunction.CODEC.FieldOf("lava")
                            .ForGetter<BarrierInputs>(static inputs => inputs.LavaNoise))
                    .Apply(
                        instance,
                        static (barrierNoise, floodednessNoise, spreadNoise, lavaNoise) =>
                            new BarrierInputs(
                                barrierNoise,
                                floodednessNoise,
                                spreadNoise,
                                lavaNoise)));

        private static MapCodec<TerminalInputs> TerminalInputsCodec { get; } =
            RecordCodecBuilder.MapCodec<TerminalInputs>(instance =>
                instance.Group(
                        DensityFunction.CODEC.FieldOf("exclusion")
                            .ForGetter<TerminalInputs>(static inputs => inputs.Exclusion),
                        DensityFunction.CODEC.FieldOf("surface_level")
                            .ForGetter<TerminalInputs>(static inputs => inputs.SurfaceLevel))
                    .Apply(
                        instance,
                        static (exclusion, surfaceLevel) =>
                            new TerminalInputs(exclusion, surfaceLevel)));

        /// <summary>
        /// Decodes the six independent 26.3 aquifer density-function inputs.
        /// </summary>
        public Aquifer Create(
            DensitySamplerSet cachingSamplers,
            PositionalRandomFactory positionalRandomFactory,
            DensityVolume volume,
            FluidPicker fluidRule)
        {
            ArgumentNullException.ThrowIfNull(cachingSamplers);
            ArgumentNullException.ThrowIfNull(positionalRandomFactory);
            ArgumentNullException.ThrowIfNull(volume);
            ArgumentNullException.ThrowIfNull(fluidRule);

            return new NoiseBasedAquifer(
                cachingSamplers,
                this,
                positionalRandomFactory,
                volume,
                fluidRule);
        }

        public static Codec<Config> CODEC { get; } =
            RecordCodecBuilder.Create<Config>(instance =>
                instance.Group(
                        BarrierInputsCodec.ForGetter<Config>(static config =>
                            new BarrierInputs(
                                config.BarrierNoise,
                                config.FluidLevelFloodednessNoise,
                                config.FluidLevelSpreadNoise,
                                config.LavaNoise)),
                        TerminalInputsCodec.ForGetter<Config>(static config =>
                            new TerminalInputs(config.Exclusion, config.SurfaceLevel)))
                    .Apply(
                        instance,
                        static (barrier, terminal) =>
                            new Config(
                                barrier.BarrierNoise,
                                barrier.FluidLevelFloodednessNoise,
                                barrier.FluidLevelSpreadNoise,
                                barrier.LavaNoise,
                                terminal.Exclusion,
                                terminal.SurfaceLevel)));
    }
}
