using Asteria.Content.Registries;
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
    internal const int Count = 13;

    private const int CopperVeinMinY = 0;
    private const int CopperVeinMaxY = 50;
    private const int IronVeinMinY = -60;
    private const int IronVeinMaxY = -8;

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
            DensityFunctionFactories.ShiftA(shiftNoise),
            RegistrationInfo.BuiltIn);

        registry.Register(
            DensityFunctionKeys.SHIFT_Z,
            DensityFunctionFactories.ShiftB(shiftNoise),
            RegistrationInfo.BuiltIn);

        registry.Register(
            DensityFunctionKeys.BASE_3D_NOISE_OVERWORLD,
            new BlendedNoise(0.25, 0.125, 80.0, 160.0, 8.0),
            RegistrationInfo.BuiltIn);

        registry.Register(
            DensityFunctionKeys.BASE_3D_NOISE_NETHER,
            new BlendedNoise(0.25, 0.375, 80.0, 60.0, 8.0),
            RegistrationInfo.BuiltIn);

        registry.Register(
            DensityFunctionKeys.BASE_3D_NOISE_END,
            new BlendedNoise(0.25, 0.25, 80.0, 160.0, 4.0),
            RegistrationInfo.BuiltIn);
    }

    private static void RegisterOreVeins(Registry<DensityFunction> registry)
    {
        Holder<NormalNoise> veininessNoise = GetNoise(Noises.ORE_VEININESS);
        Holder<NormalNoise> veinANoise = GetNoise(Noises.ORE_VEIN_A);
        Holder<NormalNoise> veinBNoise = GetNoise(Noises.ORE_VEIN_B);
        Holder<NormalNoise> gapNoise = GetNoise(Noises.ORE_GAP);

        DensityFunction y = GetFunction(registry, DensityFunctionKeys.Y);
        int veinMinY = Math.Min(CopperVeinMinY, IronVeinMinY);
        int veinMaxY = Math.Max(CopperVeinMaxY, IronVeinMaxY);
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

        registry.Register(
            DensityFunctionKeys.ORE_VEIN_COPPER_DENSITY,
            CreateOreVeinDensity(
                CopperVeinMinY,
                CopperVeinMaxY,
                y,
                veinToggle,
                veinMask,
                whenTogglePositive: true),
            RegistrationInfo.BuiltIn);

        registry.Register(
            DensityFunctionKeys.ORE_VEIN_IRON_DENSITY,
            CreateOreVeinDensity(
                IronVeinMinY,
                IronVeinMaxY,
                y,
                veinToggle,
                veinMask,
                whenTogglePositive: false),
            RegistrationInfo.BuiltIn);

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
        DensityFunctionFactories.Lerp(
            DensityFunctionFactories.Clamp(
                DensityFunctionFactories.Div(
                    DensityFunctionFactories.Sub(input, fromMin),
                    fromMax - fromMin),
                0.0f,
                1.0f),
            toMin,
            DensityFunctionFactories.Constant(toMax));

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
