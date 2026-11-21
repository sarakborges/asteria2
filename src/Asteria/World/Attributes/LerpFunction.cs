using System.Numerics;

namespace Asteria.World.Attributes;

/// <summary>Typed interpolation for attribute values and modifier arguments.</summary>
public sealed class LerpFunction<T>
{
    private readonly Func<float, T, T, T> _lerp;

    public LerpFunction(Func<float, T, T, T> lerp) =>
        _lerp = lerp ?? throw new ArgumentNullException(nameof(lerp));

    public T Apply(float alpha, T from, T to) => _lerp(alpha, from, to);

    public static LerpFunction<float> OfFloat() =>
        new(static (alpha, from, to) => from + (to - from) * alpha);

    public static LerpFunction<Vector3> OfColorVec3() =>
        new(static (alpha, from, to) => Vector3.Lerp(from, to, alpha));

    public static LerpFunction<Vector4> OfColorVec4() =>
        new(static (alpha, from, to) => Vector4.Lerp(from, to, alpha));

    public static LerpFunction<float> OfDegrees(float maxDelta) =>
        new((alpha, from, to) =>
        {
            float delta = WrapDegrees(to - from);
            return MathF.Abs(delta) >= maxDelta ? to : from + alpha * delta;
        });

    private static float WrapDegrees(float value)
    {
        float angle = value % 360.0F;
        if (angle >= 180.0F) angle -= 360.0F;
        if (angle < -180.0F) angle += 360.0F;
        return angle;
    }

    public static LerpFunction<T> OfConstant() => OfStep(1.0F);

    public static LerpFunction<T> OfStep(float threshold) =>
        new((alpha, from, to) => alpha >= threshold ? to : from);
}
