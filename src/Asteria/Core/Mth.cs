namespace Asteria.Core;

/// <summary>
/// Numeric primitives required by core spatial systems. The trigonometric lookup table,
/// quantization and integer conversion match the corresponding 26.3 utility operations.
/// </summary>
public static class Mth
{
    private const double SinScale = 10430.378350470453;
    private const int SinMask = 65535;
    private const int CosOffset = 16384;
    private static readonly float[] SinTable = CreateSinTable();

    private static float[] CreateSinTable()
    {
        var values = new float[SinMask + 1];
        for (int index = 0; index < values.Length; index++)
            values[index] = (float)Math.Sin(index / SinScale);
        return values;
    }

    public static float Sin(double value)
    {
        long index = JavaLongCast(value * SinScale);
        return SinTable[(int)(index & SinMask)];
    }

    public static float Cos(double value)
    {
        long index = JavaLongCast(value * SinScale + CosOffset);
        return SinTable[(int)(index & SinMask)];
    }

    public static float Sqrt(float value) =>
        (float)Math.Sqrt(value);

    public static int Floor(float value) => Floor((double)value);

    public static int Floor(double value)
    {
        if (double.IsNaN(value))
            return 0;

        double floor = Math.Floor(value);
        if (floor >= int.MaxValue)
            return int.MaxValue;
        if (floor <= int.MinValue)
            return int.MinValue;

        return (int)floor;
    }

    public static float Lerp(float alpha, float from, float to) =>
        from + alpha * (to - from);

    public static float Lerp2(
        float alpha1,
        float alpha2,
        float x00,
        float x10,
        float x01,
        float x11) =>
        Lerp(alpha2, Lerp(alpha1, x00, x10), Lerp(alpha1, x01, x11));

    public static float Lerp3(
        float alpha1,
        float alpha2,
        float alpha3,
        float x000,
        float x100,
        float x010,
        float x110,
        float x001,
        float x101,
        float x011,
        float x111) =>
        Lerp(
            alpha3,
            Lerp2(alpha1, alpha2, x000, x100, x010, x110),
            Lerp2(alpha1, alpha2, x001, x101, x011, x111));

    public static float Smoothstep(float value) =>
        value * value * value * (value * (value * 6.0f - 15.0f) + 10.0f);

    public static float SmoothstepDerivative(float value) =>
        30.0f * value * value * (value - 1.0f) * (value - 1.0f);

    public static double Lerp(double alpha, double from, double to) =>
        from + alpha * (to - from);

    public static double Frac(double value) =>
        value - Math.Floor(value);

    public static int Sign(double value) =>
        value == 0.0 ? 0 : value > 0.0 ? 1 : -1;

    public static long GetSeed(int x, int y, int z)
    {
        long seed = unchecked((long)(x * 3129871) ^ (long)z * 116129781L ^ y);
        seed = unchecked(seed * seed * 42317861L + seed * 11L);
        return seed >> 16;
    }

    public static double Square(double value) => value * value;

    public static double Clamp(double value, double minimum, double maximum) =>
        value < minimum ? minimum : value > maximum ? maximum : value;

    public static double ClampedLerp(double alpha, double minimum, double maximum) =>
        alpha < 0.0 ? minimum : alpha > 1.0 ? maximum : Lerp(alpha, minimum, maximum);

    public static double Map(
        double value,
        double fromMinimum,
        double fromMaximum,
        double toMinimum,
        double toMaximum) =>
        Lerp(
            (value - fromMinimum) / (fromMaximum - fromMinimum),
            toMinimum,
            toMaximum);

    public static double ClampedMap(
        double value,
        double fromMinimum,
        double fromMaximum,
        double toMinimum,
        double toMaximum) =>
        ClampedLerp(
            (value - fromMinimum) / (fromMaximum - fromMinimum),
            toMinimum,
            toMaximum);

    public static int Quantize(double value, int resolution) =>
        unchecked(Floor(value / resolution) * resolution);

    public static float Clamp(float value, float minimum, float maximum)
    {
        if (value < minimum)
            return minimum;
        return Math.Min(value, maximum);
    }

    public static int Clamp(int value, int minimum, int maximum) =>
        value < minimum ? minimum : value > maximum ? maximum : value;

    /// <summary>Integer floor division, matching Java Math.floorDiv.</summary>
    public static int FloorDiv(int numerator, int denominator)
    {
        if (denominator == 0)
            throw new DivideByZeroException();
        int quotient = numerator / denominator;
        int remainder = numerator % denominator;
        if (remainder != 0 && (numerator ^ denominator) < 0)
            quotient = unchecked(quotient - 1);
        return quotient;
    }

    /// <summary>Integer floor modulus, matching Java Math.floorMod.</summary>
    public static int FloorMod(int numerator, int denominator) =>
        unchecked(numerator - FloorDiv(numerator, denominator) * denominator);

    /// <summary>Positive ceiling division, matching Minecraft Java 26.3 Mth.positiveCeilDiv.</summary>
    public static int PositiveCeilDiv(int input, int divisor) =>
        unchecked(-FloorDiv(unchecked(-input), divisor));

    /// <summary>
    /// Rounds an integer upward to the next multiple, matching Minecraft Java 26.3 Mth.roundToward.
    /// </summary>
    public static int RoundToward(int input, int multiple) =>
        unchecked(PositiveCeilDiv(input, multiple) * multiple);


    public static float LengthSquared(float x, float y, float z) =>
        x * x + y * y + z * z;

    public static double LengthSquared(double x, double y) =>
        x * x + y * y;

    public static double LengthSquared(double x, double y, double z) =>
        x * x + y * y + z * z;

    public static double Length(double x, double y) =>
        Math.Sqrt(LengthSquared(x, y));

    public static double Length(double x, double y, double z) =>
        Math.Sqrt(LengthSquared(x, y, z));

    /// <summary>
    /// Bit-level inverse-square-root approximation retained by Minecraft Java 26.3
    /// for structure terrain-adaptation sampling.
    /// </summary>
    public static double FastInvSqrt(double value)
    {
        double half = 0.5 * value;
        long bits = BitConverter.DoubleToInt64Bits(value);
        bits = unchecked(6910469410427058090L - (bits >> 1));
        value = BitConverter.Int64BitsToDouble(bits);
        return value * (1.5 - half * value * value);
    }

    private static long JavaLongCast(double value)
    {
        if (double.IsNaN(value))
            return 0L;
        if (value >= 9223372036854775808.0)
            return long.MaxValue;
        if (value <= -9223372036854775808.0)
            return long.MinValue;

        return (long)value;
    }
}
