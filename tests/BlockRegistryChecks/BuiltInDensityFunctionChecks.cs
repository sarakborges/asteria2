using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.Synth;

internal static class BuiltInDensityFunctionChecks
{
    private sealed class UnusedCompileContext : DensityFunction.CompileContext
    {
        public Noise CreateNoiseSampler(Holder<NormalNoise> parameters) =>
            throw new InvalidOperationException("The initial density-function entries must not require noise samplers.");

        public IRandomSource CreateRandom(ResourceLocation seed) =>
            throw new InvalidOperationException("The initial density-function entries must not require random sources.");

        public IRandomSource CreateEndIslandRandom() =>
            throw new InvalidOperationException("The initial density-function entries must not require End-island random sources.");
    }

    public static void Run()
    {
        CheckRegistryAndRegistrationOrder();
        CheckZeroFunction();
        CheckYGradient();
        CheckOreVeinFunctions();
        CheckShiftFunctions();
        CheckBase3dNoiseFunctions();
    }

    private static void CheckRegistryAndRegistrationOrder()
    {
        Registry<DensityFunction> registry = BuiltInRegistries.DensityFunction;
        string[] actualPaths = registry.Entries
            .Select(holder => holder.Key.Location.Path)
            .ToArray();

        Require(
            registry.Location == ResourceLocation.Parse("asteria:worldgen/density_function") &&
            registry.Key.RegistryLocation == BuiltInRegistries.RootLocation,
            "The density-function value registry must use Asteria's worldgen/density_function registry key.");

        Require(
            actualPaths.SequenceEqual(new[]
            {
                "zero", "y",
                "overworld/ore_vein/toggle",
                "overworld/ore_vein/richness",
                "overworld/ore_vein/mask",
                "overworld/ore_vein/copper_density",
                "overworld/ore_vein/iron_density",
                "overworld/ore_vein/gap",
                "shift_x", "shift_z",
                "overworld/base_3d_noise", "nether/base_3d_noise", "end/base_3d_noise"
            }, StringComparer.Ordinal),
            "The density-function registry must preserve the Minecraft 26.3 NoiseRouterData.bootstrap order for the ported entries.");

        Require(
            registry.Entries.All(holder => holder.IsBound),
            "Every bootstrapped density-function value must have a bound registry holder.");
    }

    private static void CheckZeroFunction()
    {
        Holder<DensityFunction>.Reference holder =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.ZERO) ??
            throw new InvalidOperationException("Missing built-in zero density-function holder.");

        Require(
            holder.IsBound &&
            ReferenceEquals(holder.Value, DensityFunctions.Zero()) &&
            holder.Value is ConstantFunction { Value: 0.0f },
            "The zero density-function entry must register the canonical zero singleton.");
    }

    private static void CheckYGradient()
    {
        Holder<DensityFunction>.Reference holder =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.Y) ??
            throw new InvalidOperationException("Missing built-in y density-function holder.");

        Require(holder.IsBound, "The y density-function holder must be bound.");

        if (holder.Value is not GradientFunction gradient)
            throw new InvalidOperationException("The y density-function entry must use a gradient node.");

        int belowBottom = NoiseSettings.DimensionMinY * 2;
        int aboveTop = NoiseSettings.DimensionMaxY * 2;

        Require(
            ReferenceEquals(gradient.Axis, Direction.Axis.Y) &&
            gradient.Tiling == TilingMode.CLAMP_TO_EDGE &&
            gradient.FromCoordinate == belowBottom &&
            gradient.ToCoordinate == aboveTop &&
            gradient.FromValue == belowBottom &&
            gradient.ToValue == aboveTop,
            "The y gradient must use the exact dimension-derived vertical endpoints and clamped tiling.");

        DensitySampler sampler = gradient.CompileSampler(new UnusedCompileContext());
        SamplerContext context = SamplerContext.EmptyUncached;

        Require(
            sampler.SampleValue(context, 0, belowBottom, 0) == belowBottom &&
            sampler.SampleValue(context, 0, 0, 0) == 0.0f &&
            sampler.SampleValue(context, 0, aboveTop, 0) == aboveTop,
            "The y gradient must map the dimension's bottom, origin and top to their matching density values.");

        Require(
            sampler.SampleValue(context, 0, belowBottom - 1, 0) == belowBottom &&
            sampler.SampleValue(context, 0, aboveTop + 1, 0) == aboveTop,
            "The y gradient must clamp samples outside the dimension-derived vertical bounds.");
    }


    private static void CheckShiftFunctions()
    {
        Holder<NormalNoise> noiseHolder =
            BuiltInRegistries.Noise.GetHolder(Noises.SHIFT) ??
            throw new InvalidOperationException("Missing built-in offset noise holder.");

        Holder<DensityFunction>.Reference shiftXHolder =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.SHIFT_X) ??
            throw new InvalidOperationException("Missing built-in shift_x density-function holder.");
        Holder<DensityFunction>.Reference shiftZHolder =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.SHIFT_Z) ??
            throw new InvalidOperationException("Missing built-in shift_z density-function holder.");

        Require(
            shiftXHolder.Value is ShiftNoiseFunction.ShiftA shiftX &&
            ReferenceEquals(shiftX.OffsetNoise, noiseHolder),
            "The shift_x density function must use the vanilla ShiftA node and the registered offset noise holder.");

        Require(
            shiftZHolder.Value is ShiftNoiseFunction.ShiftB shiftZ &&
            ReferenceEquals(shiftZ.OffsetNoise, noiseHolder),
            "The shift_z density function must use the vanilla ShiftB node and the registered offset noise holder.");

        Require(
            shiftXHolder.Value.DomainAxes() == (DensityFunction.AXIS_X | DensityFunction.AXIS_Z) &&
            shiftZHolder.Value.DomainAxes() == (DensityFunction.AXIS_X | DensityFunction.AXIS_Z) &&
            shiftXHolder.Value.Range().Equals(shiftZHolder.Value.Range()),
            "The registered horizontal shift functions must preserve their shared X/Z domain and range contract.");
    }

    private static void CheckOreVeinFunctions()
    {
        Holder<DensityFunction>.Reference toggle =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.ORE_VEIN_TOGGLE) ??
            throw new InvalidOperationException("Missing ore-vein toggle density function.");
        Holder<DensityFunction>.Reference richness =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.ORE_VEIN_RICHNESS) ??
            throw new InvalidOperationException("Missing ore-vein richness density function.");
        Holder<DensityFunction>.Reference mask =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.ORE_VEIN_MASK) ??
            throw new InvalidOperationException("Missing ore-vein mask density function.");
        Holder<DensityFunction>.Reference copper =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.ORE_VEIN_COPPER_DENSITY) ??
            throw new InvalidOperationException("Missing copper-vein density function.");
        Holder<DensityFunction>.Reference iron =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.ORE_VEIN_IRON_DENSITY) ??
            throw new InvalidOperationException("Missing iron-vein density function.");
        Holder<DensityFunction>.Reference gap =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.ORE_VEIN_GAP) ??
            throw new InvalidOperationException("Missing ore-vein gap density function.");

        Require(
            toggle.Value is CacheFunction &&
            mask.Value is CacheFunction &&
            richness.Value is LerpFunction &&
            copper.Value is RangeChoiceFunction &&
            iron.Value is RangeChoiceFunction &&
            gap.Value is BinaryFunction { Operation: BinaryFunction.Type.SUB },
            "Ore-vein density functions must use the vanilla cache, interpolation, range-choice and subtraction nodes.");

        Require(
            BuiltInRegistries.Noise.GetHolder(Noises.ORE_VEININESS) is not null &&
            BuiltInRegistries.Noise.GetHolder(Noises.ORE_VEIN_A) is not null &&
            BuiltInRegistries.Noise.GetHolder(Noises.ORE_VEIN_B) is not null &&
            BuiltInRegistries.Noise.GetHolder(Noises.ORE_GAP) is not null,
            "Ore-vein density functions must reference the registered vanilla ore noise parameters.");

        Require(
            toggle.Value.Range().Min() <= -1.0f &&
            toggle.Value.Range().Max() >= 1.0f &&
            richness.Value.Range().Min() == 0.1f &&
            richness.Value.Range().Max() == 0.3f &&
            copper.Value.Range().Min() <= -1.0f &&
            copper.Value.Range().Max() >= 0.7f &&
            iron.Value.Range().Min() <= -1.0f &&
            iron.Value.Range().Max() >= 0.7f,
            "Ore-vein density-function ranges must preserve the vanilla toggle, richness and vein-body bounds.");
    }

    private static void CheckBase3dNoiseFunctions()
    {
        CheckBase3dNoise(
            DensityFunctionKeys.BASE_3D_NOISE_OVERWORLD,
            0.25, 0.125, 80.0, 160.0, 8.0,
            "overworld/base_3d_noise");
        CheckBase3dNoise(
            DensityFunctionKeys.BASE_3D_NOISE_NETHER,
            0.25, 0.375, 80.0, 60.0, 8.0,
            "nether/base_3d_noise");
        CheckBase3dNoise(
            DensityFunctionKeys.BASE_3D_NOISE_END,
            0.25, 0.25, 80.0, 160.0, 4.0,
            "end/base_3d_noise");
    }

    private static void CheckBase3dNoise(
        RegistryKey<DensityFunction> key,
        double xzScale,
        double yScale,
        double xzFactor,
        double yFactor,
        double smearScaleMultiplier,
        string expectedPath)
    {
        Holder<DensityFunction>.Reference holder =
            BuiltInRegistries.DensityFunction.GetHolder(key) ??
            throw new InvalidOperationException($"Missing built-in {expectedPath} density-function holder.");

        Require(
            holder.IsBound &&
            holder.Key.Location.Path == expectedPath &&
            holder.Value is BlendedNoise
            {
                XzScale: var actualXzScale,
                YScale: var actualYScale,
                XzFactor: var actualXzFactor,
                YFactor: var actualYFactor,
                SmearScaleMultiplier: var actualSmearScaleMultiplier
            } &&
            actualXzScale == xzScale &&
            actualYScale == yScale &&
            actualXzFactor == xzFactor &&
            actualYFactor == yFactor &&
            actualSmearScaleMultiplier == smearScaleMultiplier,
            $"The {expectedPath} entry must use the vanilla old_blended_noise node and exact 26.3 parameters.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
