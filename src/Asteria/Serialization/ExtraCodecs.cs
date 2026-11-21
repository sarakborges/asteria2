using System.Globalization;
using System.Numerics;
using Asteria.Util;

namespace Asteria.Serialization;

/// <summary>Specialized data codecs shared by built-in content types.</summary>
public static class ExtraCodecs
{
    public static readonly Codec<Vector3> VECTOR3F = CreateVector3Codec();
    public static readonly Codec<Vector4> VECTOR4F = CreateVector4Codec();

    public static readonly Codec<Vector3> RGB_COLOR_VEC3_CODEC =
        VECTOR3F.WithAlternative(Codecs.INT, Argb.Vector3FromRgb24);

    public static readonly Codec<Vector4> ARGB_COLOR_VEC4_CODEC =
        VECTOR4F.WithAlternative(Codecs.INT, Argb.Vector4FromArgb32);

    public static readonly Codec<Vector3> STRING_RGB_VEC3_COLOR =
        HexColor(6)
            .XMap(Argb.Opaque, Argb.Transparent)
            .XMap(Argb.Vector3FromRgb24, Argb.ColorFromVector3)
            .WithAlternative(RGB_COLOR_VEC3_CODEC, static value => value);

    public static readonly Codec<Vector4> STRING_ARGB_VEC4_COLOR =
        HexColor(8)
            .XMap(Argb.Vector4FromArgb32, Argb.ColorFromVector4)
            .WithAlternative(ARGB_COLOR_VEC4_CODEC, static value => value);

    public static Codec<int> HexColor(int digits)
    {
        if (digits is not (6 or 8))
            throw new ArgumentOutOfRangeException(nameof(digits));

        return Codecs.STRING.FlatXMap(
            input =>
            {
                if (!input.StartsWith('#'))
                    return DataResult<int>.Error("Hex color must begin with #");

                int actualDigits = input.Length - 1;
                if (actualDigits != digits)
                {
                    return DataResult<int>.Error(
                        $"Hex color is wrong size, expected {digits} digits but got {actualDigits}");
                }

                if (!uint.TryParse(
                        input.AsSpan(1),
                        NumberStyles.AllowHexSpecifier,
                        CultureInfo.InvariantCulture,
                        out uint value))
                {
                    return DataResult<int>.Error($"Invalid color value: {input}");
                }

                return DataResult<int>.Success(unchecked((int)value));
            },
            value =>
            {
                string hex = unchecked((uint)value).ToString($"x{digits}", CultureInfo.InvariantCulture);
                return DataResult<string>.Success("#" + hex);
            });
    }

    private static Codec<Vector3> CreateVector3Codec() =>
        Codecs.FLOAT.ListOf().FlatXMap(
            values =>
            {
                if (values.Count != 3)
                {
                    string error = "Input is not a list of 3 elements";
                    return values.Count > 3
                        ? DataResult<Vector3>.Error(
                            error,
                            new Vector3(values[0], values[1], values[2]))
                        : DataResult<Vector3>.Error(error);
                }

                return DataResult<Vector3>.Success(
                    new Vector3(values[0], values[1], values[2]));
            },
            value => DataResult<IReadOnlyList<float>>.Success(
                new[] { value.X, value.Y, value.Z }));

    private static Codec<Vector4> CreateVector4Codec() =>
        Codecs.FLOAT.ListOf().FlatXMap(
            values =>
            {
                if (values.Count != 4)
                {
                    string error = "Input is not a list of 4 elements";
                    return values.Count > 4
                        ? DataResult<Vector4>.Error(
                            error,
                            new Vector4(values[0], values[1], values[2], values[3]))
                        : DataResult<Vector4>.Error(error);
                }

                return DataResult<Vector4>.Success(
                    new Vector4(values[0], values[1], values[2], values[3]));
            },
            value => DataResult<IReadOnlyList<float>>.Success(
                new[] { value.X, value.Y, value.Z, value.W }));
}
