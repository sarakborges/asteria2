using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.World.LevelGen.DensityFunctions;

namespace Asteria.World.LevelGen;

/// <summary>
/// Built-in density-function keys from Minecraft Java 26.3, using Asteria's local registry namespace.
/// </summary>
public static class DensityFunctionKeys
{
    public static RegistryKey<DensityFunction> ZERO { get; } = CreateKey("zero");
    public static RegistryKey<DensityFunction> Y { get; } = CreateKey("y");
    public static RegistryKey<DensityFunction> SHIFT_X { get; } = CreateKey("shift_x");
    public static RegistryKey<DensityFunction> SHIFT_Z { get; } = CreateKey("shift_z");
    public static RegistryKey<DensityFunction> BASE_3D_NOISE_OVERWORLD { get; } = CreateKey("overworld/base_3d_noise");
    public static RegistryKey<DensityFunction> BASE_3D_NOISE_NETHER { get; } = CreateKey("nether/base_3d_noise");
    public static RegistryKey<DensityFunction> BASE_3D_NOISE_END { get; } = CreateKey("end/base_3d_noise");

    private static RegistryKey<DensityFunction> CreateKey(string name) =>
        RegistryKey<DensityFunction>.Create(
            Registries.DENSITY_FUNCTION,
            ResourceLocation.WithDefaultNamespace(name));
}
