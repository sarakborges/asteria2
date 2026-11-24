using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Operations;
using Asteria.World.LevelGen.Synth;
using DensityFunctionFactories = Asteria.World.LevelGen.DensityFunctions.DensityFunctions;

namespace Asteria.World.LevelGen;

/// <summary>
/// Partial vanilla Minecraft Java 26.3 NoiseRouterData density-function bootstrap.
/// </summary>
internal static class BuiltInDensityFunctions
{
    internal const int Count = 53;

    private readonly record struct OreVeinDefinition(
        RegistryKey<DensityFunction> DensityKey,
        int MinY,
        int MaxY,
        bool WhenTogglePositive);

    private readonly record struct Base3dNoiseDefinition(
        RegistryKey<DensityFunction> Key,
        double XzScale,
        double YScale,
        double XzFactor,
        double YFactor,
        double SmearScaleMultiplier);

    private readonly record struct ClimateNoiseDefinition(
        RegistryKey<DensityFunction> DensityKey,
        RegistryKey<NormalNoise> NoiseKey,
        bool Cached);

    // These are vanilla's data variants; registration and density construction stay type-agnostic.
    private static readonly OreVeinDefinition[] OreVeins =
    [
        new(DensityFunctionKeys.ORE_VEIN_COPPER_DENSITY, 0, 50, true),
        new(DensityFunctionKeys.ORE_VEIN_IRON_DENSITY, -60, -8, false)
    ];

    private static readonly Base3dNoiseDefinition[] Base3dNoises =
    [
        new(DensityFunctionKeys.BASE_3D_NOISE_OVERWORLD, 0.25, 0.125, 80.0, 160.0, 8.0),
        new(DensityFunctionKeys.BASE_3D_NOISE_NETHER, 0.25, 0.375, 80.0, 60.0, 8.0),
        new(DensityFunctionKeys.BASE_3D_NOISE_END, 0.25, 0.25, 80.0, 160.0, 4.0)
    ];

    private static readonly ClimateNoiseDefinition[] OverworldClimateNoises =
    [
        new(DensityFunctionKeys.OVERWORLD_TEMPERATURE, Noises.TEMPERATURE, false),
        new(DensityFunctionKeys.OVERWORLD_LARGE_TEMPERATURE, Noises.TEMPERATURE_LARGE, false),
        new(DensityFunctionKeys.OVERWORLD_VEGETATION, Noises.VEGETATION, false),
        new(DensityFunctionKeys.OVERWORLD_LARGE_VEGETATION, Noises.VEGETATION_LARGE, false),
        new(DensityFunctionKeys.OVERWORLD_CONTINENTS, Noises.CONTINENTALNESS, true),
        new(DensityFunctionKeys.OVERWORLD_EROSION, Noises.EROSION, true),
        new(DensityFunctionKeys.RIDGES, Noises.RIDGE, true)
    ];

    private static readonly ClimateNoiseDefinition[] LargeBiomeTerrainClimateNoises =
    [
        new(DensityFunctionKeys.OVERWORLD_LARGE_CONTINENTS, Noises.CONTINENTALNESS_LARGE, true),
        new(DensityFunctionKeys.OVERWORLD_LARGE_EROSION, Noises.EROSION_LARGE, true)
    ];

    internal static void RegisterAll()
    {
        Registry<DensityFunction> registry = BuiltInRegistries.DensityFunction;

        registry.Register(
            DensityFunctionKeys.ZERO,
            DensityFunctionFactories.Zero(),
            RegistrationInfo.BuiltIn);

        int belowBottom = NoiseSettings.DimensionMinY * 2;
        int aboveTop = NoiseSettings.DimensionMaxY * 2;

        registry.Register(
            DensityFunctionKeys.Y,
            DensityFunctionFactories.YClampedGradient(
                belowBottom,
                aboveTop,
                belowBottom,
                aboveTop),
            RegistrationInfo.BuiltIn);

        RegisterOreVeins(registry);

        Holder<NormalNoise> shiftNoise =
            BuiltInRegistries.Noise.GetHolder(Noises.SHIFT) ??
            throw new InvalidOperationException("Missing built-in offset noise parameters.");

        registry.Register(
            DensityFunctionKeys.SHIFT_X,
            DensityFunctionFactories.Cache(DensityFunctionFactories.ShiftA(shiftNoise)),
            RegistrationInfo.BuiltIn);

        registry.Register(
            DensityFunctionKeys.SHIFT_Z,
            DensityFunctionFactories.Cache(DensityFunctionFactories.ShiftB(shiftNoise)),
            RegistrationInfo.BuiltIn);

        foreach (Base3dNoiseDefinition definition in Base3dNoises)
        {
            registry.Register(
                definition.Key,
                new BlendedNoise(
                    definition.XzScale,
                    definition.YScale,
                    definition.XzFactor,
                    definition.YFactor,
                    definition.SmearScaleMultiplier),
                RegistrationInfo.BuiltIn);
        }

        RegisterOverworldClimateFunctions(registry);

        RegisterOverworldTerrainFunctions(registry);
    }

    private static void RegisterOverworldClimateFunctions(Registry<DensityFunction> registry)
    {
        DensityFunction shiftX = GetFunction(registry, DensityFunctionKeys.SHIFT_X);
        DensityFunction shiftZ = GetFunction(registry, DensityFunctionKeys.SHIFT_Z);

        foreach (ClimateNoiseDefinition definition in OverworldClimateNoises)
        {
            DensityFunction shiftedNoise = DensityFunctionFactories.ShiftedNoise2d(
                shiftX,
                shiftZ,
                0.25,
                GetNoise(definition.NoiseKey));

            RegisterAndWrap(
                registry,
                definition.DensityKey,
                definition.Cached
                    ? DensityFunctionFactories.Cache(shiftedNoise)
                    : shiftedNoise);
        }

        DensityFunction ridges = GetFunction(registry, DensityFunctionKeys.RIDGES);

        registry.Register(
            DensityFunctionKeys.RIDGES_FOLDED,
            PeaksAndValleys(ridges),
            RegistrationInfo.BuiltIn);

        foreach (ClimateNoiseDefinition definition in LargeBiomeTerrainClimateNoises)
        {
            DensityFunction shiftedNoise = DensityFunctionFactories.ShiftedNoise2d(
                shiftX,
                shiftZ,
                0.25,
                GetNoise(definition.NoiseKey));

            RegisterAndWrap(
                registry,
                definition.DensityKey,
                DensityFunctionFactories.Cache(shiftedNoise));
        }
    }

    private static void RegisterOverworldTerrainFunctions(Registry<DensityFunction> registry)
    {
        DensityFunction jaggedNoise = DensityFunctionFactories.Noise(
            GetNoise(Noises.JAGGED),
            1500.0,
            0.0);

        RegisterTerrainNoises(
            registry,
            jaggedNoise,
            GetFunction(registry, DensityFunctionKeys.OVERWORLD_CONTINENTS),
            GetFunction(registry, DensityFunctionKeys.OVERWORLD_EROSION),
            DensityFunctionKeys.OVERWORLD_FUNCTIONS,
            amplified: false);

        RegisterTerrainNoises(
            registry,
            jaggedNoise,
            GetFunction(registry, DensityFunctionKeys.OVERWORLD_LARGE_CONTINENTS),
            GetFunction(registry, DensityFunctionKeys.OVERWORLD_LARGE_EROSION),
            DensityFunctionKeys.LARGE_OVERWORLD_FUNCTIONS,
            amplified: false);

        RegisterTerrainNoises(
            registry,
            jaggedNoise,
            GetFunction(registry, DensityFunctionKeys.OVERWORLD_CONTINENTS),
            GetFunction(registry, DensityFunctionKeys.OVERWORLD_EROSION),
            DensityFunctionKeys.AMPLIFIED_OVERWORLD_FUNCTIONS,
            amplified: true);

        RegisterOverworldCaveFunctions(registry);
    }

    private static void RegisterTerrainNoises(
        Registry<DensityFunction> registry,
        DensityFunction jaggedNoise,
        DensityFunction continentsFunction,
        DensityFunction erosionFunction,
        OverworldFunctionSet<RegistryKey<DensityFunction>> names,
        bool amplified)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(jaggedNoise);
        ArgumentNullException.ThrowIfNull(continentsFunction);
        ArgumentNullException.ThrowIfNull(erosionFunction);

        var continents = new SplineFunction.Coordinate(continentsFunction);
        var erosion = new SplineFunction.Coordinate(erosionFunction);
        var weirdness = new SplineFunction.Coordinate(
            GetFunction(registry, DensityFunctionKeys.RIDGES));
        var ridges = new SplineFunction.Coordinate(
            GetFunction(registry, DensityFunctionKeys.RIDGES_FOLDED));

        DensityFunction offsetSpline = DensityFunctionFactories.Spline(
            TerrainProvider.OverworldOffset(continents, erosion, ridges, amplified));
        DensityFunction offset = RegisterAndWrap(
            registry,
            names.Offset,
            SplineWithBlending(
                DensityFunctionFactories.Add(
                    DensityFunctionFactories.Constant(-0.50375f),
                    offsetSpline),
                DensityFunctionFactories.BlendOffset()));

        DensityFunction factorSpline = DensityFunctionFactories.Spline(
            TerrainProvider.OverworldFactor(
                continents,
                erosion,
                weirdness,
                ridges,
                amplified));
        DensityFunction factor = RegisterAndWrap(
            registry,
            names.Factor,
            SplineWithBlending(
                factorSpline,
                DensityFunctionFactories.Constant(10.0f)));

        DensityFunction depth = RegisterAndWrap(
            registry,
            names.Depth,
            OffsetToDepth(offset));

        DensityFunction jaggednessSpline = DensityFunctionFactories.Spline(
            TerrainProvider.OverworldJaggedness(
                continents,
                erosion,
                weirdness,
                ridges,
                amplified));
        DensityFunction unscaledJaggedness = RegisterAndWrap(
            registry,
            names.Jaggedness,
            SplineWithBlending(
                jaggednessSpline,
                DensityFunctionFactories.Zero()));

        DensityFunction jaggedness = DensityFunctionFactories.Cache(
            DensityFunctionFactories.Mul(
                unscaledJaggedness,
                DensityFunctionFactories.HalfNegative(jaggedNoise)));

        DensityFunction initialDensity = NoiseGradientDensity(
            factor,
            DensityFunctionFactories.Add(depth, jaggedness));

        DensityFunction slopedCheese = RegisterAndWrap(
            registry,
            names.SlopedCheese,
            DensityFunctionFactories.Cache(
                DensityFunctionFactories.Add(
                    initialDensity,
                    GetFunction(registry, DensityFunctionKeys.BASE_3D_NOISE_OVERWORLD))));

        DensityFunction preliminarySurfaceLevel = RegisterAndWrap(
            registry,
            names.PreliminarySurfaceLevel,
            PreliminarySurfaceLevel(offset, factor, amplified));

        registry.Register(
            names.ChunkSurfaceLevel,
            DensityFunctionFactories.Interpolate(
                preliminarySurfaceLevel,
                16,
                1),
            RegistrationInfo.BuiltIn);

        DensityFunction surfaceWithEntrances = DensityFunctionFactories.Min(
            slopedCheese,
            DensityFunctionFactories.Mul(
                GetFunction(registry, DensityFunctionKeys.ENTRANCES),
                5.0f));

        DensityFunction caves = DensityFunctionFactories.RangeChoice(
            slopedCheese,
            -1_000_000.0f,
            1.5625f,
            surfaceWithEntrances,
            BuildUnderground(registry, slopedCheese));

        DensityFunction postProcessed = PostProcess(
            SlideOverworld(amplified, caves),
            4,
            8);

        registry.Register(
            names.FinalDensity,
            DensityFunctionFactories.Min(
                postProcessed,
                GetFunction(registry, DensityFunctionKeys.NOODLE)),
            RegistrationInfo.BuiltIn);
    }

    private static void RegisterOverworldCaveFunctions(Registry<DensityFunction> registry)
    {
        DensityFunction spaghettiRoughnessNoise =
            DensityFunctionFactories.Noise(GetNoise(Noises.SPAGHETTI_ROUGHNESS));
        DensityFunction spaghettiRoughnessModulator =
            DensityFunctionFactories.MappedNoise(
                GetNoise(Noises.SPAGHETTI_ROUGHNESS_MODULATOR),
                0.0f,
                -0.1f);

        RegisterAndWrap(
            registry,
            DensityFunctionKeys.SPAGHETTI_ROUGHNESS_FUNCTION,
            DensityFunctionFactories.Cache(
                DensityFunctionFactories.Mul(
                    spaghettiRoughnessModulator,
                    DensityFunctionFactories.Add(
                        DensityFunctionFactories.Abs(spaghettiRoughnessNoise),
                        -0.4f))));

        DensityFunction spaghetti2DThickness = DensityFunctionFactories.Cache(
            DensityFunctionFactories.MappedNoise(
                GetNoise(Noises.SPAGHETTI_2D_THICKNESS),
                2.0,
                1.0,
                -0.6f,
                -1.3f));

        registry.Register(
            DensityFunctionKeys.SPAGHETTI_2D_THICKNESS_MODULATOR,
            spaghetti2DThickness,
            RegistrationInfo.BuiltIn);

        registry.Register(
            DensityFunctionKeys.SPAGHETTI_2D,
            BuildSpaghetti2D(registry, spaghetti2DThickness),
            RegistrationInfo.BuiltIn);

        registry.Register(
            DensityFunctionKeys.ENTRANCES,
            BuildEntrances(registry),
            RegistrationInfo.BuiltIn);

        registry.Register(
            DensityFunctionKeys.NOODLE,
            BuildNoodle(registry),
            RegistrationInfo.BuiltIn);

        registry.Register(
            DensityFunctionKeys.PILLARS,
            BuildPillars(),
            RegistrationInfo.BuiltIn);
    }

    private static DensityFunction BuildEntrances(Registry<DensityFunction> registry)
    {
        DensityFunction spaghetti3DRarityModulator =
            DensityFunctionFactories.Cache(
                DensityFunctionFactories.Noise(
                    GetNoise(Noises.SPAGHETTI_3D_RARITY),
                    2.0,
                    1.0));

        DensityFunction spaghetti3DThicknessModulator =
            DensityFunctionFactories.MappedNoise(
                GetNoise(Noises.SPAGHETTI_3D_THICKNESS),
                -0.065f,
                -0.088f);

        DensityFunction spaghetti3DCave1 = WrapRarity3D(
            spaghetti3DRarityModulator,
            GetNoise(Noises.SPAGHETTI_3D_1));
        DensityFunction spaghetti3DCave2 = WrapRarity3D(
            spaghetti3DRarityModulator,
            GetNoise(Noises.SPAGHETTI_3D_2));

        DensityFunction spaghetti3D = DensityFunctionFactories.Clamp(
            DensityFunctionFactories.Add(
                DensityFunctionFactories.Max(spaghetti3DCave1, spaghetti3DCave2),
                spaghetti3DThicknessModulator),
            -1.0f,
            1.0f);

        DensityFunction spaghettiRoughness =
            GetFunction(registry, DensityFunctionKeys.SPAGHETTI_ROUGHNESS_FUNCTION);
        DensityFunction bigEntranceNoise =
            DensityFunctionFactories.Noise(GetNoise(Noises.CAVE_ENTRANCE), 0.75, 0.5);
        DensityFunction bigEntrances = DensityFunctionFactories.Add(
            DensityFunctionFactories.Add(bigEntranceNoise, 0.37f),
            DensityFunctionFactories.YClampedGradient(-10, 30, 0.3f, 0.0f));

        return DensityFunctionFactories.Cache(
            DensityFunctionFactories.Min(
                bigEntrances,
                DensityFunctionFactories.Add(spaghettiRoughness, spaghetti3D)));
    }

    private static DensityFunction BuildNoodle(Registry<DensityFunction> registry)
    {
        DensityFunction y = GetFunction(registry, DensityFunctionKeys.Y);

        DensityFunction noodleToggle = YLimitedInterpolatable(
            y,
            DensityFunctionFactories.Noise(GetNoise(Noises.NOODLE)),
            -60,
            320,
            -1.0f,
            4,
            8);

        DensityFunction noodleThickness = YLimitedInterpolatable(
            y,
            DensityFunctionFactories.MappedNoise(
                GetNoise(Noises.NOODLE_THICKNESS),
                1.0,
                -0.05f,
                -0.1f),
            -60,
            320,
            0.0f,
            4,
            8);

        const double noodleRidgeFrequency = 2.6666666666666665;
        DensityFunction noodleRidgeA = YLimitedInterpolatable(
            y,
            DensityFunctionFactories.Noise(
                GetNoise(Noises.NOODLE_RIDGE_A),
                noodleRidgeFrequency,
                noodleRidgeFrequency),
            -60,
            320,
            0.0f,
            4,
            8);

        DensityFunction noodleRidgeB = YLimitedInterpolatable(
            y,
            DensityFunctionFactories.Noise(
                GetNoise(Noises.NOODLE_RIDGE_B),
                noodleRidgeFrequency,
                noodleRidgeFrequency),
            -60,
            320,
            0.0f,
            4,
            8);

        DensityFunction noodleRidged = DensityFunctionFactories.Mul(
            DensityFunctionFactories.Max(
                DensityFunctionFactories.Abs(noodleRidgeA),
                DensityFunctionFactories.Abs(noodleRidgeB)),
            1.5f);

        return DensityFunctionFactories.RangeChoice(
            noodleToggle,
            -1_000_000.0f,
            0.0f,
            DensityFunctionFactories.Constant(64.0f),
            DensityFunctionFactories.Add(noodleThickness, noodleRidged));
    }

    private static DensityFunction BuildPillars()
    {
        DensityFunction pillarNoise =
            DensityFunctionFactories.Noise(GetNoise(Noises.PILLAR), 25.0, 0.3);
        DensityFunction pillarRareness =
            DensityFunctionFactories.MappedNoise(
                GetNoise(Noises.PILLAR_RARENESS),
                0.0f,
                -2.0f);
        DensityFunction pillarThickness =
            DensityFunctionFactories.MappedNoise(
                GetNoise(Noises.PILLAR_THICKNESS),
                0.0f,
                1.1f);
        DensityFunction pillarsWithRareness = DensityFunctionFactories.Add(
            DensityFunctionFactories.Mul(pillarNoise, 2.0f),
            pillarRareness);

        return DensityFunctionFactories.Cache(
            DensityFunctionFactories.Mul(
                pillarsWithRareness,
                DensityFunctionFactories.Cube(pillarThickness)));
    }

    private static DensityFunction BuildSpaghetti2D(
        Registry<DensityFunction> registry,
        DensityFunction spaghetti2DThickness)
    {
        DensityFunction spaghetti2DRarityModulator =
            DensityFunctionFactories.Noise(
                GetNoise(Noises.SPAGHETTI_2D_MODULATOR),
                2.0,
                1.0);

        DensityFunction spaghetti2DCave = WrapRarity2D(
            spaghetti2DRarityModulator,
            GetNoise(Noises.SPAGHETTI_2D));

        DensityFunction spaghetti2DElevationModulator =
            DensityFunctionFactories.MappedNoise(
                GetNoise(Noises.SPAGHETTI_2D_ELEVATION),
                0.0,
                -8.0f,
                8.0f);

        DensityFunction slopedSpaghetti = DensityFunctionFactories.Abs(
            DensityFunctionFactories.Add(
                DensityFunctionFactories.Cache(spaghetti2DElevationModulator),
                DensityFunctionFactories.YClampedGradient(-64, 320, 8.0f, -40.0f)));

        DensityFunction layerRidged = DensityFunctionFactories.Cube(
            DensityFunctionFactories.Add(slopedSpaghetti, spaghetti2DThickness));

        DensityFunction caveNoise = DensityFunctionFactories.Add(
            spaghetti2DCave,
            DensityFunctionFactories.Mul(spaghetti2DThickness, 0.083f));

        return DensityFunctionFactories.Clamp(
            DensityFunctionFactories.Max(caveNoise, layerRidged),
            -1.0f,
            1.0f);
    }

    private static DensityFunction BuildUnderground(
        Registry<DensityFunction> registry,
        DensityFunction slopedCheese)
    {
        DensityFunction spaghetti2D =
            GetFunction(registry, DensityFunctionKeys.SPAGHETTI_2D);
        DensityFunction spaghettiRoughness =
            GetFunction(registry, DensityFunctionKeys.SPAGHETTI_ROUGHNESS_FUNCTION);
        DensityFunction entrances =
            GetFunction(registry, DensityFunctionKeys.ENTRANCES);

        DensityFunction layerNoise =
            DensityFunctionFactories.Noise(GetNoise(Noises.CAVE_LAYER), 8.0);
        DensityFunction layerizedCaverns =
            DensityFunctionFactories.Mul(
                DensityFunctionFactories.Square(layerNoise),
                4.0f);

        DensityFunction cheese =
            DensityFunctionFactories.Noise(
                GetNoise(Noises.CAVE_CHEESE),
                0.6666666666666666);

        DensityFunction solidifiedCheeseWithTopSlide =
            DensityFunctionFactories.Add(
                DensityFunctionFactories.Clamp(
                    DensityFunctionFactories.Add(cheese, 0.27f),
                    -1.0f,
                    1.0f),
                DensityFunctionFactories.Clamp(
                    DensityFunctionFactories.Add(
                        DensityFunctionFactories.Mul(slopedCheese, -0.64f),
                        1.5f),
                    0.0f,
                    0.5f));

        DensityFunction baseCaveDensity = DensityFunctionFactories.Add(
            layerizedCaverns,
            solidifiedCheeseWithTopSlide);

        DensityFunction undergroundSubtractions = DensityFunctionFactories.Min(
            DensityFunctionFactories.Min(baseCaveDensity, entrances),
            DensityFunctionFactories.Add(spaghetti2D, spaghettiRoughness));

        DensityFunction pillarsWithoutCutoff =
            GetFunction(registry, DensityFunctionKeys.PILLARS);
        DensityFunction pillars = DensityFunctionFactories.RangeChoice(
            pillarsWithoutCutoff,
            -1_000_000.0f,
            0.03f,
            DensityFunctionFactories.Constant(-1_000_000.0f),
            pillarsWithoutCutoff);

        return DensityFunctionFactories.Max(undergroundSubtractions, pillars);
    }

    private static DensityFunction PostProcess(
        DensityFunction slide,
        int cellSizeXz,
        int cellSizeY)
    {
        DensityFunction blended = DensityFunctionFactories.BlendDensity(slide);
        DensityFunction scaled = DensityFunctionFactories.Mul(blended, 0.64f);
        DensityFunction interpolated = DensityFunctionFactories.Interpolate(
            scaled,
            cellSizeXz,
            cellSizeY);

        return DensityFunctionFactories.Squeeze(interpolated);
    }

    private static DensityFunction WrapRarity2D(
        DensityFunction input,
        Holder<NormalNoise> noise) =>
        DensityFunctionFactories.Abs(
            DensityFunctionFactories.IntervalSelect(
                input,
                [-0.75f, -0.5f, 0.5f, 0.75f],
                [
                    NoiseFunctionForRarity(noise, 0.5f),
                    NoiseFunctionForRarity(noise, 0.75f),
                    NoiseFunctionForRarity(noise, 1.0f),
                    NoiseFunctionForRarity(noise, 2.0f),
                    NoiseFunctionForRarity(noise, 3.0f)
                ]));

    private static DensityFunction WrapRarity3D(
        DensityFunction input,
        Holder<NormalNoise> noise) =>
        DensityFunctionFactories.Abs(
            DensityFunctionFactories.IntervalSelect(
                input,
                [-0.5f, 0.0f, 0.5f],
                [
                    NoiseFunctionForRarity(noise, 0.75f),
                    NoiseFunctionForRarity(noise, 1.0f),
                    NoiseFunctionForRarity(noise, 1.5f),
                    NoiseFunctionForRarity(noise, 2.0f)
                ]));

    private static DensityFunction NoiseFunctionForRarity(
        Holder<NormalNoise> noise,
        float rarity) =>
        DensityFunctionFactories.Mul(
            DensityFunctionFactories.Noise(noise, 1.0 / rarity, 1.0 / rarity),
            rarity);

    private static DensityFunction SplineWithBlending(
        DensityFunction spline,
        DensityFunction blendingTarget) =>
        DensityFunctionFactories.Cache(
            DensityFunctionFactories.Lerp(
                DensityFunctionFactories.BlendAlpha(),
                blendingTarget,
                spline));

    private static DensityFunction PeaksAndValleys(DensityFunction weirdness)
    {
        DensityFunction absoluteOffset = DensityFunctionFactories.Abs(
            DensityFunctionFactories.Add(
                DensityFunctionFactories.Abs(weirdness),
                -0.6666667f));
        DensityFunction centered = DensityFunctionFactories.Add(
            absoluteOffset,
            -0.33333334f);
        return DensityFunctionFactories.Mul(centered, -3.0f);
    }

    /// <summary>
    /// Converts the vanilla Overworld offset channel into depth using the
    /// fixed -64..320 density-Y gradient from Minecraft Java 26.3.
    /// </summary>
    internal static DensityFunction OffsetToDepth(DensityFunction offset)
    {
        ArgumentNullException.ThrowIfNull(offset);

        return DensityFunctionFactories.Add(
            DensityFunctionFactories.YClampedGradient(-64, 320, 1.5f, -1.5f),
            offset);
    }

    /// <summary>
    /// Builds Minecraft Java 26.3's noise-gradient density expression.
    /// </summary>
    internal static DensityFunction NoiseGradientDensity(
        DensityFunction factor,
        DensityFunction depthWithJaggedness)
    {
        ArgumentNullException.ThrowIfNull(factor);
        ArgumentNullException.ThrowIfNull(depthWithJaggedness);

        DensityFunction gradientUnscaled =
            DensityFunctionFactories.Mul(depthWithJaggedness, factor);

        return DensityFunctionFactories.Mul(
            DensityFunctionFactories.QuarterNegative(gradientUnscaled),
            4.0f);
    }

    /// <summary>
    /// Applies Minecraft Java 26.3's Overworld top and bottom density slides.
    /// </summary>
    internal static DensityFunction SlideOverworld(
        bool amplified,
        DensityFunction density) =>
        Slide(
            density,
            -64,
            384,
            amplified ? 16 : 80,
            amplified ? 0 : 64,
            -0.078125f,
            0,
            24,
            amplified ? 0.4f : 0.1171875f);

    /// <summary>
    /// Applies the vanilla top and bottom Y-gradient slides in their original order.
    /// </summary>
    internal static DensityFunction Slide(
        DensityFunction density,
        int minY,
        int height,
        int topStartY,
        int topEndY,
        float topTarget,
        int bottomStartY,
        int bottomEndY,
        float bottomTarget)
    {
        ArgumentNullException.ThrowIfNull(density);

        DensityFunction topFactor = DensityFunctionFactories.YClampedGradient(
            minY + height - topStartY,
            minY + height - topEndY,
            1.0f,
            0.0f);

        DensityFunction slidTop = DensityFunctionFactories.Lerp(
            topFactor,
            topTarget,
            density);

        DensityFunction bottomFactor = DensityFunctionFactories.YClampedGradient(
            minY + bottomStartY,
            minY + bottomEndY,
            0.0f,
            1.0f);

        return DensityFunctionFactories.Lerp(
            bottomFactor,
            bottomTarget,
            slidTop);
    }

    /// <summary>
    /// Builds the vanilla Overworld preliminary surface-level function.
    /// </summary>
    internal static DensityFunction PreliminarySurfaceLevel(
        DensityFunction offset,
        DensityFunction factor,
        bool amplified)
    {
        ArgumentNullException.ThrowIfNull(offset);
        ArgumentNullException.ThrowIfNull(factor);

        DensityFunction upperBound = Remap(
            DensityFunctionFactories.Sub(
                DensityFunctionFactories.Div(
                    DensityFunctionFactories.Constant(0.2734375f),
                    factor),
                offset),
            1.5f,
            -1.5f,
            -64.0f,
            320.0f);

        upperBound = DensityFunctionFactories.Clamp(upperBound, -40.0f, 320.0f);

        DensityFunction depth = OffsetToDepth(offset);
        DensityFunction initialDensity = NoiseGradientDensity(factor, depth);
        DensityFunction slidDensity = SlideOverworld(
            amplified,
            DensityFunctionFactories.Clamp(
                DensityFunctionFactories.Add(initialDensity, -0.703125f),
                -64.0f,
                64.0f));

        DensityFunction density = DensityFunctionFactories.Add(
            slidDensity,
            -0.390625f);

        return new FindTopSurfaceFunction(density, upperBound, -64, 8);
    }

    private static void RegisterOreVeins(Registry<DensityFunction> registry)
    {
        Holder<NormalNoise> veininessNoise = GetNoise(Noises.ORE_VEININESS);
        Holder<NormalNoise> veinANoise = GetNoise(Noises.ORE_VEIN_A);
        Holder<NormalNoise> veinBNoise = GetNoise(Noises.ORE_VEIN_B);
        Holder<NormalNoise> gapNoise = GetNoise(Noises.ORE_GAP);

        DensityFunction y = GetFunction(registry, DensityFunctionKeys.Y);
        int veinMinY = OreVeins.Min(definition => definition.MinY);
        int veinMaxY = OreVeins.Max(definition => definition.MaxY);
        int interpolatedVeinMinY = Mth.FloorDiv(veinMinY, 8) * 8;
        int interpolatedVeinMaxY = (Mth.FloorDiv(veinMaxY, 8) + 1) * 8;

        DensityFunction veinToggle = RegisterAndWrap(
            registry,
            DensityFunctionKeys.ORE_VEIN_TOGGLE,
            DensityFunctionFactories.Cache(
                YLimitedInterpolatable(
                    y,
                    DensityFunctionFactories.Noise(veininessNoise, 1.5, 1.5),
                    interpolatedVeinMinY,
                    interpolatedVeinMaxY,
                    0,
                    4,
                    8)));

        registry.Register(
            DensityFunctionKeys.ORE_VEIN_RICHNESS,
            ClampedMap(
                DensityFunctionFactories.Abs(veinToggle),
                0.4f,
                0.6f,
                0.1f,
                0.3f),
            RegistrationInfo.BuiltIn);

        DensityFunction veinMask = RegisterAndWrap(
            registry,
            DensityFunctionKeys.ORE_VEIN_MASK,
            DensityFunctionFactories.Cache(
                CreateBaseOreVeinMask(
                    veinANoise,
                    veinBNoise,
                    y,
                    veinToggle,
                    interpolatedVeinMinY,
                    interpolatedVeinMaxY)));

        foreach (OreVeinDefinition definition in OreVeins)
        {
            registry.Register(
                definition.DensityKey,
                CreateOreVeinDensity(
                    definition.MinY,
                    definition.MaxY,
                    y,
                    veinToggle,
                    veinMask,
                    definition.WhenTogglePositive),
                RegistrationInfo.BuiltIn);
        }

        registry.Register(
            DensityFunctionKeys.ORE_VEIN_GAP,
            DensityFunctionFactories.Sub(
                DensityFunctionFactories.Constant(-0.3f),
                DensityFunctionFactories.Noise(gapNoise)),
            RegistrationInfo.BuiltIn);
    }

    private static DensityFunction CreateBaseOreVeinMask(
        Holder<NormalNoise> veinANoise,
        Holder<NormalNoise> veinBNoise,
        DensityFunction y,
        DensityFunction toggle,
        int minY,
        int maxY)
    {
        DensityFunction veinA = DensityFunctionFactories.Abs(
            YLimitedInterpolatable(
                y,
                DensityFunctionFactories.Noise(veinANoise, 4.0, 4.0),
                minY,
                maxY,
                1,
                4,
                8));

        DensityFunction veinB = DensityFunctionFactories.Abs(
            YLimitedInterpolatable(
                y,
                DensityFunctionFactories.Noise(veinBNoise, 4.0, 4.0),
                minY,
                maxY,
                1,
                4,
                8));

        return DensityFunctionFactories.RangeChoice(
            toggle,
            -0.4f,
            0.4f,
            DensityFunctionFactories.Constant(-1.0f),
            DensityFunctionFactories.Sub(
                DensityFunctionFactories.Constant(0.08f),
                DensityFunctionFactories.Max(veinA, veinB)));
    }

    private static DensityFunction CreateOreVeinDensity(
        int minY,
        int maxY,
        DensityFunction y,
        DensityFunction toggle,
        DensityFunction baseVeinMask,
        bool whenTogglePositive)
    {
        DensityFunction noVein = DensityFunctionFactories.Constant(-1.0f);
        DensityFunction distanceFromEdge = DensityFunctionFactories.Min(
            DensityFunctionFactories.Sub(DensityFunctionFactories.Constant(maxY), y),
            DensityFunctionFactories.Sub(y, DensityFunctionFactories.Constant(minY)));
        DensityFunction edgeRoundoff = ClampedMap(
            distanceFromEdge,
            0.0f,
            20.0f,
            -0.2f,
            0.0f);
        DensityFunction veininess = whenTogglePositive
            ? toggle
            : DensityFunctionFactories.Negate(toggle);

        DensityFunction veinBody = DensityFunctionFactories.RangeChoice(
            DensityFunctionFactories.Add(
                DensityFunctionFactories.Sub(veininess, 0.4f),
                edgeRoundoff),
            0.0f,
            DensityFunctionFactories.MAX_REASONABLE_NOISE_VALUE,
            DensityFunctionFactories.Constant(0.7f),
            noVein);

        DensityFunction maskedVein = DensityFunctionFactories.RangeChoice(
            baseVeinMask,
            0.0f,
            DensityFunctionFactories.MAX_REASONABLE_NOISE_VALUE,
            veinBody,
            noVein);

        return DensityFunctionFactories.RangeChoice(
            y,
            minY,
            maxY,
            maskedVein,
            noVein);
    }

    private static DensityFunction YLimitedInterpolatable(
        DensityFunction y,
        DensityFunction whenInRange,
        int minYInclusive,
        int maxYInclusive,
        float whenOutOfRange,
        int cellSizeXz,
        int cellSizeY) =>
        DensityFunctionFactories.Interpolate(
            DensityFunctionFactories.RangeChoice(
                y,
                minYInclusive,
                maxYInclusive + 1,
                whenInRange,
                DensityFunctionFactories.Constant(whenOutOfRange)),
            cellSizeXz,
            cellSizeY);

    private static DensityFunction ClampedMap(
        DensityFunction input,
        float fromMin,
        float fromMax,
        float toMin,
        float toMax) =>
        Remap(
            DensityFunctionFactories.Clamp(input, fromMin, fromMax),
            fromMin,
            fromMax,
            toMin,
            toMax);

    private static DensityFunction Remap(
        DensityFunction input,
        float fromMin,
        float fromMax,
        float toMin,
        float toMax)
    {
        float factor = (toMax - toMin) / (fromMax - fromMin);
        float offset = toMin - fromMin * factor;
        DensityFunction scaled = DensityFunctionFactories.Mul(input, factor);
        return offset == 0.0f
            ? scaled
            : DensityFunctionFactories.Add(scaled, offset);
    }

    private static Holder<NormalNoise> GetNoise(RegistryKey<NormalNoise> key) =>
        BuiltInRegistries.Noise.GetHolder(key) ??
        throw new InvalidOperationException($"Missing built-in noise parameters: {key.Location}");

    private static DensityFunction GetFunction(
        Registry<DensityFunction> registry,
        RegistryKey<DensityFunction> key)
    {
        Holder<DensityFunction>.Reference holder =
            registry.CreateRegistrationLookup().Get(key) ??
            throw new InvalidOperationException($"Missing built-in density function: {key.Location}");
        return new DensityFunctionFactories.HolderHolder(holder);
    }

    private static DensityFunction RegisterAndWrap(
        Registry<DensityFunction> registry,
        RegistryKey<DensityFunction> key,
        DensityFunction value)
    {
        registry.Register(key, value, RegistrationInfo.BuiltIn);
        return GetFunction(registry, key);
    }
}
