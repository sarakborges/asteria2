using System.Numerics;

namespace Asteria.Util;

/// <summary>
/// Packed ARGB color conversions used by data codecs and rendering code.
/// Channel ordering and floor-based quantization match the 26.3 source.
/// </summary>
public static class Argb
{
    public static int Opaque(int color) =>
        color | unchecked((int)0xFF000000);

    public static int Transparent(int color) =>
        color & 0x00FFFFFF;

    public static Vector3 Vector3FromRgb24(int color) =>
        new(RedFloat(color), GreenFloat(color), BlueFloat(color));

    public static Vector4 Vector4FromArgb32(int color) =>
        new(RedFloat(color), GreenFloat(color), BlueFloat(color), AlphaFloat(color));

    public static int ColorFromVector3(Vector3 color) =>
        Color(255, As8BitChannel(color.X), As8BitChannel(color.Y), As8BitChannel(color.Z));

    public static int ColorFromVector4(Vector4 color) =>
        Color(
            As8BitChannel(color.W),
            As8BitChannel(color.X),
            As8BitChannel(color.Y),
            As8BitChannel(color.Z));

    public static int ColorFromFloat(float alpha, float red, float green, float blue) =>
        Color(
            As8BitChannel(alpha),
            As8BitChannel(red),
            As8BitChannel(green),
            As8BitChannel(blue));

    public static int As8BitChannel(float value) =>
        FloorToInt(value * 255.0F);

    public static float AlphaFloat(int color) =>
        From8BitChannel((int)((uint)color >> 24));

    public static float RedFloat(int color) =>
        From8BitChannel((color >> 16) & 0xFF);

    public static float GreenFloat(int color) =>
        From8BitChannel((color >> 8) & 0xFF);

    public static float BlueFloat(int color) =>
        From8BitChannel(color & 0xFF);

    public static int Color(int alpha, int red, int green, int blue) =>
        unchecked((alpha << 24) | (red << 16) | (green << 8) | blue);

    private static float From8BitChannel(int value) =>
        value / 255.0F;

    private static int FloorToInt(float value)
    {
        if (float.IsNaN(value))
            return 0;
        if (value >= int.MaxValue)
            return int.MaxValue;
        if (value <= int.MinValue)
            return int.MinValue;
        return (int)MathF.Floor(value);
    }
}
