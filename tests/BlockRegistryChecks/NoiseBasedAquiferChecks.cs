using Asteria.Content.Blocks;
using Asteria.Core;
using Asteria.World;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;

internal static class NoiseBasedAquiferChecks
{
    private sealed class ConstantDensitySampler(float value) : DensitySampler
    {
        public void SampleVolume(
            SamplerContext context,
            DensityBuffer outputBuffer,
            DensityVolume volume) =>
            outputBuffer.Fill(value);

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            value;
    }

    private sealed class TestDensitySamplerSet : DensitySamplerSet
    {
        private readonly Dictionary<DensityFunction, ConstantDensitySampler> _samplers =
            new(ReferenceEqualityComparer.Instance);

        public void Add(DensityFunction function, float value) =>
            _samplers.Add(function, new ConstantDensitySampler(value));

        public DensitySampler.Bound Get(DensityFunction function)
        {
            if (!_samplers.TryGetValue(function, out ConstantDensitySampler? sampler))
                throw new InvalidOperationException("The aquifer test did not configure a sampler for a density function.");

            return sampler.Bind(SamplerContext.EmptyUncached);
        }
    }

    public static void Run()
    {
        CheckNumericHelpers();
        CheckFluidStatusBoundaries();
        CheckDisabledAquifer();
        CheckNoiseBasedAquiferFastPaths();
        CheckNoiseBasedAquiferNormalSampling();
    }

    private static void CheckNumericHelpers()
    {
        Require(Mth.Map(0.5, 0.0, 1.0, -1.0, 1.0) == 0.0,
            "Mth.Map must interpolate between the requested output bounds.");
        Require(Mth.ClampedMap(-1.0, 0.0, 1.0, -0.3, 0.8) == -0.3,
            "Mth.ClampedMap must clamp below the input interval.");
        Require(Mth.ClampedMap(2.0, 0.0, 1.0, -0.3, 0.8) == 0.8,
            "Mth.ClampedMap must clamp above the input interval.");
        Require(Mth.Quantize(5.9, 3) == 3 && Mth.Quantize(-1.0, 3) == -3,
            "Mth.Quantize must floor to the lower multiple, including negative values.");
    }

    private static void CheckFluidStatusBoundaries()
    {
        Aquifer.FluidStatus water = new(10, Blocks.WATER.DefaultState);
        Require(ReferenceEquals(water.At(9), Blocks.WATER.DefaultState),
            "FluidStatus.At must return the selected fluid below its level.");
        Require(ReferenceEquals(water.At(10), Blocks.AIR.DefaultState),
            "FluidStatus.At must return air at the exact fluid-level boundary.");
        Require(ReferenceEquals(water.At(11), Blocks.AIR.DefaultState),
            "FluidStatus.At must return air above its fluid level.");
    }

    private static void CheckDisabledAquifer()
    {
        Aquifer disabled = Aquifer.CreateDisabled(
            static (_, _, _) => new Aquifer.FluidStatus(10, Blocks.WATER.DefaultState));

        Require(disabled.ComputeSubstance(0, 9, 0, 0.0) == Blocks.WATER.DefaultState,
            "The disabled aquifer must choose the global fluid when density is non-positive.");
        Require(disabled.ComputeSubstance(0, 10, 0, -1.0) == Blocks.AIR.DefaultState,
            "The disabled aquifer must respect the global fluid level.");
        Require(disabled.ComputeSubstance(0, 0, 0, 0.0001) is null,
            "The disabled aquifer must return null for positive density.");
        Require(!disabled.ShouldScheduleFluidUpdate(),
            "The disabled aquifer must never schedule fluid updates.");
    }

    private static void CheckNoiseBasedAquiferFastPaths()
    {
        Aquifer.Config config = CreateConfig(
            floodedness: 1.0f,
            preliminarySurface: 0.0f);
        DensityVolume volume = new(16, 16, 16, 0, -8, 0);
        TestDensitySamplerSet samplers = CreateSamplers(config);

        Aquifer waterAquifer = config.Create(
            samplers,
            new LegacyRandomSource(918273L).ForkPositional(),
            volume,
            static (_, _, _) => new Aquifer.FluidStatus(100, Blocks.WATER.DefaultState));

        Require(waterAquifer.ComputeSubstance(0, 0, 0, 0.25) is null,
            "NoiseBasedAquifer must reject positive density before querying fluid.");
        Require(!waterAquifer.ShouldScheduleFluidUpdate(),
            "Positive density must clear the fluid-update flag.");

        BlockState? above = waterAquifer.ComputeSubstance(0, 100, 0, -1.0);
        Require(ReferenceEquals(above, Blocks.AIR.DefaultState),
            "NoiseBasedAquifer must use the global fluid picker above the precomputed sampling limit.");
        Require(!waterAquifer.ShouldScheduleFluidUpdate(),
            "The above-surface fast path must clear the fluid-update flag.");

        Aquifer lavaAquifer = config.Create(
            CreateSamplers(config),
            new LegacyRandomSource(918273L).ForkPositional(),
            volume,
            static (_, _, _) => new Aquifer.FluidStatus(100, Blocks.LAVA.DefaultState));

        BlockState? lava = lavaAquifer.ComputeSubstance(0, 0, 0, -1.0);
        Require(ReferenceEquals(lava, Blocks.LAVA.DefaultState),
            "NoiseBasedAquifer must preserve the global lava shortcut.");
        Require(!lavaAquifer.ShouldScheduleFluidUpdate(),
            "The global lava shortcut must clear the fluid-update flag.");
    }

    private static void CheckNoiseBasedAquiferNormalSampling()
    {
        Aquifer.Config config = CreateConfig(
            floodedness: 1.0f,
            preliminarySurface: 0.0f);
        Aquifer aquifer = config.Create(
            CreateSamplers(config),
            new LegacyRandomSource(123456789L).ForkPositional(),
            new DensityVolume(16, 16, 16, 0, -8, 0),
            static (_, _, _) => new Aquifer.FluidStatus(100, Blocks.WATER.DefaultState));

        BlockState? first = aquifer.ComputeSubstance(0, 0, 0, -1.0);
        bool shouldScheduleUpdate = aquifer.ShouldScheduleFluidUpdate();
        BlockState? second = aquifer.ComputeSubstance(1, 0, 0, -1.0);

        Require(ReferenceEquals(first, Blocks.WATER.DefaultState),
            "The regular aquifer sampler must fill a submerged solid sample with the nearest cached global water status.");
        Require(ReferenceEquals(second, Blocks.WATER.DefaultState),
            "Neighboring aquifer samples must remain within the precomputed cell grid and preserve the water status.");
        Require(!shouldScheduleUpdate && !aquifer.ShouldScheduleFluidUpdate(),
            "Uniform fluid statuses must not schedule a flowing-fluid update.");
    }

    private static Aquifer.Config CreateConfig(float floodedness, float preliminarySurface)
    {
        var barrier = new ConstantFunction(0.0f);
        var flooded = new ConstantFunction(floodedness);
        var spread = new ConstantFunction(0.0f);
        var lava = new ConstantFunction(0.0f);
        var exclusion = new ConstantFunction(0.0f);
        var surface = new ConstantFunction(preliminarySurface);

        _lastFunctions = [barrier, flooded, spread, lava, exclusion, surface];
        return new Aquifer.Config(barrier, flooded, spread, lava, exclusion, surface);
    }

    private static readonly List<DensityFunction> _lastFunctions = new();

    private static TestDensitySamplerSet CreateSamplers(Aquifer.Config config)
    {
        var samplers = new TestDensitySamplerSet();
        samplers.Add(config.BarrierNoise, 0.0f);
        samplers.Add(config.FluidLevelFloodednessNoise, config.FluidLevelFloodednessNoise is ConstantFunction flooded
            ? flooded.Value
            : 0.0f);
        samplers.Add(config.FluidLevelSpreadNoise, 0.0f);
        samplers.Add(config.LavaNoise, 0.0f);
        samplers.Add(config.Exclusion, 0.0f);
        samplers.Add(config.SurfaceLevel, config.SurfaceLevel is ConstantFunction surface
            ? surface.Value
            : 0.0f);
        return samplers;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
