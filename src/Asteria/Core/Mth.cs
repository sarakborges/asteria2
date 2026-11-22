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

    public static int Clamp(int value, int minimum, int maximum) =>
        value < minimum ? minimum : value > maximum ? maximum : value;


    public static double LengthSquared(double x, double y) =>
        x * x + y * y;

    public static double LengthSquared(double x, double y, double z) =>
        x * x + y * y + z * z;

    public static double Length(double x, double y) =>
        Math.Sqrt(LengthSquared(x, y));

    public static double Length(double x, double y, double z) =>
        Math.Sqrt(LengthSquared(x, y, z));

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
