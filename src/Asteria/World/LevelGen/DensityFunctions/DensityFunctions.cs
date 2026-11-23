using Asteria.World.LevelGen.DensityFunctions.Generator;

namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// Runtime density-function constructors from Minecraft Java 26.3.
/// Registry/bootstrap and codec APIs are deferred with local persistence.
/// </summary>
public static class DensityFunctions
{
    private static readonly DensityFunction ZERO = new ConstantFunction(0.0f);

    public static DensityFunction Zero() => ZERO;

    public static DensityFunction Constant(float value) =>
        new ConstantFunction(value);
}
