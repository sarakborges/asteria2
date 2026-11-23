using Asteria.Content.Registries;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.Synth;
using DensityFunctionFactories = Asteria.World.LevelGen.DensityFunctions.DensityFunctions;

namespace Asteria.World.LevelGen;

/// <summary>
/// Initial vanilla 26.3 density-function bootstrap prefix.
/// Additional entries must be ported in NoiseRouterData.bootstrap order.
/// </summary>
internal static class BuiltInDensityFunctions
{
    internal const int Count = 7;

    internal static void RegisterAll()
    {
        Registry<DensityFunction> registry = BuiltInRegistries.DensityFunction;

        // NoiseRouterData.bootstrap registers ZERO and Y before ore-vein functions.
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

        // NoiseRouterData.bootstrap: the three legacy blended-noise dimension bases.
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
}
