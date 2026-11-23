using Asteria.Core;
using Asteria.Content.Registries;
using Asteria.World.LevelGen.Synth;
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

    public static DensityFunction Clamp(DensityFunction input, float min, float max) =>
        new ClampFunction(input, min, max);

    public static DensityFunction Pow(DensityFunction @base, DensityFunction exponent) =>
        new PowFunction(@base, exponent);

    public static DensityFunction Noise(Holder<NormalNoise> noiseData) =>
        Noise(noiseData, 1.0, 1.0);

    public static DensityFunction Noise(
        Holder<NormalNoise> noiseData,
        double xzScale,
        double yScale) =>
        new NoiseFunction(
            noiseData,
            xzScale,
            yScale,
            Zero(),
            Zero(),
            Zero());

    public static DensityFunction Noise(Holder<NormalNoise> noiseData, double yScale) =>
        Noise(noiseData, 1.0, yScale);

    public static DensityFunction YClampedGradient(
        int fromY,
        int toY,
        float fromValue,
        float toValue) =>
        Gradient(Direction.Axis.Y, TilingMode.CLAMP_TO_EDGE, fromY, toY, fromValue, toValue);

    public static DensityFunction Gradient(
        Direction.Axis axis,
        TilingMode tiling,
        int fromCoordinate,
        int toCoordinate,
        float fromValue,
        float toValue) =>
        new GradientFunction(axis, tiling, fromCoordinate, toCoordinate, fromValue, toValue);

    public static DensityFunction ShiftA(Holder<NormalNoise> noiseData) =>
        new ShiftNoiseFunction.ShiftA(noiseData);

    public static DensityFunction ShiftB(Holder<NormalNoise> noiseData) =>
        new ShiftNoiseFunction.ShiftB(noiseData);

    public static DensityFunction Shift(Holder<NormalNoise> noiseData) =>
        new ShiftNoiseFunction.Shift(noiseData);

    public static DensityFunction ShiftedNoise2d(
        DensityFunction shiftX,
        DensityFunction shiftZ,
        double xzScale,
        Holder<NormalNoise> noiseData) =>
        new NoiseFunction(
            noiseData,
            xzScale,
            0.0,
            shiftX,
            Zero(),
            shiftZ);

    public static DensityFunction RangeChoice(
        DensityFunction input,
        float minInclusive,
        float maxExclusive,
        DensityFunction whenInRange,
        DensityFunction whenOutOfRange) =>
        new RangeChoiceFunction(input, minInclusive, maxExclusive, whenInRange, whenOutOfRange);

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

    public static DensityFunction Round(
        RoundFunction.Type type,
        DensityFunction input,
        DensityFunction multiple) =>
        new RoundFunction(type, input, multiple);

    public static DensityFunction Round(RoundFunction.Type type, DensityFunction input) =>
        Round(type, input, Constant(1.0f));

    public static DensityFunction Floor(DensityFunction input, DensityFunction multiple) =>
        Round(RoundFunction.Type.FLOOR, input, multiple);

    public static DensityFunction Floor(DensityFunction input) =>
        Floor(input, Constant(1.0f));

    public static DensityFunction Round(DensityFunction input, DensityFunction multiple) =>
        Round(RoundFunction.Type.ROUND, input, multiple);

    public static DensityFunction Round(DensityFunction input) =>
        Round(input, Constant(1.0f));

    public static DensityFunction Ceil(DensityFunction input, DensityFunction multiple) =>
        Round(RoundFunction.Type.CEIL, input, multiple);

    public static DensityFunction Ceil(DensityFunction input) =>
        Ceil(input, Constant(1.0f));

    public static DensityFunction Truncate(DensityFunction input, DensityFunction multiple) =>
        Round(RoundFunction.Type.TRUNCATE, input, multiple);

    public static DensityFunction Truncate(DensityFunction input) =>
        Truncate(input, Constant(1.0f));

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
