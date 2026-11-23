using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.DensityFunctions.Operations;

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

    public static DensityFunction Abs(DensityFunction input) =>
        new UnaryFunction(UnaryFunction.Type.ABS, input);

    public static DensityFunction Square(DensityFunction input) =>
        new UnaryFunction(UnaryFunction.Type.SQUARE, input);

    public static DensityFunction Cube(DensityFunction input) =>
        new UnaryFunction(UnaryFunction.Type.CUBE, input);

    public static DensityFunction Sqrt(DensityFunction input) =>
        new UnaryFunction(UnaryFunction.Type.SQRT, input);

    public static DensityFunction HalfNegative(DensityFunction input) =>
        new UnaryFunction(UnaryFunction.Type.HALF_NEGATIVE, input);

    public static DensityFunction QuarterNegative(DensityFunction input) =>
        new UnaryFunction(UnaryFunction.Type.QUARTER_NEGATIVE, input);

    public static DensityFunction Reciprocal(DensityFunction input) =>
        new UnaryFunction(UnaryFunction.Type.RECIPROCAL, input);

    public static DensityFunction Negate(DensityFunction input) =>
        new UnaryFunction(UnaryFunction.Type.NEGATE, input);

    public static DensityFunction Squeeze(DensityFunction input) =>
        new UnaryFunction(UnaryFunction.Type.SQUEEZE, input);

    public static DensityFunction Log(DensityFunction input) =>
        new UnaryFunction(UnaryFunction.Type.LOG, input);

    public static DensityFunction Sign(DensityFunction input) =>
        new UnaryFunction(UnaryFunction.Type.SIGN, input);

}
