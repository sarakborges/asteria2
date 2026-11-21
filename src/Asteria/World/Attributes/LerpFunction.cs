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

    public static LerpFunction<int> OfInteger() =>
        new(static (alpha, from, to) =>
        {
            int delta = unchecked(to - from);
            float scaled = MathF.Floor(alpha * delta);
            int step = float.IsNaN(scaled) ? 0 :
                scaled >= int.MaxValue ? int.MaxValue :
                scaled <= int.MinValue ? int.MinValue : (int)scaled;
            return unchecked(from + step);
        });

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

    /// <summary>Cross-fades two lists by scaling each source element by its remaining weight and each target element by its incoming weight.</summary>
    public static LerpFunction<IReadOnlyList<T>> OfListCrossFade(Func<T, float, T> scaler)
    {
        ArgumentNullException.ThrowIfNull(scaler);
        return new LerpFunction<IReadOnlyList<T>>((alpha, from, to) =>
        {
            ArgumentNullException.ThrowIfNull(from);
            ArgumentNullException.ThrowIfNull(to);
            if (alpha == 0.0F) return from;
            if (alpha == 1.0F) return to;

            var result = new T[from.Count + to.Count];
            int index = 0;
            foreach (T element in from)
                result[index++] = scaler(element, 1.0F - alpha);
            foreach (T element in to)
                result[index++] = scaler(element, alpha);
            return Array.AsReadOnly(result);
        });
    }

    public static LerpFunction<T> OfConstant() => OfStep(1.0F);

    public static LerpFunction<T> OfStep(float threshold) =>
        new((alpha, from, to) => alpha >= threshold ? to : from);
}
