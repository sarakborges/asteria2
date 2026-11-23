using Asteria.Content.Registries;
using Asteria.World.LevelGen.DensityFunctions;
using DensityFunctionFactories = Asteria.World.LevelGen.DensityFunctions.DensityFunctions;

namespace Asteria.World.LevelGen;

/// <summary>
/// Initial vanilla 26.3 density-function bootstrap prefix.
/// Additional entries must be ported in NoiseRouterData.bootstrap order.
/// </summary>
internal static class BuiltInDensityFunctions
{
    internal const int Count = 2;

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
    }
}
