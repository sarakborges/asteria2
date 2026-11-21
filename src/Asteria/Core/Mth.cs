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
