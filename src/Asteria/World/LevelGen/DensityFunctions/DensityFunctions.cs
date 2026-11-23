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

    public static DensityFunction Add(DensityFunction left, DensityFunction right) =>
        new BinaryFunction(BinaryFunction.Type.ADD, left, right);

    public static DensityFunction Add(DensityFunction left, float right) =>
        Add(left, Constant(right));

    public static DensityFunction Sub(DensityFunction left, DensityFunction right) =>
        new BinaryFunction(BinaryFunction.Type.SUB, left, right);

    public static DensityFunction Sub(DensityFunction left, float right) =>
        Sub(left, Constant(right));

    public static DensityFunction Mul(DensityFunction left, DensityFunction right) =>
        new BinaryFunction(BinaryFunction.Type.MUL, left, right);

    public static DensityFunction Mul(DensityFunction left, float right) =>
        Mul(left, Constant(right));

    public static DensityFunction Div(DensityFunction left, DensityFunction right) =>
        new BinaryFunction(BinaryFunction.Type.DIV, left, right);

    public static DensityFunction Div(DensityFunction left, float right) =>
        Div(left, Constant(right));

    public static DensityFunction Min(DensityFunction left, DensityFunction right) =>
        new BinaryFunction(BinaryFunction.Type.MIN, left, right);

    public static DensityFunction Max(DensityFunction left, DensityFunction right) =>
        new BinaryFunction(BinaryFunction.Type.MAX, left, right);

    public static DensityFunction Lerp(
        DensityFunction alpha,
        DensityFunction first,
        DensityFunction second) =>
        new LerpFunction(alpha, first, second);

    public static DensityFunction Lerp(
        DensityFunction alpha,
        float first,
        DensityFunction second) =>
        Lerp(alpha, Constant(first), second);


}
