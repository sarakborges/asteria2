using System.Numerics;

namespace Asteria.Core.Mathematics;

/// <summary>
/// Read-only 3x3 single-precision matrix. Elements use row/column notation and vectors are treated as columns.
/// </summary>
public readonly record struct Matrix3x3(
    float M00, float M01, float M02,
    float M10, float M11, float M12,
    float M20, float M21, float M22)
{
    public static Matrix3x3 Identity { get; } = new(
        1.0f, 0.0f, 0.0f,
        0.0f, 1.0f, 0.0f,
        0.0f, 0.0f, 1.0f);

    public static Matrix3x3 CreateScale(float x, float y, float z) =>
        new(
            x, 0.0f, 0.0f,
            0.0f, y, 0.0f,
            0.0f, 0.0f, z);

    public static Matrix3x3 CreatePermutation(int p0, int p1, int p2)
    {
        ValidatePermutation(p0, p1, p2);

        var values = new float[3, 3];
        values[0, p0] = 1.0f;
        values[1, p1] = 1.0f;
        values[2, p2] = 1.0f;

        return new Matrix3x3(
            values[0, 0], values[0, 1], values[0, 2],
            values[1, 0], values[1, 1], values[1, 2],
            values[2, 0], values[2, 1], values[2, 2]);
    }

    public Matrix3x3 Multiply(Matrix3x3 other) =>
        new(
            M00 * other.M00 + M01 * other.M10 + M02 * other.M20,
            M00 * other.M01 + M01 * other.M11 + M02 * other.M21,
            M00 * other.M02 + M01 * other.M12 + M02 * other.M22,
            M10 * other.M00 + M11 * other.M10 + M12 * other.M20,
            M10 * other.M01 + M11 * other.M11 + M12 * other.M21,
            M10 * other.M02 + M11 * other.M12 + M12 * other.M22,
            M20 * other.M00 + M21 * other.M10 + M22 * other.M20,
            M20 * other.M01 + M21 * other.M11 + M22 * other.M21,
            M20 * other.M02 + M21 * other.M12 + M22 * other.M22);

    public Vector3 Transform(Vector3 vector) =>
        new(
            M00 * vector.X + M01 * vector.Y + M02 * vector.Z,
            M10 * vector.X + M11 * vector.Y + M12 * vector.Z,
            M20 * vector.X + M21 * vector.Y + M22 * vector.Z);

    public float Get(int row, int column) =>
        (row, column) switch
        {
            (0, 0) => M00,
            (0, 1) => M01,
            (0, 2) => M02,
            (1, 0) => M10,
            (1, 1) => M11,
            (1, 2) => M12,
            (2, 0) => M20,
            (2, 1) => M21,
            (2, 2) => M22,
            _ => throw new ArgumentOutOfRangeException(
                row is < 0 or > 2 ? nameof(row) : nameof(column),
                "Matrix coordinates must be between 0 and 2.")
        };

    private static void ValidatePermutation(int p0, int p1, int p2)
    {
        if ((uint)p0 > 2u || (uint)p1 > 2u || (uint)p2 > 2u ||
            p0 == p1 || p0 == p2 || p1 == p2)
        {
            throw new ArgumentException("The values must be a permutation of 0, 1 and 2.");
        }
    }
}
