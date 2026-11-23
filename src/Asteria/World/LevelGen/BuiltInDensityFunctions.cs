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
    internal const int Count = 21;

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
    }

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
        Holder<DensityFunction>.Reference holder = registry.GetHolder(key) ??
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
